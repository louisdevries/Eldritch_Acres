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

    // -----------------------------
    // FIX: stable ghost scaling
    // -----------------------------
    private Vector3 ghostBaseScale;

    void Start()
    {
        ghostInstance = Instantiate(ghostPrefab);
        ghostRenderer = ghostInstance.GetComponentInChildren<Renderer>();

        ghostBaseScale = ghostInstance.transform.localScale;

        // Position ghost properly on first frame so it doesn't pop
        Vector3 forwardPosition = transform.position + transform.forward * interactDistance;
        forwardPosition.y = 0;

        Vector2Int gridPos = farmGrid.WorldToGrid(forwardPosition);
        Vector3 snappedPos = farmGrid.GridToWorld(gridPos);

        ghostInstance.transform.position = snappedPos;
        ghostInstance.transform.rotation = Quaternion.identity;
        UpdateGhostVisual();
    }

    void Update()
    {
        UpdateGhost();

        if (Input.GetKeyDown(KeyCode.E))
        {
            TryPlant();
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            TryTill();
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            CycleCrop();
        }
    }

    // -----------------------------
    // GHOST SYSTEM
    // -----------------------------
    void UpdateGhost()
    {
        Vector3 forwardPosition = transform.position + transform.forward * interactDistance;
        forwardPosition.y = 0;

        Vector2Int gridPos = farmGrid.WorldToGrid(forwardPosition);
        Vector3 snappedPos = farmGrid.GridToWorld(gridPos);

        ghostInstance.transform.position = snappedPos;
        ghostInstance.transform.rotation = Quaternion.identity;

        bool canPlant = CanPlantAt(gridPos);
        var state = farmGrid.GetTile(gridPos);

        // -----------------------------
        // COLOR FEEDBACK
        // -----------------------------
        if (ghostRenderer != null)
        {
            Color color;

            if (state == null)
            {
                color = new Color(1f, 0.5f, 0f, 0.5f); // orange = untilled
            }
            else if (state == FarmGrid.TileState.Occupied)
            {
                color = new Color(1f, 0f, 0f, 0.5f); // red
            }
            else if (canPlant)
            {
                color = new Color(0f, 1f, 0f, 0.5f); // green
            }
            else
            {
                color = new Color(1f, 0f, 0f, 0.5f);
            }

            ghostRenderer.material.color = color;
        }

        // -----------------------------
        // SMOOTH SCALING
        // -----------------------------
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

        Vector2Int gridPos = farmGrid.WorldToGrid(forwardPosition);
        Vector3 snappedPos = farmGrid.GridToWorld(gridPos);

        // -----------------------------
        // HARVEST FIRST
        // -----------------------------
        Collider[] colliders = Physics.OverlapSphere(forwardPosition, 1f);

        foreach (Collider col in colliders)
        {
            CropBehavior crop = col.GetComponentInParent<CropBehavior>();

            if (crop != null && crop.CanHarvest())
            {
                Vector2Int tile = farmGrid.WorldToGrid(crop.transform.position);

                cropCount += crop.Harvest();

                farmGrid.SetTile(tile, FarmGrid.TileState.Tilled);

                Destroy(crop.gameObject);
                return;
            }
        }

        // -----------------------------
        // PLANTING
        // -----------------------------
        if (CanPlantAt(gridPos))
        {
            GameObject prefab = cropPrefabs[currentCropIndex];
            Instantiate(prefab, snappedPos, Quaternion.identity);
            farmGrid.SetTile(gridPos, FarmGrid.TileState.Occupied);
        }
    }

    // -----------------------------
    // TILLING
    // -----------------------------
    void TryTill()
    {
        Vector3 pos = transform.position + transform.forward * interactDistance;
        Vector2Int tile = farmGrid.WorldToGrid(pos);

        if (farmGrid.GetTile(tile) == null)
        {
            farmGrid.SetTile(tile, FarmGrid.TileState.Tilled);
        }
    }

    // -----------------------------
    // PLANT RULES
    // -----------------------------
    bool CanPlantAt(Vector2Int tile)
    {
        var state = farmGrid.GetTile(tile);

        if (state != FarmGrid.TileState.Tilled)
            return false;

        // spacing rule
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
        if (cropPrefabs == null || cropPrefabs.Length == 0) return;

        currentCropIndex++;
        if (currentCropIndex >= cropPrefabs.Length)
            currentCropIndex = 0;

        UpdateGhostVisual();
    }

    void UpdateGhostVisual()
    {
        if (ghostInstance == null) return;

        // destroy old mesh
        foreach (Transform child in ghostInstance.transform)
        {
            Destroy(child.gameObject);
        }

        GameObject prefab = cropPrefabs[currentCropIndex];

        // copy visual from crop prefab
        GameObject visual = Instantiate(prefab, ghostInstance.transform);

        // remove gameplay scripts
        foreach (var comp in visual.GetComponentsInChildren<MonoBehaviour>())
        {
            Destroy(comp);
        }

        // remove colliders
        foreach (var col in visual.GetComponentsInChildren<Collider>())
        {
            Destroy(col);
        }

        // make transparent
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
        if (cropPrefabs != null && cropPrefabs.Length > 0)
        {
            GUI.Label(new Rect(10, 10, 300, 20),
                "Selected Crop: " + cropPrefabs[currentCropIndex].name + " (Q to switch)");
        }
        GUI.Label(new Rect(10, 30, 300, 20), "Harvested: " + cropCount);
    }
}