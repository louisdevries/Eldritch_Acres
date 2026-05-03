using UnityEngine;
using EldritchFarm.Crops;
using EldritchFarm.Player;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Crop System")]
    public GameObject[] cropPrefabs;
    private int currentCropIndex = 0;
    public GameObject ghostPrefab;

    public float interactDistance = 2f;

    public FarmGrid farmGrid;

    private PlayerWallet wallet;

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
        wallet = GetComponent<PlayerWallet>();
        if (wallet == null)
            Debug.LogWarning("[PlayerInteraction] No PlayerWallet on the player — planting will be free, harvesting will yield no coins.");

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

        // Check affordability of the currently selected crop.
        bool canAfford = true;
        if (HasCropPrefabs && wallet != null)
        {
            int cost = GetSeedCost(cropPrefabs[currentCropIndex]);
            canAfford = wallet.CanAfford(cost);
        }

        // Color feedback
        Color color;
        if (state == null)
            color = new Color(1f, 0.5f, 0f, 0.5f); // orange = untilled
        else if (state == FarmGrid.TileState.Occupied)
            color = new Color(1f, 0f, 0f, 0.5f);   // red = blocked
        else if (canPlant && canAfford)
            color = new Color(0f, 1f, 0f, 0.5f);   // green = ready
        else if (canPlant && !canAfford)
            color = new Color(0.4f, 0.4f, 1f, 0.5f); // blue = can place but can't afford
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
                int yield = crop.Harvest();
                if (wallet != null) wallet.Add(yield);

                // Walk up to the topmost CropBehavior ancestor. Defensive against prefabs
                // that ended up with CropBehavior on multiple GameObjects (e.g. when
                // [RequireComponent] auto-added one to a child) — we want to destroy the
                // whole crop, not just a piece of it.
                GameObject toDestroy = FindTopmostCropRoot(crop);

                if (HasFarmGrid)
                {
                    Vector2Int tile = farmGrid.WorldToGrid(toDestroy.transform.position);
                    if (farmGrid.GetTile(tile) == FarmGrid.TileState.Occupied)
                    {
                        farmGrid.SetTile(tile, FarmGrid.TileState.Tilled);
                    }
                }

                Destroy(toDestroy);
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

            // Check seed cost. Skip the spend if there's no wallet (free planting in dev/test).
            int cost = GetSeedCost(prefab);
            if (wallet != null && !wallet.TrySpend(cost)) return;

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

    /// <summary>
    /// Walk up the transform hierarchy to find the topmost ancestor that has a
    /// CropBehavior. This handles malformed prefabs where the same crop has
    /// CropBehavior components on both root and a child (e.g. from [RequireComponent]
    /// auto-adding one) — we always want to destroy the whole crop, not a fragment.
    /// </summary>
    GameObject FindTopmostCropRoot(CropBehavior startingCrop)
    {
        GameObject result = startingCrop.gameObject;
        Transform t = startingCrop.transform.parent;
        while (t != null)
        {
            if (t.GetComponent<CropBehavior>() != null)
                result = t.gameObject;
            t = t.parent;
        }
        return result;
    }

    /// <summary>
    /// Read the seed cost from a crop prefab's CropData. Returns 0 if the prefab
    /// is malformed (no CropBehavior or no Data assigned) — better to plant for
    /// free than to crash the planting flow.
    /// </summary>
    int GetSeedCost(GameObject prefab)
    {
        if (prefab == null) return 0;
        var crop = prefab.GetComponentInChildren<CropBehavior>();
        if (crop == null || crop.Data == null) return 0;
        return crop.Data.seedCost;
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

        // Strip gameplay scripts. We have to destroy them in *dependency order* —
        // any component with [RequireComponent(...)] must be destroyed before the
        // component it requires, otherwise Unity blocks the destroy. Easiest way:
        // destroy everything except CropBehavior subclasses first, then destroy
        // CropBehavior subclasses last.
        var allBehaviours = visual.GetComponentsInChildren<MonoBehaviour>();

        // Pass 1: dependents (visualizers, etc.)
        foreach (var comp in allBehaviours)
        {
            if (comp == null) continue;
            if (comp is EldritchFarm.Crops.CropBehavior) continue; // skip crops, do them last
            Destroy(comp);
        }

        // Pass 2: crops themselves
        foreach (var comp in allBehaviours)
        {
            if (comp == null) continue;
            if (comp is EldritchFarm.Crops.CropBehavior)
                Destroy(comp);
        }

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
        int y = 10;

        if (wallet != null)
        {
            GUI.Label(new Rect(10, y, 300, 20), $"Coins: {wallet.Coins}");
            y += 20;
        }

        if (HasCropPrefabs && cropPrefabs[currentCropIndex] != null)
        {
            int cost = GetSeedCost(cropPrefabs[currentCropIndex]);
            string costLabel = cost > 0 ? $" — {cost} coins" : "";
            GUI.Label(new Rect(10, y, 400, 20),
                $"Selected: {cropPrefabs[currentCropIndex].name}{costLabel}  (Q to switch)");
            y += 20;
        }

        GUI.Label(new Rect(10, y, 300, 20), "E to plant/harvest, F to till");
    }
}