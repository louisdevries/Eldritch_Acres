using UnityEngine;

public class FarmTile : MonoBehaviour
{
    private Vector3 baseScale;
    private Renderer rend;

    private Color targetColor;
    private Color currentColor;

    [Range(0f, 1f)]
    public float corruption = 0f;

    private float targetCorruption = 0f;

    void Awake()
    {
        baseScale = transform.localScale;
        rend = GetComponent<Renderer>();

        if (rend != null)
        {
            // Initialize shader values instead of reading material.color
            currentColor = Color.white;
            rend.material.SetColor("_BaseColor", currentColor);
            rend.material.SetFloat("_Corruption", 0f);
        }
    }

    // Called by FarmGrid when tile state changes
    public void SetVisual(Color color, float corruptionValue = 0f)
    {
        targetColor = color;
        targetCorruption = corruptionValue;

        // pop animation
        transform.localScale = baseScale * 1.2f;
    }

    void Update()
    {
        // -----------------------------
        // SCALE ANIMATION
        // -----------------------------
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            baseScale,
            Time.deltaTime * 8f
        );

        if (rend == null) return;

        // -----------------------------
        // SMOOTH COLOR (BASE COLOR)
        // -----------------------------
        currentColor = Color.Lerp(currentColor, targetColor, Time.deltaTime * 6f);
        rend.material.SetColor("_BaseColor", currentColor);

        // -----------------------------
        // SMOOTH CORRUPTION
        // -----------------------------
        corruption = Mathf.Lerp(corruption, targetCorruption, Time.deltaTime * 6f);
        rend.material.SetFloat("_Corruption", corruption);

        // -----------------------------
        // OPTIONAL: WAVE EFFECT (if used in shader)
        // -----------------------------
        if (rend.material.HasProperty("_WaveSpeed"))
        {
            float waveTime = Time.time;

            rend.material.SetFloat("_WaveSpeed", 1f);
            rend.material.SetFloat("_WaveStrength", corruption * 0.2f);
        }
    }
}