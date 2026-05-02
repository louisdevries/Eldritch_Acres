using UnityEngine;

namespace EldritchFarm.Crops
{
    /// <summary>
    /// First concrete crop. Watches the player. Screams when approached.
    /// Other Sentries hearing the scream become alert themselves.
    /// </summary>
    public class SentryCorn : CropBehavior
    {
        [Header("Refs")]
        [Tooltip("Assign the player's transform (or leave empty to auto-find by 'Player' tag).")]
        [SerializeField] private Transform player;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        // Tracks when this corn last entered the Reacting state. Used to enforce
        // the "take a breath" cooldown so the corn doesn't scream continuously
        // while the player lingers in range.
        private float lastReactionTime = Mathf.NegativeInfinity;

        // When alerted by sound (a neighbor's scream), the corn holds the Alert state
        // until this time, even if the player isn't actually nearby. Without this, a
        // sound-triggered alert immediately self-cancels because the player-distance
        // check in Alert sees no player and bounces back to Idle.
        private float alertHoldUntilTime = 0f;

        // Intensity to use for the next scream this corn raises. Defaults to 1.0
        // (a self-triggered scream from the player), but is reduced when the scream
        // is a chain reaction — that's how the chain naturally damps over distance.
        private float pendingScreamIntensity = 1f;

        protected override void OnEnable()
        {
            base.OnEnable();

            if (player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) player = go.transform;
            }
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

            // Sound-alerted: hold the Alert state for a moment regardless of
            // player position. Refreshes if we're already alert and hear another scream.
            alertHoldUntilTime = Time.time + data.alertHoldDuration;

            // Compute the intensity this corn would relay if it screamed.
            float relayedIntensity = evt.Intensity * data.chainIntensityDecay;

            // If the relayed scream would be too quiet, don't propagate the chain
            // any further. The corn still reacts (goes Alert if it was Idle), but
            // it doesn't add a new link to the chain.
            bool willRelay = relayedIntensity >= data.chainIntensityThreshold;

            // Bring an idle corn to Alert at minimum.
            if (CurrentState == CropState.Idle)
            {
                if (logStateTransitions)
                    Debug.Log($"[{InstanceId}] heard {evt.Source.InstanceId} scream", this);
                TransitionTo(CropState.Alert);
            }

            // If the scream is loud enough AND we've recovered from our last scream,
            // re-broadcast it onward through this corn. This is what keeps the chain alive.
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

            float sqrDist = player != null
                ? (player.position - transform.position).sqrMagnitude
                : float.MaxValue;

            switch (state)
            {
                case CropState.Idle:
                    if (sqrDist <= data.awarenessRadius * data.awarenessRadius)
                        TransitionTo(CropState.Alert);
                    break;

                case CropState.Alert:
                    bool playerOutOfRange = sqrDist > data.awarenessRadius * data.awarenessRadius;
                    bool stillListening = Time.time < alertHoldUntilTime;

                    // Player left AND we're done listening for follow-up sounds — relax.
                    if (playerOutOfRange && !stillListening)
                    {
                        TransitionTo(CropState.Idle);
                    }
                    // Player got close AND we've had time to recover from the last scream.
                    else if (sqrDist <= data.reactionRadius * data.reactionRadius
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
                    {
                        // Back to watchful, not idle — the player is probably still there.
                        // The Alert state will route us correctly based on actual distance.
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
                // Reset for next time. If the next reaction is player-triggered
                // (not chain-triggered), it should be a fresh full-intensity scream.
                pendingScreamIntensity = 1f;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || data == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, data.awarenessRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, data.reactionRadius);
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, data.alertBroadcastRadius);
        }
#endif
    }
}