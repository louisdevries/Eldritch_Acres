using UnityEngine;
using EldritchFarm.Player;

namespace EldritchFarm.Crops
{
    /// <summary>
    /// Third concrete crop, and the first aggressive one. Sits passively until the
    /// player gets close, then telegraphs a strike (windup), commits to attacking
    /// the player's position at that moment, swings, and recovers.
    ///
    /// The gameplay is in the telegraph: player has windupDuration seconds to read
    /// the tell and dodge. Movement during windup is the player's only defence.
    ///
    /// Pressure-tests the framework on:
    ///   - A new shared state (Striking) on the central enum
    ///   - Timing-based player interaction (commit-and-swing, not lock-on)
    ///   - Cross-system communication (PlayerKnockback)
    ///   - A crop that broadcasts events but subscribes to none (proactive, not reactive)
    /// </summary>
    public class AggressivePumpkin : CropBehavior
    {
        [Header("Refs")]
        [Tooltip("Assign the player's transform (or leave empty to auto-find by 'Player' tag).")]
        [SerializeField] private Transform player;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        // Where the player was when windup started. Strike checks against this point,
        // not the player's live position — that's what makes dodging meaningful.
        private Vector3 strikeTargetPosition;

        // Direction the pumpkin is facing for this attack — set when entering Alert
        // and locked in. Used for both visual tells (gizmo) and the strike cone check.
        private Vector3 facingDirection = Vector3.forward;

        // When the pumpkin last finished a strike. Gates the next attack so the
        // pumpkin doesn't chain strikes back-to-back.
        private float lastStrikeEndTime = Mathf.NegativeInfinity;

        // Cached knockback receiver on the player.
        private PlayerKnockback playerKnockback;

        protected override void OnEnable()
        {
            base.OnEnable();

            if (player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null)
                {
                    player = go.transform;
                    playerKnockback = go.GetComponent<PlayerKnockback>();
                }
            }
            else
            {
                playerKnockback = player.GetComponent<PlayerKnockback>();
            }
        }

        // Pumpkin is proactive — it doesn't subscribe to crop events. It only watches
        // the player. This is a meaningful character distinction; comment left for clarity.
        protected override void SubscribeToEvents()   { }
        protected override void UnsubscribeFromEvents() { }

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
                    if (sqrDist <= data.awarenessRadius * data.awarenessRadius
                        && Time.time - lastStrikeEndTime >= data.attackRecoveryDuration)
                    {
                        FacePlayer();
                        TransitionTo(CropState.Alert);
                    }
                    break;

                case CropState.Alert:
                    if (sqrDist > data.awarenessRadius * data.awarenessRadius)
                    {
                        TransitionTo(CropState.Idle);
                    }
                    else if (sqrDist <= data.reactionRadius * data.reactionRadius)
                    {
                        FacePlayer();
                        TransitionTo(CropState.Reacting); // begin windup
                    }
                    else
                    {
                        // Track the player while alert so the windup faces the right way when it starts.
                        FacePlayer();
                    }
                    break;

                case CropState.Reacting:
                    // Windup — locked in, telegraph the swing.
                    if (stateTimer >= data.windupDuration)
                    {
                        // Commit to a strike at the player's current position. After this point,
                        // moving the player no longer changes whether the swing connects.
                        strikeTargetPosition = player != null ? player.position : transform.position;
                        TransitionTo(CropState.Striking);
                    }
                    break;

                case CropState.Striking:
                    // Strike is active for the duration. The hit is resolved on enter (see OnStateEnter)
                    // so the player is checked at the moment the strike commits, not every frame.
                    if (stateTimer >= data.strikeDuration)
                    {
                        TransitionTo(CropState.Distressed);
                    }
                    break;

                case CropState.Distressed:
                    if (stateTimer >= data.attackRecoveryDuration)
                    {
                        lastStrikeEndTime = Time.time;
                        TransitionTo(CropState.Idle);
                    }
                    break;
            }
        }

        protected override void OnStateEnter(CropState state)
        {
            switch (state)
            {
                case CropState.Reacting:
                    // Windup begins — pumpkin is committed but the player still has time to dodge.
                    break;

                case CropState.Striking:
                    ResolveStrike();
                    // Announce the strike for any future systems / nearby crops that care.
                    RaiseAttack(facingDirection);
                    break;
            }
        }

        // -------------------------------------------------------------
        // Strike resolution
        // -------------------------------------------------------------

        /// <summary>
        /// Check if the player's locked-in target position falls within the strike cone,
        /// and apply knockback if so. This runs once when entering Striking — the swing
        /// either hits or misses based on where the player was when the windup ended.
        /// </summary>
        private void ResolveStrike()
        {
            if (player == null || data == null) return;

            Vector3 toTarget = strikeTargetPosition - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            // Out of range → swing misses.
            if (distance > data.strikeRadius) return;
            if (distance < 0.001f) return; // sanity: player on top of pumpkin, treat as no direction

            // Check angle vs facing direction.
            Vector3 toTargetDir = toTarget / distance;
            float angle = Vector3.Angle(facingDirection, toTargetDir);
            if (angle > data.strikeHalfAngleDegrees) return;

            // Hit confirmed. Knockback the player using the *current* player position
            // (not the locked target) so the push direction feels right even if they tried to dodge.
            Debug.Log($"[{InstanceId}] HIT! distance={distance:F2} angle={angle:F1}");
            if (playerKnockback != null)
            {
                Vector3 knockbackDir = (player.position - transform.position);
                knockbackDir.y = 0f;
                if (knockbackDir.sqrMagnitude > 0.0001f)
                {
                    playerKnockback.Apply(knockbackDir.normalized, data.strikeKnockback);
                }
            }
            else if (logStateTransitions)
            {
                Debug.LogWarning($"[{InstanceId}] hit player but PlayerKnockback component not found.", this);
            }
        }

        // -------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------

        /// <summary>
        /// Update facing direction toward the player. Called continuously during Alert
        /// (so the windup starts pointed correctly), but locked once windup begins.
        /// </summary>
        private void FacePlayer()
        {
            if (player == null) return;
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.0001f)
            {
                facingDirection = toPlayer.normalized;
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

            // Strike cone — show the threat zone in front of the pumpkin.
            Vector3 origin = transform.position;
            Vector3 fwd = Application.isPlaying ? facingDirection : transform.forward;

            float halfAngle = data.strikeHalfAngleDegrees;
            Vector3 left  = Quaternion.Euler(0f, -halfAngle, 0f) * fwd;
            Vector3 right = Quaternion.Euler(0f,  halfAngle, 0f) * fwd;

            Gizmos.color = Application.isPlaying && CurrentState == CropState.Reacting
                ? Color.magenta // bright while telegraphing
                : new Color(1f, 0f, 0f, 0.3f);

            Gizmos.DrawLine(origin, origin + left  * data.strikeRadius);
            Gizmos.DrawLine(origin, origin + right * data.strikeRadius);
            Gizmos.DrawLine(origin + left  * data.strikeRadius,
                            origin + right * data.strikeRadius);

            // Show locked-in target during the strike.
            if (Application.isPlaying && CurrentState == CropState.Striking)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(strikeTargetPosition, 0.3f);
            }
        }
#endif
    }
}
