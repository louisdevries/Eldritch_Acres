using UnityEngine;

namespace EldritchFarm.Crops
{
    /// <summary>
    /// Second concrete crop. Detaches and flees when alarmed — by the player
    /// approaching, a neighbor screaming, or another tomato fleeing nearby.
    /// Once it stops running, it sits on the ground and is harvestable.
    ///
    /// Pressure-tests the framework on:
    ///   - Reacting to multiple event types simultaneously (Scream + Fled)
    ///   - Cross-crop chain reactions (corn scream → tomato flee)
    ///   - Position-changing crop behavior
    ///   - A different state lifecycle (one-way Idle → Harvestable, no looping)
    /// </summary>
    public class SkittishTomato : CropBehavior
    {
        [Header("Refs")]
        [Tooltip("Assign the player's transform (or leave empty to auto-find by 'Player' tag).")]
        [SerializeField] private Transform player;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        // Direction to flee in. Set when transitioning to Reacting.
        private Vector3 fleeDirection;

        // Whether the current flee is a "tired" one (triggered while already harvestable).
        // Determines speed/duration multipliers and where the tomato returns to after fleeing.
        private bool isTiredFlee = false;

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
            CropEventBus.OnFled   += HandleFled;
        }

        protected override void UnsubscribeFromEvents()
        {
            CropEventBus.OnScream -= HandleScream;
            CropEventBus.OnFled   -= HandleFled;
        }

        private void HandleScream(CropScreamEvent evt)
        {
            if (evt.Source == this) return;
            if (!IsWithinRange(evt.Position, evt.Radius)) return;
            // Once grounded, the tomato doesn't react to sounds — only to direct player proximity.
            if (CurrentState != CropState.Idle && CurrentState != CropState.Alert) return;

            // Heard a scream — flee away from the source.
            FleeFrom(evt.Position, tired: false);
        }

        private void HandleFled(CropFledEvent evt)
        {
            if (evt.Source == this) return;
            if (data == null) return;
            if (!IsWithinRange(evt.Position, data.alertBroadcastRadius)) return;
            // Same rule as screams — grounded tomatoes don't get spooked by other tomatoes' movement.
            if (CurrentState != CropState.Idle && CurrentState != CropState.Alert) return;

            FleeFrom(evt.Position, tired: false);
        }

        // -------------------------------------------------------------
        // State machine
        // -------------------------------------------------------------

        protected override void TickState(CropState state)
        {
            if (data == null) return;

            switch (state)
            {
                case CropState.Idle:
                    if (player != null && IsWithinRange(player.position, data.reactionRadius))
                    {
                        FleeFrom(player.position, tired: false);
                    }
                    else if (player != null && IsWithinRange(player.position, data.awarenessRadius))
                    {
                        TransitionTo(CropState.Alert);
                    }
                    break;

                case CropState.Alert:
                    if (player == null) break;
                    if (IsWithinRange(player.position, data.reactionRadius))
                    {
                        FleeFrom(player.position, tired: false);
                    }
                    else if (!IsWithinRange(player.position, data.awarenessRadius))
                    {
                        TransitionTo(CropState.Idle);
                    }
                    break;

                case CropState.Reacting:
                    {
                        // Apply speed multiplier if this is a tired (harvestable) flee.
                        float speed = isTiredFlee
                            ? data.fleeSpeed * data.harvestableFleeSpeedMultiplier
                            : data.fleeSpeed;
                        float duration = isTiredFlee
                            ? data.fleeDuration * data.harvestableFleeDurationMultiplier
                            : data.fleeDuration;

                        Vector3 step = fleeDirection * speed * Time.deltaTime;
                        transform.position += step;

                        if (stateTimer >= duration)
                        {
                            // Always return to Harvestable after fleeing — once the tomato has
                            // detached from its vine, it never goes back to Idle.
                            TransitionTo(CropState.Harvestable);
                        }
                    }
                    break;

                case CropState.Harvestable:
                    // Still skittish — flee a small distance if the player gets right on top of it.
                    // No reaction to sounds anymore (handled in event handlers).
                    if (player != null && IsWithinRange(player.position, data.harvestableReactionRadius))
                    {
                        FleeFrom(player.position, tired: true);
                    }
                    break;
            }
        }

        protected override void OnStateEnter(CropState state)
        {
            if (state == CropState.Reacting)
            {
                // Announce the flee so other tomatoes (and future crops) can chain off it.
                RaiseFled(fleeDirection);
            }
        }

        // -------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------

        /// <summary>
        /// Set flee direction (away from the threat) and transition to Reacting.
        /// `tired` indicates this is a follow-up flee after the tomato is already
        /// harvestable — the Reacting state will apply reduced speed/duration.
        /// </summary>
        private void FleeFrom(Vector3 threatPosition, bool tired)
        {
            Vector3 away = transform.position - threatPosition;
            away.y = 0f; // keep movement on the ground plane

            fleeDirection = away.sqrMagnitude > 0.001f
                ? away.normalized
                : Random.insideUnitSphere; // edge case: threat exactly on top — pick any direction

            // Re-zero Y after Random.insideUnitSphere
            fleeDirection.y = 0f;
            if (fleeDirection.sqrMagnitude < 0.001f)
                fleeDirection = Vector3.forward;
            else
                fleeDirection = fleeDirection.normalized;

            isTiredFlee = tired;
            TransitionTo(CropState.Reacting);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || data == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, data.awarenessRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, data.reactionRadius);
            Gizmos.color = new Color(0.8f, 0.4f, 1f, 0.5f); // purple — tomato hearing range
            Gizmos.DrawWireSphere(transform.position, data.alertBroadcastRadius);

            // Show flee direction while running
            if (Application.isPlaying && CurrentState == CropState.Reacting)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, transform.position + fleeDirection * 2f);
            }
        }
#endif
    }
}