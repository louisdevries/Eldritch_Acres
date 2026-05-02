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

        [Header("Growth")]
        [Tooltip("Seconds from planting to harvestable.")]
        public float growthTimeSeconds = 30f;

        [Header("Harvest")]
        [Tooltip("How many units this crop yields when harvested cleanly.")]
        public int yieldAmount = 1;
    }
}