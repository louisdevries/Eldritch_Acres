using UnityEngine;

namespace EldritchFarm.Player
{
    /// <summary>
    /// Lightweight knockback receiver. Attach to the player so aggressive crops
    /// can push them around without depending on the specific player controller.
    ///
    /// Designed to cooperate with a CharacterController-based movement script.
    /// Rather than fighting the controller by writing to transform.position,
    /// this exposes the current knockback velocity which the movement script
    /// reads and applies through its own controller.Move() call. This way
    /// movement and knockback combine cleanly into a single CharacterController
    /// step per frame, which the physics engine handles correctly.
    ///
    /// Optional: while a knockback is active, IsStunned will be true for a
    /// short window. Movement scripts can ignore input during this period
    /// for snappier "you got hit" game-feel.
    /// </summary>
    public class PlayerKnockback : MonoBehaviour
    {
        [Header("Decay")]
        [Tooltip("How quickly the knockback velocity fades. Higher = snappier.")]
        [SerializeField] private float decayRate = 6f;

        [Tooltip("Velocity below this magnitude is treated as zero. Stops jittery motion at the tail end.")]
        [SerializeField] private float minVelocityThreshold = 0.1f;

        [Header("Stun")]
        [Tooltip("How long after a knockback the player input should be ignored. " +
                 "0 = no stun. ~0.2s feels punchy without being frustrating.")]
        [SerializeField] private float stunDuration = 0.25f;

        private Vector3 currentVelocity;
        private float stunUntilTime;

        /// <summary>
        /// Current knockback velocity. Read this from your movement script and
        /// add it to the controller's per-frame Move() vector. Decays automatically.
        /// </summary>
        public Vector3 CurrentVelocity => currentVelocity;

        /// <summary>
        /// True if the player should ignore input for now (just got hit).
        /// Movement scripts can check this and skip input handling while stunned.
        /// </summary>
        public bool IsStunned => Time.time < stunUntilTime;

        /// <summary>
        /// Apply an instantaneous knockback. `direction` should be normalized; `magnitude`
        /// is in units/second of initial velocity. Direction is forced to ground plane (Y=0)
        /// to keep the player grounded.
        /// </summary>
        public void Apply(Vector3 direction, float magnitude)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;

            currentVelocity = direction.normalized * magnitude;
            stunUntilTime = Time.time + stunDuration;
        }

        private void Update()
        {
            // Decay over time. Movement script reads the velocity each frame and
            // applies it through controller.Move() — we don't write to transform here.
            if (currentVelocity.sqrMagnitude < minVelocityThreshold * minVelocityThreshold)
            {
                currentVelocity = Vector3.zero;
                return;
            }

            currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, Time.deltaTime * decayRate);
        }
    }
}