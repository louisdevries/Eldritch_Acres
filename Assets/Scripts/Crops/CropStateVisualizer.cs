using UnityEngine;

namespace EldritchFarm.Crops
{
    /// <summary>
    /// Visualizes the crop's current state by tinting a target renderer.
    /// Subscribes to CropEventBus.OnCropStateChanged and reacts only to its own crop.
    ///
    /// This is intentionally a separate component from CropBehavior so:
    ///   - All crops share one visualization system
    ///   - Visuals can be swapped (debug colors → shader effects → particles)
    ///     without touching crop logic
    ///   - You can disable it for release builds and keep it for prototyping
    /// </summary>
    [RequireComponent(typeof(CropBehavior))]
    public class CropStateVisualizer : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Renderer to tint. If empty, uses the first child renderer found.")]
        [SerializeField] private Renderer targetRenderer;

        [Header("State Colors")]
        [SerializeField] private Color idleColor       = Color.white;
        [SerializeField] private Color alertColor      = Color.yellow;
        [SerializeField] private Color reactingColor   = Color.red;
        [SerializeField] private Color strikingColor   = new Color(1f, 0f, 1f); // magenta
        [SerializeField] private Color distressedColor = new Color(1f, 0.5f, 0f); // orange
        [SerializeField] private Color harvestableColor = Color.green;
        [SerializeField] private Color spentColor      = Color.gray;

        [Header("Animation")]
        [Tooltip("How fast the color blends to the target. Higher = snappier.")]
        [SerializeField] private float blendSpeed = 8f;

        private CropBehavior crop;
        private Material runtimeMaterial;
        private Color currentColor;
        private Color targetColor;

        private void Awake()
        {
            crop = GetComponent<CropBehavior>();

            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>();
            }

            if (targetRenderer != null)
            {
                // Instance the material so each crop has its own — otherwise
                // tinting one Sentry Corn tints all of them sharing the asset.
                runtimeMaterial = targetRenderer.material;
            }
        }

        private void OnEnable()
        {
            CropEventBus.OnCropStateChanged += HandleStateChanged;
            // Apply current state immediately on enable
            currentColor = ColorFor(crop.CurrentState);
            targetColor = currentColor;
            ApplyColor();
        }

        private void OnDisable()
        {
            CropEventBus.OnCropStateChanged -= HandleStateChanged;
        }

        private void Update()
        {
            if (currentColor != targetColor)
            {
                currentColor = Color.Lerp(currentColor, targetColor, Time.deltaTime * blendSpeed);
                ApplyColor();
            }
        }

        private void HandleStateChanged(CropBehavior source, CropState from, CropState to)
        {
            if (source != crop) return;
            targetColor = ColorFor(to);
        }

        private Color ColorFor(CropState state)
        {
            switch (state)
            {
                case CropState.Alert:       return alertColor;
                case CropState.Reacting:    return reactingColor;
                case CropState.Striking:    return strikingColor;
                case CropState.Distressed:  return distressedColor;
                case CropState.Harvestable: return harvestableColor;
                case CropState.Spent:       return spentColor;
                case CropState.Idle:
                default:                    return idleColor;
            }
        }

        private void ApplyColor()
        {
            if (runtimeMaterial == null) return;

            if (runtimeMaterial.HasProperty("_BaseColor"))
                runtimeMaterial.SetColor("_BaseColor", currentColor);
            else if (runtimeMaterial.HasProperty("_Color"))
                runtimeMaterial.color = currentColor;
        }
    }
}