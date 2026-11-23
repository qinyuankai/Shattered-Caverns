using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public sealed class VineWall : MonoBehaviour
{
    private enum WallSurfaces
    {
        Both,
        FrontOnly,
        BackOnly
    }

    private const float CellEdgeInset = 0.001f;

    [SerializeField] private MechanismDirection direction;
    [SerializeField] private BoxCollider2D frontTrigger;
    [SerializeField] private BoxCollider2D backTrigger;
    [SerializeField] private Transform frontVisual;
    [SerializeField] private Transform backVisual;
    [SerializeField] private Tilemap growthWall;
    [SerializeField] private LayerMask solidLayers;
    [SerializeField, Min(1)] private int initialLength = 1;
    [SerializeField, Min(0f)] private float wallWidth;
    [SerializeField] private WallSurfaces wallSurfaces = WallSurfaces.Both;
    [SerializeField, Min(0.05f)] private float growthInterval = 0.5f;

    private readonly List<RaycastHit2D> skyHits = new List<RaycastHit2D>();
    private readonly List<Collider2D> growthOverlaps = new List<Collider2D>();
    private ChunkWeather weather;
    private TilemapCollider2D growthWallCollider;
    private (int Bottom, int Top) frontSpan;
    private (int Bottom, int Top) backSpan;
    private float elapsed;

    public bool CanClimb => isActiveAndEnabled && direction != null
        && Mathf.Abs(Vector2.Dot(direction.WorldDirection, Vector2.up)) > 0.999f;

    private void Awake()
    {
        weather = GetComponentInParent<ChunkWeather>();
        if (growthWall != null)
        {
            growthWall.TryGetComponent(out growthWallCollider);
        }

        frontSpan = backSpan = (0, Mathf.Max(1, initialLength));
        bool hasWall = wallWidth > 0f;
        frontTrigger.enabled = !hasWall || wallSurfaces != WallSurfaces.BackOnly;
        frontVisual.gameObject.SetActive(frontTrigger.enabled);
        if (frontTrigger.enabled)
        {
            float frontX = hasWall ? (wallWidth + frontTrigger.size.x) * 0.5f : 0f;
            UpdateSurface(frontTrigger, frontVisual, frontX, frontSpan);
        }

        backTrigger.enabled = hasWall && wallSurfaces != WallSurfaces.FrontOnly;
        backVisual.gameObject.SetActive(backTrigger.enabled);
        if (backTrigger.enabled)
        {
            float backX = -(wallWidth + backTrigger.size.x) * 0.5f;
            UpdateSurface(backTrigger, backVisual, backX, backSpan);
        }
    }

    private void FixedUpdate()
    {
        if (growthWall == null || weather == null || !weather.HasRain)
        {
            elapsed = 0f;
            return;
        }

        elapsed += Time.fixedDeltaTime;
        if (elapsed < growthInterval)
        {
            return;
        }

        elapsed = 0f;
        if (frontTrigger.enabled)
        {
            GrowSurface(ref frontSpan, frontTrigger, frontVisual);
        }
        if (backTrigger.enabled)
        {
            GrowSurface(ref backSpan, backTrigger, backVisual);
        }
    }

    private void GrowSurface(ref (int Bottom, int Top) span, BoxCollider2D trigger, Transform visual)
    {
        int bottom = span.Bottom - CountGrowableCells(span.Bottom + 0.5f, -1, trigger);
        int top = span.Top + CountGrowableCells(span.Top - 0.5f, 1, trigger);

        if (bottom != span.Bottom || top != span.Top)
        {
            span = (bottom, top);
            UpdateSurface(trigger, visual, trigger.offset.x, span);
        }
    }

    private bool ReceivesRain(float y)
    {
        float halfWall = wallWidth * 0.5f;
        return (frontTrigger.enabled && WeatherMechanismPhysics.HasClearSky(
                transform.TransformPoint(new Vector3(halfWall + frontTrigger.size.x, y, 0f)), solidLayers, null, skyHits))
            || (backTrigger.enabled && WeatherMechanismPhysics.HasClearSky(
                transform.TransformPoint(new Vector3(-halfWall - backTrigger.size.x, y, 0f)), solidLayers, null, skyHits));
    }

    private int CountGrowableCells(float endY, int step, BoxCollider2D trigger)
    {
        if (!ReceivesRain(endY))
        {
            return 0;
        }

        int count = 0;
        Vector2 size = new Vector2(trigger.size.x, 1f);
        for (int i = 0; i < 2; i++)
        {
            float y = endY + (i + 1) * step;
            Vector2 center = new Vector2(trigger.offset.x, y);
            if (growthWall.GetColliderType(WallCellAt(new Vector2(0f, y))) == Tile.ColliderType.None
                || ContainsSolidWallCell(center, size)
                || !WeatherMechanismPhysics.HasRoomForBox(
                    trigger.transform, center, size, solidLayers, growthWallCollider, growthOverlaps))
            {
                break;
            }

            count++;
        }

        return count;
    }

    private bool ContainsSolidWallCell(Vector2 center, Vector2 size)
    {
        Vector2 half = Vector2.Max(Vector2.zero, size * 0.5f - Vector2.one * CellEdgeInset);
        Vector2 otherDiagonal = new Vector2(half.x, -half.y);
        Vector3Int a = WallCellAt(center - half);
        Vector3Int b = WallCellAt(center + half);
        Vector3Int c = WallCellAt(center - otherDiagonal);
        Vector3Int d = WallCellAt(center + otherDiagonal);
        Vector3Int min = Vector3Int.Min(Vector3Int.Min(a, b), Vector3Int.Min(c, d));
        Vector3Int max = Vector3Int.Max(Vector3Int.Max(a, b), Vector3Int.Max(c, d));
        BoundsInt cells = new BoundsInt(min, max - min + Vector3Int.one);

        foreach (Vector3Int cell in cells.allPositionsWithin)
        {
            if (growthWall.GetColliderType(cell) != Tile.ColliderType.None)
            {
                return true;
            }
        }

        return false;
    }

    private Vector3Int WallCellAt(Vector2 localPoint)
    {
        return growthWall.WorldToCell(transform.TransformPoint(localPoint));
    }

    private static void UpdateSurface(
        BoxCollider2D trigger, Transform visual, float x, (int Bottom, int Top) span)
    {
        float y = (span.Bottom + span.Top) * 0.5f;
        float length = span.Top - span.Bottom;
        trigger.offset = new Vector2(x, y);
        trigger.size = new Vector2(trigger.size.x, length);
        visual.localPosition = new Vector3(x, y, 0f);
        visual.localScale = new Vector3(trigger.size.x, length, 1f);
    }
}
