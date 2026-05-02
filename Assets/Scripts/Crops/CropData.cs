using UnityEngine;

[CreateAssetMenu(fileName = "NewCropData", menuName = "Farming/Crop Data")]
public class CropData : ScriptableObject
{
    [Header("Identity")]
    public string cropName;

    [Header("Growth")]
    public float growTime = 5f;

    [Header("Corruption")]
    [Range(0f, 1f)] public float baseCorruptionRate = 0.1f;
    public float infectionResistance = 1f;   // higher = harder to corrupt

    [Header("Spread")]
    public float infectionRadius = 2f;
    public float visualRadius = 12f;
    public float crawlSpeed = 0.4f;

    [Header("Visual")]
    public Color healthyColor = Color.green;
    public Color sickColor = new Color(0.7f, 0.7f, 0.2f);
    public Color corruptedColor = new Color(0.6f, 0f, 0.8f);

    [Header("Yield")]
    public int normalYield = 1;
    public int corruptedYield = 3;
}