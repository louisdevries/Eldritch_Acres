using UnityEngine;

public class CorruptionWorldController : MonoBehaviour
{
    public PlayerInteraction player;

    void Update()
    {
        if (player == null) return;

        float c = player.corruption;

        // GLOBAL FOG SETTINGS
        RenderSettings.fog = true;

        RenderSettings.fogColor = Color.Lerp(
            new Color(0.8f, 0.8f, 0.8f),
            new Color(0.3f, 0.6f, 0.3f),
            c
        );

        RenderSettings.fogDensity = Mathf.Lerp(0.01f, 0.03f, c);

        // GLOBAL LIGHT MOOD
        RenderSettings.ambientLight = Color.Lerp(
            Color.white,
            new Color(0.4f, 0.5f, 0.4f),
            c
        );

        // CAMERA MOOD
        if (Camera.main != null)
        {
            Camera.main.backgroundColor = Color.Lerp(
                new Color(0.6f, 0.7f, 0.6f),
                new Color(0.2f, 0.2f, 0.25f),
                c
            );
        }
    }
}