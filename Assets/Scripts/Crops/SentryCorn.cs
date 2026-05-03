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

            float sqrDist = player != null
                ? (player.position - transform.position).sqrMagnitude
                : float.MaxValue;

            switch (state)
            {
                case CropState.Idle:
                    // Player approaches → go alert
                    if (sqrDist <= data.awarenessRadius * data.awarenessRadius)
                    {
                        TransitionTo(CropState.Alert);
                    }
                    // No player nearby AND mature AND has been calm long enough → ripen
                    else if (IsMature && stateTimer >= data.cooldownDuration)
                    {
                        TransitionTo(CropState.Harvestable);
                    }
                    break;

                case CropState.Alert:
                    bool playerOutOfRange = sqrDist > data.awarenessRadius * data.awarenessRadius;
                    bool stillListening = Time.time < alertHoldUntilTime;

                    if (playerOutOfRange && !stillListening)
                    {
                        TransitionTo(CropState.Idle);
                    }
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
                        TransitionTo(CropState.Alert); // back to watchful, may re-trigger
                    break;

                case CropState.Harvestable:
                    // A harvestable corn still reacts to the player — getting close to grab it
                    // will startle it back into the alert/scream cycle. The growth timer doesn't
                    // reset, so the corn will return to Harvestable quickly after calming.
                    if (sqrDist <= data.awarenessRadius * data.awarenessRadius)
                    {
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