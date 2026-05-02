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

            // Sound-alerted: hold the Alert state for a moment regardless of
            // player position. Refreshes if we're already alert and hear another scream.
            if (data != null)
                alertHoldUntilTime = Time.time + data.alertHoldDuration;

            if (CurrentState == CropState.Idle)
            {
                if (logStateTransitions)
                    Debug.Log($"[{InstanceId}] heard {evt.Source.InstanceId} scream", this);
                TransitionTo(CropState.Alert);
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
                RaiseScream(intensity: 1f);
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