using UnityEngine;
using EldritchFarm.Crops;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Crop System")]
    public GameObject[] cropPrefabs;
    private int currentCropIndex = 0;
    public GameObject ghostPrefab;

    public float interactDistance = 2f;

    public FarmGrid farmGrid;

    int cropCount = 0;

    private GameObject ghostInstance;
    private Renderer ghostRenderer;
    private Vector3 ghostBaseScale;

    // Cached "do we have everything to run the ghost system?" check.
    // Lets the script tolerate missing references without throwing, useful while
    // the scene is being set up incrementally.
    private bool HasGhost => ghostInstance != null && ghostRenderer != null;
    private bool HasCropPrefabs => cropPrefabs != null && cropPrefabs.Length > 0;
    private bool HasFarmGrid => farmGrid != null;

    void Start()
    {
        if (ghostPrefab != null)
        {
            ghostInstance = Instantiate(ghostPrefab);
            ghostRenderer = ghostInstance.GetComponentInChildren<Renderer>();
            ghostBaseScale = ghostInstance.transform.localScale;

            // Position ghost on first frame so it doesn't pop in at origin.
            if (HasFarmGrid)
            {
                Vector3 forwardPosition = transform.position + transform.forward * interactDistance;
                forwardPosition.y = 0;

                Vector2Int gridPos = farmGrid.WorldToGrid(forwardPosition);
                Vector3 snappedPos = farmGrid.GridToWorld(gridPos);

                ghostInstance.transform.position = snappedPos;
                ghostInstance.transform.rotation = Quaternion.identity;
            }

            UpdateGhostVisual();
        }
        else
        {
            Debug.LogWarning("[PlayerInteraction] No ghostPrefab assigned — ghost preview disabled.");
        }

        if (!HasCropPrefabs)
            Debug.LogWarning("[PlayerInteraction] No cropPrefabs assigned — planting disabled.");

        if (!HasFarmGrid)
            Debug.LogWarning("[PlayerInteraction] No farmGrid assigned — planting/tilling disabled.");
    }

    void Update()
    {
        UpdateGhost();

        if (Input.GetKeyDown(KeyCode.E))
            TryPlant();

        if (Input.GetKeyDown(KeyCode.F))
            TryTill();

        if (Input.GetKeyDown(KeyCode.Q))
            CycleCrop();
    }

    // -----------------------------
    // GHOST SYSTEM
    // -----------------------------
    void UpdateGhost()
    {
        if (!HasGhost || !HasFarmGrid) return;

        Vector3 forwardPosition = transform.position + transform.forward * interactDistance;
        forwardPosition.y = 0;

        Vector2Int gridPos = farmGrid.WorldToGrid(forwardPosition);
        Vector3 snappedPos = farmGrid.GridToWorld(gridPos);

        ghostInstance.transform.position = snappedPos;
        ghostInstance.transform.rotation = Quaternion.identity;

        bool canPlant = CanPlantAt(gridPos);
        var state = farmGrid.GetTile(gridPos);

        // Color feedback
        Color color;
        if (state == null)
            color = new Color(1f, 0.5f, 0f, 0.5f); // orange = untilled
        else if (state == FarmGrid.TileState.Occupied)
            color = new Color(1f, 0f, 0f, 0.5f);   // red
        else if (canPlant)
            color = new Color(0f, 1f, 0f, 0.5f);   // green
        else
            color = new Color(1f, 0f, 0f, 0.5f);

        ghostRenderer.material.color = color;

        // Smooth scaling
        float targetScale = canPlant ? 1f : 0.9f;
        ghostInstance.transform.localScale = Vector3.Lerp(
            ghostInstance.transform.localScale,
            ghostBaseScale * targetScale,
            Time.deltaTime * 10f
        );
    }

    // -----------------------------
    // INTERACTION
    // -----------------------------
    void TryPlant()
    {
        Vector3 forwardPosition = transform.position + transform.forward * interactDistance;
        forwardPosition.y = 0;

        // Harvest first — works even without farmGrid or cropPrefabs configured.
        Collider[] colliders = Physics.OverlapSphere(forwardPosition, 1f);

        foreach (Collider col in colliders)
        {
            CropBehavior crop = col.GetComponentInParent<CropBehavior>();

            if (crop != null && crop.CanHarvest())
            {
                cropCount += crop.Harvest();

                if (HasFarmGrid)
                {
                    Vector2Int tile = farmGrid.WorldToGrid(crop.transform.position);
                    farmGrid.SetTile(tile, FarmGrid.TileState.Tilled);
                }

                Destroy(crop.gameObject);
                return;
            }
        }

        // Planting requires both farmGrid and a crop prefab.
        if (!HasFarmGrid || !HasCropPrefabs) return;

        Vector2Int gridPos = farmGrid.WorldToGrid(forwardPosition);
        Vector3 snappedPos = farmGrid.GridToWorld(gridPos);

        if (CanPlantAt(gridPos))
        {
            GameObject prefab = cropPrefabs[currentCropIndex];
            if (prefab == null) return;

            Instantiate(prefab, snappedPos, Quaternion.identity);
            farmGrid.SetTile(gridPos, FarmGrid.TileState.Occupied);
        }
    }

    void TryTill()
    {
        if (!HasFarmGrid) return;

        Vector3 pos = transform.position + transform.forward * interactDistance;
        Vector2Int tile = farmGrid.WorldToGrid(pos);

        if (farmGrid.GetTile(tile) == null)
            farmGrid.SetTile(tile, FarmGrid.TileState.Tilled);
    }

    bool CanPlantAt(Vector2Int tile)
    {
        if (!HasFarmGrid) return false;

        var state = farmGrid.GetTile(tile);
        if (state != FarmGrid.TileState.Tilled) return false;

        // Spacing rule
        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                Vector2Int check = tile + new Vector2Int(x, z);
                if (farmGrid.GetTile(check) == FarmGrid.TileState.Occupied)
                    return false;
            }
        }

        return true;
    }

    void CycleCrop()
    {
        if (!HasCropPrefabs) return;

        currentCropIndex = (currentCropIndex + 1) % cropPrefabs.Length;
        UpdateGhostVisual();
    }

    void UpdateGhostVisual()
    {
        if (!HasGhost || !HasCropPrefabs) return;

        // Destroy old mesh children
        foreach (Transform child in ghostInstance.transform)
            Destroy(child.gameObject);

        GameObject prefab = cropPrefabs[currentCropIndex];
        if (prefab == null) return;

        // Copy visual from crop prefab
        GameObject visual = Instantiate(prefab, ghostInstance.transform);

        // Strip gameplay scripts so the ghost doesn't accidentally run AI
        foreach (var comp in visual.GetComponentsInChildren<MonoBehaviour>())
            Destroy(comp);

        // Strip colliders so the ghost doesn't get harvested or block the player
        foreach (var c in visual.GetComponentsInChildren<Collider>())
            Destroy(c);

        // Make transparent
        Renderer[] rends = visual.GetComponentsInChildren<Renderer>();
        foreach (var r in rends)
        {
            foreach (var mat in r.materials)
            {
                if (mat.HasProperty("_Color"))
                {
                    Color c = mat.color;
                    c.a = 0.5f;
                    mat.color = c;
                }
                else if (mat.HasProperty("_BaseColor"))
                {
                    Color c = mat.GetColor("_BaseColor");
                    c.a = 0.5f;
                    mat.SetColor("_BaseColor", c);
                }
            }
        }
    }

    void OnGUI()
    {
        if (HasCropPrefabs && cropPrefabs[currentCropIndex] != null)
        {
            GUI.Label(new Rect(10, 10, 300, 20),
                "Selected Crop: " + cropPrefabs[currentCropIndex].name + " (Q to switch)");
        }
        GUI.Label(new Rect(10, 30, 300, 20), "Harvested: " + cropCount);
    }
}