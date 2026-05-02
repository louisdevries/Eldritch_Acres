using UnityEngine;

public class FieldInfectionSender : MonoBehaviour
{
    void Update()
    {
        Crop[] crops = FindObjectsByType<Crop>(FindObjectsInactive.Exclude);

        Vector3 bestPosition = Vector3.zero;
        float bestStrength = 0f;
        bool found = false;

        foreach (var crop in crops)
        {
            if (crop.state != Crop.CropState.Corrupted) continue;

            if (crop.infectionStrength > bestStrength)
            {
                bestStrength = crop.infectionStrength;
                bestPosition = crop.transform.position;
                found = true;
            }
        }

        if (found)
        {
            Shader.SetGlobalVector("_CropPosition", bestPosition);
            Shader.SetGlobalFloat("_CorruptionStrength", bestStrength);
        }
        else
        {
            // No corruption → reset
            Shader.SetGlobalFloat("_CorruptionStrength", 0f);
        }
    }
}