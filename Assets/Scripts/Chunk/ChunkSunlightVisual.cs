using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public sealed class ChunkSunlightVisual : MonoBehaviour
{
    private const float SurfaceOffset = 0.01f;

    [SerializeField] private ChunkController chunk;
    [SerializeField] private Tilemap solidTilemap;
    [SerializeField] private LayerMask solidLayers;
    [SerializeField, Range(0f, 1f)] private float shadowBrightness = 0.55f;

    private readonly List<RaycastHit2D> skyHits = new List<RaycastHit2D>();
    private readonly Dictionary<Vector3Int, Color> originalColors = new Dictionary<Vector3Int, Color>();
    private LevelManager levelManager;

    private void OnEnable()
    {
        if (chunk == null || solidTilemap == null || chunk.Weather == null || !chunk.Weather.CanFreezePlayer)
        {
            return;
        }

        levelManager = FindObjectOfType<LevelManager>();
        if (levelManager != null)
        {
            levelManager.ChunkLayoutChanged += RefreshSunlight;
        }
    }

    private void OnDisable()
    {
        if (levelManager != null)
        {
            levelManager.ChunkLayoutChanged -= RefreshSunlight;
        }

        levelManager = null;
    }

    private void RefreshSunlight()
    {
        Vector3 origin = solidTilemap.CellToWorld(Vector3Int.zero);
        Vector3 cellX = solidTilemap.CellToWorld(Vector3Int.right) - origin;
        Vector3 cellY = solidTilemap.CellToWorld(Vector3Int.up) - origin;
        float worldHalfHeight = 0.5f * (Mathf.Abs(cellX.y) + Mathf.Abs(cellY.y));
        Vector3 sampleOffset = Vector3.up * (worldHalfHeight + SurfaceOffset);
        Color shadowTint = new Color(shadowBrightness, shadowBrightness, shadowBrightness, 1f);

        foreach (Vector3Int cell in solidTilemap.cellBounds.allPositionsWithin)
        {
            if (!solidTilemap.HasTile(cell))
            {
                continue;
            }

            Vector2 sample = solidTilemap.GetCellCenterWorld(cell) + sampleOffset;
            bool hasSunlight = chunk.Weather.HasSunlight
                && WeatherMechanismPhysics.HasClearSky(sample, solidLayers, null, skyHits);
            if (!originalColors.TryGetValue(cell, out Color originalColor))
            {
                originalColor = solidTilemap.GetColor(cell);
                originalColors.Add(cell, originalColor);
            }

            ApplyColor(cell, hasSunlight ? originalColor : originalColor * shadowTint);
        }
    }

    private void ApplyColor(Vector3Int cell, Color color)
    {
        if (solidTilemap.GetColor(cell) == color)
        {
            return;
        }

        TileFlags flags = solidTilemap.GetTileFlags(cell);
        solidTilemap.RemoveTileFlags(cell, TileFlags.LockColor);
        solidTilemap.SetColor(cell, color);
        solidTilemap.SetTileFlags(cell, flags);
    }
}
