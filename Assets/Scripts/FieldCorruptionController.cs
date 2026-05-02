using UnityEngine;

public class FieldCorruptionController : MonoBehaviour
{
    public Renderer fieldRenderer;

    public float influenceRadius = 6f;
    public float maxStrength = 1f;

    public float smoothSpeed = 2f;

    private float currentInfluence = 0f;

    void Start()
    {
        if (fieldRenderer != null)
        {
            fieldRenderer.material = new Material(fieldRenderer.material);
            fieldRenderer.material.EnableKeyword("_EMISSION");
        }
    }

    void Update()
    {
        UpdateFieldVisual();
    }

    void UpdateFieldVisual()
    {
        if (fieldRenderer == null) return;

        Crop[] crops = FindObjectsByType<Crop>(FindObjectsInactive.Exclude);

        float totalInfluence = 0f;
        float weightSum = 0f;

        foreach (Crop crop in crops)
        {
            if (crop.state != Crop.CropState.Corrupted) continue;

            float dist = Vector3.Distance(transform.position, crop.transform.position);

            if (dist < influenceRadius)
            {
                float falloff = 1f - (dist / influenceRadius);

                totalInfluence += falloff * crop.infectionStrength;
                weightSum += falloff;
            }
        }

        float targetInfluence = (weightSum > 0f)
            ? totalInfluence / weightSum
            : 0f;

        targetInfluence = Mathf.Clamp01(targetInfluence * maxStrength);

        // Smooth transition
        currentInfluence = Mathf.Lerp(
            currentInfluence,
            targetInfluence,
            Time.deltaTime * smoothSpeed
        );

        // ---------------- VEIN EFFECT ----------------

        // Fake UV coordinates across world space
        float scale = 1.5f; // controls how many veins you see

        float x = transform.position.x;
        float z = transform.position.z;

        // Offset over time to animate
        float timeOffset = Time.time * 0.5f;

        // Multi-layer pattern (THIS is key)
        float veinPattern =
            Mathf.Sin((x * scale) + timeOffset) *
            Mathf.Cos((z * scale) + timeOffset);

        // Add second layer for complexity
        veinPattern +=
            Mathf.Sin((x * scale * 2f) - timeOffset * 1.5f) *
            Mathf.Cos((z * scale * 2f) - timeOffset * 1.5f);

        // Normalize
        veinPattern = Mathf.Abs(veinPattern * 0.5f);

        // Sharpen into veins
        veinPattern = Mathf.Pow(veinPattern, 5f);

        // Mask by corruption strength
        float veinMask = veinPattern * currentInfluence;

        // ---------------- FINAL VISUAL ----------------

        Color baseTint = Color.Lerp(
            Color.black,
            new Color(0.1f, 0f, 0.15f),
            currentInfluence
        );

        Color veinColor = new Color(0.6f, 0f, 0.8f) * veinMask * 2f;

        Color finalEmission = baseTint + veinColor;

        fieldRenderer.material.SetColor("_EmissionColor", finalEmission);
    }
}