using UnityEngine;

namespace EldritchFarm.Crops
{
    /// <summary>
    /// Per-crop-type configuration. Create assets via:
    /// Assets > Create > Eldritch Farm > Crop Data
    ///
    /// Designer-tunable values live here. Behavior logic lives in CropBehavior subclasses.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCropData", menuName = "Eldritch Farm/Crop Data", order = 0)]
    public class CropData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Display name used in UI and debug logs.")]
        public string cropName = "Unnamed Crop";

        [TextArea(2, 4)]
        [Tooltip("Short flavour description — for design reference, not runtime.")]
        public string description;

        [Header("Awareness")]
        [Tooltip("Radius at which the crop notices the player.")]
        public float awarenessRadius = 4f;

        [Tooltip("Radius at which the crop reacts (screams, attacks, flees, etc.).")]
        public float reactionRadius = 2f;

        [Header("Chain Reactions")]
        [Tooltip("Radius at which this crop's reaction alerts neighboring crops.")]
        public float alertBroadcastRadius = 6f;

        [Tooltip("Does this crop alert neighbors when it reacts?")]
        public bool broadcastsAlerts = true;

        [Tooltip("Does this crop respond to neighbors' alerts?")]
        public bool listensToAlerts = true;

        [Header("Timing")]
        [Tooltip("Seconds the crop stays in Reacting state before calming down.")]
        public float reactionDuration = 2f;

        [Tooltip("Seconds the crop must be calm before returning to Idle.")]
        public float cooldownDuration = 1.5f;

        [Tooltip("Minimum seconds between repeat reactions while the player lingers nearby. " +
                 "After reacting, the crop has to 'take a breath' before it can react again.")]
        public float rescreamCooldown = 8f;

        [Tooltip("Seconds the crop stays in Alert after being alerted by sound (e.g. a neighbor's scream), " +
                 "even if the player isn't actually nearby. Like 'wait, what was that?' — gives the alert " +
                 "time to mean something instead of immediately reverting to Idle.")]
        public float alertHoldDuration = 3f;

        [Header("Chain Decay")]
        [Tooltip("Multiplier applied to scream intensity when this crop relays a scream. " +
                 "0.7 means each link in the chain is 70% as loud as the previous one. " +
                 "Lower = chains die faster. Higher = chains travel further.")]
        [Range(0.1f, 1f)]
        public float chainIntensityDecay = 0.7f;

        [Tooltip("Minimum scream intensity required to relay onward. Screams below this " +
                 "are heard (still cause Alert) but don't trigger a re-scream. " +
                 "Higher = chains die sooner.")]
        [Range(0.05f, 1f)]
        public float chainIntensityThreshold = 0.3f;

        [Header("Movement (for crops that move when reacting)")]
        [Tooltip("Speed in units/second when fleeing.")]
        public float fleeSpeed = 3f;

        [Tooltip("How long the crop flees before stopping.")]
        public float fleeDuration = 1.2f;

        [Header("Harvestable Skittishness (for fled crops still on the ground)")]
        [Tooltip("Speed multiplier applied to flees triggered while harvestable. " +
                 "0.5 = half speed. The crop is tired but still nervous.")]
        [Range(0.1f, 1f)]
        public float harvestableFleeSpeedMultiplier = 0.5f;

        [Tooltip("Duration multiplier applied to flees triggered while harvestable. " +
                 "0.5 = scoots half as long.")]
        [Range(0.1f, 1f)]
        public float harvestableFleeDurationMultiplier = 0.5f;

        [Tooltip("Reaction radius for harvestable crops. Usually smaller than the main one — " +
                 "a tomato on the ground won't bolt until the player is right on top of it.")]
        public float harvestableReactionRadius = 1f;

        [Header("Aggression (for crops that attack)")]
        [Tooltip("Seconds the crop spends winding up before striking. This is the player's " +
                 "window to react and dodge — the telegraph is the gameplay.")]
        public float windupDuration = 0.6f;

        [Tooltip("Seconds the strike is active. Brief — the actual impact frame.")]
        public float strikeDuration = 0.15f;

        [Tooltip("Radius of the strike hit-check, measured from the crop's position.")]
        public float strikeRadius = 1.8f;

        [Tooltip("Half-angle of the strike cone in degrees. 45 means the strike covers a 90° arc " +
                 "in front of the crop. Lower = more directional, easier to dodge sideways.")]
        [Range(15f, 180f)]
        public float strikeHalfAngleDegrees = 45f;

        [Tooltip("Force applied to the player on a successful hit (knockback magnitude).")]
        public float strikeKnockback = 8f;

        [Tooltip("Seconds the crop is unable to attack again after striking.")]
        public float attackRecoveryDuration = 1.5f;

        [Tooltip("After an aggressive crop strikes and recovers, it's vulnerable and harvestable " +
                 "for this many seconds. Picks up the pumpkin gameplay loop: bait an attack, dodge, " +
                 "harvest while it's catching its breath.")]
        public float harvestWindowDuration = 3f;

        [Header("Growth")]
        [Tooltip("Seconds from planting to harvestable.")]
        public float growthTimeSeconds = 30f;

        [Header("Harvest")]
        [Tooltip("How many units this crop yields when harvested cleanly.")]
        public int yieldAmount = 1;
    }
}