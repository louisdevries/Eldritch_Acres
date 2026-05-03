using UnityEngine;

namespace EldritchFarm.Crops
{
    /// <summary>
    /// Visualizes growth progress by scaling a target transform from a seedling size
    /// up to full size as the crop matures. Sits alongside CropStateVisualizer —
    /// state visualizer handles colors (idle/alert/striking/etc.), this handles the
    /// "is it ready yet?" question.
    ///
    /// Scales a child transform, never the root, so gameplay-relevant radii and
    /// colliders stay constant. Designed to be replaceable later with real model
    /// swapping or shader-driven growth — for prototyping, scaling is enough.
    /// </summary>
    [RequireComponent(typeof(CropBehavior))]
    public class CropGrowthVisualizer : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Transform to scale. Should be a child of this GameObject — usually the visual mesh. " +
                 "If empty, uses the first child found.")]
        [SerializeField] private Transform targetTransform;

        [Header("Scale")]
        [Tooltip("Scale of the crop when freshly planted (as a multiplier of the target's original scale). " +
                 "0.2 = 20% of full size, like a sprout.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float seedlingScale = 0.2f;

        [Tooltip("How fast the visual catches up to the target scale. " +
                 "Higher = snappier; lower = more gradual lerp toward the size.")]
        [SerializeField] private float lerpSpeed = 4f;

        private CropBehavior crop;
        private Vector3 fullScale;

        private void Awake()
        {
            crop = GetComponent<CropBehavior>();

            if (targetTransform == null)
            {
                // Pick the first child as a sensible default
                if (transform.childCount > 0)
                    targetTransform = transform.GetChild(0);
            }

            if (targetTransform != null)
            {
                fullScale = targetTransform.localScale;
                // Start at seedling size so the crop pops in small.
                targetTransform.localScale = fullScale * seedlingScale;
            }
        }

        private void Update()
        {
            if (targetTransform == null || crop == null || crop.Data == null) return;

            // Compute target scale from age. We use the public IsMature property
            // for binary state, but for the visual we want a smooth ramp from age 0
            // up to growthTimeSeconds. Read the field via reflection? No — better,
            // just clamp the ratio ourselves.
            float ratio = Mathf.Clamp01(GetGrowthProgress());

            float currentScale = Mathf.Lerp(seedlingScale, 1f, ratio);
            Vector3 desired = fullScale * currentScale;

            targetTransform.localScale = Vector3.Lerp(
                targetTransform.localScale,
                desired,
                Time.deltaTime * lerpSpeed
            );
        }

        /// <summary>
        /// Linear 0..1 progress from "just planted" to "mature".
        /// </summary>
        private float GetGrowthProgress()
        {
            return crop.Age / crop.Data.growthTimeSeconds;
        }
    }
}
