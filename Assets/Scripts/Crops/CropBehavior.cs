using UnityEngine;

namespace EldritchFarm.Crops
{
    /// <summary>
    /// Base class for all crops. Handles:
    ///   - Reference to CropData (configuration)
    ///   - Current state + state transition plumbing
    ///   - State-change broadcasting (so visuals/audio can react)
    ///   - Harvest contract
    ///   - Per-instance identity for debugging
    ///
    /// Subclasses (SentryCorn, AggressivePumpkin, SkittishTomato, etc.)
    /// override the OnState* hooks to express their unique behavior, and
    /// subscribe to whichever specific CropEventBus events they care about.
    /// </summary>
    public abstract class CropBehavior : MonoBehaviour
    {
        [SerializeField] protected CropData data;

        [Header("Debug")]
        [Tooltip("Log state transitions to the console.")]
        [SerializeField] protected bool logStateTransitions = true;

        public CropData Data => data;
        public CropState CurrentState { get; private set; } = CropState.Idle;

        /// <summary>
        /// Stable per-instance identifier — useful when you have many of the
        /// same crop type in the scene and need to tell them apart in logs.
        /// E.g. "Sentry Corn #4172".
        /// </summary>
        public string InstanceId { get; private set; }

        protected float stateTimer;

        protected virtual void Awake()
        {
            // Stable per-instance ID, modded down to 4 digits for log readability.
            // Unity 6 renamed GetInstanceID -> GetEntityId; the directive keeps
            // this compatible with older Unity versions too.
#if UNITY_6000_0_OR_NEWER
            int rawId = GetEntityId();
#else
            int rawId = GetInstanceID();
#endif
            int shortId = Mathf.Abs(rawId) % 10000;
            string typeName = data != null ? data.cropName : GetType().Name;
            InstanceId = $"{typeName} #{shortId:D4}";
        }

        protected virtual void OnEnable()
        {
            SubscribeToEvents();
        }

        protected virtual void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        protected virtual void Update()
        {
            stateTimer += Time.deltaTime;
            TickState(CurrentState);
        }

        /// <summary>
        /// Request a transition to a new state. Routes through Exit/Enter hooks
        /// and notifies the event bus so visuals/audio/etc. can react.
        /// </summary>
        public void TransitionTo(CropState next)
        {
            if (next == CurrentState) return;

            var previous = CurrentState;
            OnStateExit(previous);
            CurrentState = next;
            stateTimer = 0f;
            OnStateEnter(next);

            if (logStateTransitions)
            {
                Debug.Log($"[{Time.time:F1}s] [{InstanceId}] {previous} → {next}", this);
            }

            CropEventBus.RaiseCropStateChanged(this, previous, next);
        }

        // -------------------------------------------------------------
        // HARVEST
        // -------------------------------------------------------------

        public virtual bool CanHarvest()
        {
            return CurrentState == CropState.Harvestable;
        }

        public virtual int Harvest()
        {
            int yield = data != null ? data.yieldAmount : 0;
            RaiseHarvested();
            return yield;
        }

        // -------------------------------------------------------------
        // Helpers for subclasses to broadcast their actions
        // -------------------------------------------------------------

        protected void RaiseScream(float intensity = 1f)
        {
            if (data == null) return;
            CropEventBus.RaiseScream(new CropScreamEvent(
                this, transform.position, data.alertBroadcastRadius, intensity));
        }

        protected void RaiseAttack(Vector3 direction)
        {
            if (data == null) return;
            CropEventBus.RaiseAttack(new CropAttackEvent(
                this, transform.position, direction, data.alertBroadcastRadius));
        }

        protected void RaiseFled(Vector3 direction)
        {
            CropEventBus.RaiseFled(new CropFledEvent(
                this, transform.position, direction));
        }

        protected void RaiseHarvested()
        {
            CropEventBus.RaiseHarvested(new CropHarvestedEvent(
                this, transform.position));
        }

        protected void RaiseDistress()
        {
            if (data == null) return;
            CropEventBus.RaiseDistress(new CropDistressEvent(
                this, transform.position, data.alertBroadcastRadius));
        }

        // -------------------------------------------------------------
        // Hooks for subclasses
        // -------------------------------------------------------------

        protected virtual void SubscribeToEvents() { }
        protected virtual void UnsubscribeFromEvents() { }

        protected virtual void OnStateEnter(CropState state) { }
        protected virtual void OnStateExit(CropState state) { }
        protected virtual void TickState(CropState state) { }

        // -------------------------------------------------------------
        // Utilities
        // -------------------------------------------------------------

        protected bool IsWithinRange(Vector3 point, float radius)
        {
            return (transform.position - point).sqrMagnitude <= radius * radius;
        }
    }
}