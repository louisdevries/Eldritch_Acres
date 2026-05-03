using UnityEngine;
using EldritchFarm.Player;

namespace EldritchFarm.Crops
{
    /// <summary>
    /// Sits passively until the player gets close, then telegraphs a strike, commits
    /// to attacking the player's position at that moment, swings, and recovers.
    ///
    /// The gameplay is in the telegraph: player has windupDuration seconds to read
    /// the tell and dodge. Movement during windup is the player's only defence.
    ///
    /// Harvestable while exhausted: a mature pumpkin that finishes its post-strike
    /// recovery enters a Harvestable window — the player can grab it during this
    /// vulnerable period. Bait the attack → dodge → harvest while it catches its
    /// breath. If not harvested in time, the pumpkin returns to Idle and is ready
    /// to attack again.
    /// </summary>
    public class AggressivePumpkin : CropBehavior
    {
        [Header("Refs")]
        [Tooltip("Assign the player's transform (or leave empty to auto-find by 'Player' tag).")]
        [SerializeField] private Transform player;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        private Vector3 strikeTargetPosition;
        private Vector3 facingDirection = Vector3.forward;
        private float lastStrikeEndTime = Mathf.NegativeInfinity;
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
                        FacePlayer();
                    }
                    break;

                case CropState.Reacting:
                    // Windup — locked in, telegraph the swing.
                    if (stateTimer >= data.windupDuration)
                    {
                        strikeTargetPosition = player != null ? player.position : transform.position;
                        TransitionTo(CropState.Striking);
                    }
                    break;

                case CropState.Striking:
                    if (stateTimer >= data.strikeDuration)
                    {
                        TransitionTo(CropState.Distressed);
                    }
                    break;

                case CropState.Distressed:
                    if (stateTimer >= data.attackRecoveryDuration)
                    {
                        lastStrikeEndTime = Time.time;
                        // Mature pumpkin? Player gets a harvest window. Otherwise back to Idle
                        // and ready to attack again.
                        if (IsMature)
                            TransitionTo(CropState.Harvestable);
                        else
                            TransitionTo(CropState.Idle);
                    }
                    break;

                case CropState.Harvestable:
                    // Vulnerable harvest window. If not picked in time, recovers and goes
                    // back on guard.
                    if (stateTimer >= data.harvestWindowDuration)
                    {
                        TransitionTo(CropState.Idle);
                    }
                    break;
            }
        }

        protected override void OnStateEnter(CropState state)
        {
            switch (state)
            {
                case CropState.Striking:
                    ResolveStrike();
                    RaiseAttack(facingDirection);
                    break;
            }
        }

        // -------------------------------------------------------------
        // Strike resolution
        // -------------------------------------------------------------

        private void ResolveStrike()
        {
            if (player == null || data == null) return;

            Vector3 toTarget = strikeTargetPosition - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            if (distance > data.strikeRadius) return;
            if (distance < 0.001f) return;

            Vector3 toTargetDir = toTarget / distance;
            float angle = Vector3.Angle(facingDirection, toTargetDir);
            if (angle > data.strikeHalfAngleDegrees) return;

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

            Vector3 origin = transform.position;
            Vector3 fwd = Application.isPlaying ? facingDirection : transform.forward;

            float halfAngle = data.strikeHalfAngleDegrees;
            Vector3 left  = Quaternion.Euler(0f, -halfAngle, 0f) * fwd;
            Vector3 right = Quaternion.Euler(0f,  halfAngle, 0f) * fwd;

            Gizmos.color = Application.isPlaying && CurrentState == CropState.Reacting
                ? Color.magenta
                : new Color(1f, 0f, 0f, 0.3f);

            Gizmos.DrawLine(origin, origin + left  * data.strikeRadius);
            Gizmos.DrawLine(origin, origin + right * data.strikeRadius);
            Gizmos.DrawLine(origin + left  * data.strikeRadius,
                            origin + right * data.strikeRadius);

            if (Application.isPlaying && CurrentState == CropState.Striking)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(strikeTargetPosition, 0.3f);
            }
        }
#endif
    }
}