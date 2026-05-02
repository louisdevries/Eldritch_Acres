using System.Collections.Generic;
using UnityEngine;

public class FarmGrid : MonoBehaviour
{
    public enum TileState
    {
        Tilled,
        Occupied
    }

    private Dictionary<Vector2Int, TileState> tiles = new Dictionary<Vector2Int, TileState>();

    public float gridSize = 1f;
    public GameObject tilePrefab;

    private Dictionary<Vector2Int, GameObject> tileObjects = new Dictionary<Vector2Int, GameObject>();

    // -----------------------------
    // GET TILE (NULL = UNTILLED)
    // -----------------------------
    public TileState? GetTile(Vector2Int pos)
    {
        if (tiles.TryGetValue(pos, out TileState state))
            return state;

        return null; // UNTILLED (no tile exists)
    }

    // -----------------------------
    // SET TILE
    // -----------------------------
    public void SetTile(Vector2Int pos, TileState state)
    {
        tiles[pos] = state;
        UpdateTileVisual(pos);
    }

    // -----------------------------
    // GRID CONVERSIONS
    // -----------------------------
    public Vector2Int WorldToGrid(Vector3 pos)
    {
        return new Vector2Int(
            Mathf.RoundToInt(pos.x / gridSize),
            Mathf.RoundToInt(pos.z / gridSize)
        );
    }

    public Vector3 GridToWorld(Vector2Int gridPos)
    {
        return new Vector3(
            gridPos.x * gridSize,
            0f,
            gridPos.y * gridSize
        );
    }

    // -----------------------------
    // TILE VISUALS
    // -----------------------------
    public void UpdateTileVisual(Vector2Int pos)
    {
        TileState? state = GetTile(pos);

        // ❗ IMPORTANT: if null = UNTILLED → remove tile object
        if (state == null)
        {
            if (tileObjects.TryGetValue(pos, out GameObject obj))
            {
                Destroy(obj);
                tileObjects.Remove(pos);
            }
            return;
        }

        // Create or reuse tile object
        if (!tileObjects.TryGetValue(pos, out GameObject tileObj))
        {
            tileObj = Instantiate(tilePrefab, GridToWorld(pos), Quaternion.identity);
            tileObjects[pos] = tileObj;
        }

        FarmTile tile = tileObj.GetComponent<FarmTile>();
        if (tile == null) return;

        // -----------------------------
        // COLORS (soil states)
        // -----------------------------
        Color targetColor = Color.white;

        switch (state.Value)
        {
            case TileState.Tilled:
                targetColor = new Color(0.45f, 0.28f, 0.1f); // brown soil
                break;

            case TileState.Occupied:
                targetColor = new Color(0.3f, 0.5f, 0.2f); // planted green
                break;
        }

        tile.SetVisual(targetColor);
    }

    // -----------------------------
    // CORRUPTION MAPPING
    // -----------------------------
    public float GetCorruption(Vector2Int pos)
    {
        if (!tiles.TryGetValue(pos, out TileState state))
            return 0f;

        return state switch
        {
            TileState.Occupied => 0.8f,
            TileState.Tilled   => 0.2f,
            _ => 0f
        };
    }
}