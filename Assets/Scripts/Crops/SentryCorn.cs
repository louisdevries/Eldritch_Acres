using UnityEngine;

namespace EldritchFarm.Crops
{
    /// <summary>
    /// Watches the player. Screams when approached. Alerts neighbors via the event bus.
    ///
    /// Becomes harvestable when grown AND calm for a brief period after any reaction.
    /// Walking up to grab a calm corn might startle it again — it'll go through its
    /// scream cycle and re-become harvestable shortly after calming. The growth timer
    /// doesn't reset when this happens; the corn is grown, just temporarily uncalm.
    /// </summary>
    public class SentryCorn : CropBehavior
    {
        [Header("Refs")]
        [Tooltip("Assign the player's transform (or leave empty to auto-find by 'Player' tag).")]
        [SerializeField] private Transform player;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        // See CropBehavior — these are gating timers for the rescream cooldown
        // and the post-scream alert hold.
        private float lastReactionTime = Mathf.NegativeInfinity;
        private float alertHoldUntilTime = 0f;

        // Carried-forward intensity for chain decay. Defaults to 1 (full scream)
        // for player-triggered events; set lower by HandleScream when relaying.
        private float pendingScreamIntensity = 1f;

        // Facing direction the corn is currently "looking." Slowly rotates while
        // Idle/Harvestable so the player has windows to approach unseen.
        // Locked once the corn enters Alert (it's now staring at the player directly).
        private Vector3 facingDirection = Vector3.forward;
        private float currentFacingAngle = 0f; // degrees, on the Y axis
        private bool facingLocked = false;

        protected override void OnEnable()
        {
            base.OnEnable();

            if (player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) player = go.transform;
            }

            // Initialise facing from the prefab's transform forward, so prefab orientation
            // determines starting look direction.
            currentFacingAngle = transform.eulerAngles.y;
            UpdateFacingFromAngle();
        }

        // -------------------------------------------------------------
        // Event subscriptions
        // -------------------------------------------------------------

        protected override void SubscribeToEvents()
        {
            CropEventBus.OnScream += HandleScream;
        }

        protected override void UnsubscribeFromEvents()
        {
            CropEventBus.OnScream -= HandleScream;
        }

        private void HandleScream(CropScreamEvent evt)
        {
            if (evt.Source == this) return;
            if (!IsWithinRange(evt.Position, evt.Radius)) return;
            if (data == null) return;

            alertHoldUntilTime = Time.time + data.alertHoldDuration;

            float relayedIntensity = evt.Intensity * data.chainIntensityDecay;
            bool willRelay = relayedIntensity >= data.chainIntensityThreshold;

            // Bring an idle/harvestable corn to Alert at minimum.
            if (CurrentState == CropState.Idle || CurrentState == CropState.Harvestable)
            {
                if (logStateTransitions)
                    Debug.Log($"[{InstanceId}] heard {evt.Source.InstanceId} scream", this);
                TransitionTo(CropState.Alert);
            }

            if (willRelay
                && Time.time - lastReactionTime >= data.rescreamCooldown
                && CurrentState != CropState.Reacting
                && CurrentState != CropState.Distressed)
            {
                pendingScreamIntensity = relayedIntensity;
                TransitionTo(CropState.Reacting);
            }
        }

        // -------------------------------------------------------------
        // State machine
        // -------------------------------------------------------------

        protected override void TickState(CropState state)
        {
            if (data == null) return;

            // Rotate the facing while not locked. This is the "looking around" behavior —
            // gives the player rhythmic windows to approach.
            if (!facingLocked && data.visionRotationSpeed > 0f)
            {
                currentFacingAngle += data.visionRotationSpeed * Time.deltaTime;
                if (currentFacingAngle > 360f) currentFacingAngle -= 360f;
                UpdateFacingFromAngle();
            }

            float sqrDist = player != null
                ? (player.position - transform.position).sqrMagnitude
                : float.MaxValue;

            bool playerInRange = sqrDist <= data.awarenessRadius * data.awarenessRadius;
            bool playerInReactionRange = sqrDist <= data.reactionRadius * data.reactionRadius;
            bool playerInCone = playerInRange && IsPlayerInVisionCone();

            switch (state)
            {
                case CropState.Idle:
                    // Player must be both in range AND in the cone of vision to be noticed.
                    if (playerInCone)
                    {
                        FacePlayer();
                        facingLocked = true;
                        TransitionTo(CropState.Alert);
                    }
                    else if (IsMature && stateTimer >= data.cooldownDuration)
                    {
                        TransitionTo(CropState.Harvestable);
                    }
                    break;

                case CropState.Alert:
                    bool playerOutOfRange = !playerInRange;
                    bool stillListening = Time.time < alertHoldUntilTime;
                    bool playerLeftCone = !playerInCone;

                    // Once alert, the corn's eye-contact tracks the player as long as they're visible.
                    if (playerInCone) FacePlayer();

                    if ((playerOutOfRange || playerLeftCone) && !stillListening)
                    {
                        // Lost sight — release lock and resume looking around.
                        facingLocked = false;
                        TransitionTo(CropState.Idle);
                    }
                    else if (playerInReactionRange && playerInCone
                          && Time.time - lastReactionTime >= data.rescreamCooldown)
                    {
                        TransitionTo(CropState.Reacting);
                    }
                    break;

                case CropState.Reacting:
                    if (stateTimer >= data.reactionDuration)
                        TransitionTo(CropState.Distressed);
                    break;

                case CropState.Distressed:
                    if (stateTimer >= data.cooldownDuration)
                        TransitionTo(CropState.Alert);
                    break;

                case CropState.Harvestable:
                    // Harvestable corn still sees — getting in front of it triggers the cycle again.
                    if (playerInCone)
                    {
                        FacePlayer();
                        facingLocked = true;
                        TransitionTo(CropState.Alert);
                    }
                    break;
            }
        }

        protected override void OnStateEnter(CropState state)
        {
            if (state == CropState.Reacting)
            {
                lastReactionTime = Time.time;
                RaiseScream(intensity: pendingScreamIntensity);
                pendingScreamIntensity = 1f;
            }
        }

        // -------------------------------------------------------------
        // Vision helpers
        // -------------------------------------------------------------

        /// <summary>
        /// True if the player is currently within the corn's vision cone, ignoring distance.
        /// Caller is responsible for the distance check.
        /// </summary>
        private bool IsPlayerInVisionCone()
        {
            if (player == null || data == null) return false;
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.0001f) return true; // player on top of crop counts as seen

            float angle = Vector3.Angle(facingDirection, toPlayer.normalized);
            return angle <= data.visionHalfAngleDegrees;
        }

        /// <summary>
        /// Snap the facing direction to point at the player. Used when the corn locks onto
        /// the player on transition to Alert.
        /// </summary>
        private void FacePlayer()
        {
            if (player == null) return;
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.0001f) return;

            facingDirection = toPlayer.normalized;
            currentFacingAngle = Mathf.Atan2(facingDirection.x, facingDirection.z) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Recompute facingDirection from currentFacingAngle. Called after the angle is rotated.
        /// Keeps the two representations in sync.
        /// </summary>
        private void UpdateFacingFromAngle()
        {
            float rad = currentFacingAngle * Mathf.Deg2Rad;
            facingDirection = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || data == null) return;

            // Range rings
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, data.awarenessRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, data.reactionRadius);
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, data.alertBroadcastRadius);

            // Vision cone — draw using the live facing direction at runtime, transform.forward
            // when not playing (so designers can preview prefab orientation).
            Vector3 origin = transform.position;
            Vector3 fwd = Application.isPlaying ? facingDirection : transform.forward;

            float halfAngle = data.visionHalfAngleDegrees;
            Vector3 left  = Quaternion.Euler(0f, -halfAngle, 0f) * fwd;
            Vector3 right = Quaternion.Euler(0f,  halfAngle, 0f) * fwd;

            Gizmos.color = Application.isPlaying && (CurrentState == CropState.Alert || CurrentState == CropState.Reacting)
                ? new Color(1f, 1f, 0f, 0.9f)  // bright yellow when watching
                : new Color(0f, 1f, 1f, 0.5f); // cyan when idle/looking around

            Gizmos.DrawLine(origin, origin + left  * data.awarenessRadius);
            Gizmos.DrawLine(origin, origin + right * data.awarenessRadius);
            Gizmos.DrawLine(origin + left  * data.awarenessRadius,
                            origin + right * data.awarenessRadius);
        }
#endif
    }
}