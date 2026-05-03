using UnityEngine;

namespace EldritchFarm.Crops
{
    /// <summary>
    /// Base class for all crops. Handles:
    ///   - Reference to CropData (configuration)
    ///   - Current state + state transition plumbing
    ///   - State-change broadcasting (so visuals/audio can react)
    ///   - Harvest contract
    ///   - Growth timer (how long since planted)
    ///   - Per-instance identity for debugging
    ///
    /// Subclasses (SentryCorn, AggressivePumpkin, SkittishTomato, etc.)
    /// override the OnState* hooks to express their unique behavior, and
    /// subscribe to whichever specific CropEventBus events they care about.
    /// They also decide *when* their growth + state combination should
    /// become Harvestable — the base class only tracks maturity, not what to do with it.
    /// </summary>
    public abstract class CropBehavior : MonoBehaviour
    {
        [SerializeField] protected CropData data;

        [Header("Debug")]
        [Tooltip("Log state transitions to the console.")]
        [SerializeField] protected bool logStateTransitions = true;

        public CropData Data => data;
        public CropState CurrentState { get; private set; } = CropState.Idle;

        public string InstanceId { get; private set; }

        protected float stateTimer;

        // Time since the crop was planted (or since OnEnable, in practice).
        // Used by subclasses to decide when the crop is mature enough to harvest.
        protected float ageSeconds;

        /// <summary>
        /// Public read-only accessor for the crop's age in seconds. Visualizers
        /// and UI use this for things like growth-progress bars or scale ramps.
        /// </summary>
        public float Age => ageSeconds;

        /// <summary>
        /// True once the crop has been alive long enough to be considered grown.
        /// Subclasses use this in combination with their own state to decide when
        /// to transition to Harvestable.
        /// </summary>
        public bool IsMature => data != null && ageSeconds >= data.growthTimeSeconds;

        protected virtual void Awake()
        {
            // Stable per-instance ID, modded down to 4 digits for log readability.
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
            ageSeconds += Time.deltaTime;
            TickState(CurrentState);
        }

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