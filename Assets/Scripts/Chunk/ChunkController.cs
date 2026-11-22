using UnityEngine;

public class ChunkController : MonoBehaviour
{
    [SerializeField, HideInInspector] private int chunkId;
    [SerializeField] private BoxCollider2D boundary;
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [SerializeField] private ChunkWeather weather;

    public int ChunkId => chunkId;
    public ChunkWeather Weather => weather;

    private Color originalBackgroundColor;

    internal void AssignChunkId(int id)
    {
        chunkId = id;
    }

    private void Awake()
    {
        if (boundary == null)
        {
            Debug.LogError("Chunk boundary is not assigned.", this);
        }

        if (backgroundRenderer == null)
        {
            Debug.LogError("Chunk background renderer is not assigned.", this);
        }
        else
        {
            originalBackgroundColor = backgroundRenderer.color;
        }
    }

    public Vector2 WorldCenter
    {
        get
        {
            return boundary.transform.TransformPoint(boundary.offset);
        }
    }

    public bool ContainsWorldPoint(Vector2 worldPoint)
    {
        Vector2 localPoint = boundary.transform.InverseTransformPoint(worldPoint);
        Vector2 halfSize = boundary.size * 0.5f;
        Vector2 localCenter = boundary.offset;

        return Mathf.Abs(localPoint.x - localCenter.x) <= halfSize.x
            && Mathf.Abs(localPoint.y - localCenter.y) <= halfSize.y;
    }

    public float GetWorldSizeAlongDirection(Vector2 worldDirection)
    {
        Vector2 localDirection = boundary.transform
            .InverseTransformVector(worldDirection)
            .normalized;
        Vector2 size = boundary.size;

        return Mathf.Abs(localDirection.x) * size.x
            + Mathf.Abs(localDirection.y) * size.y;
    }

    public void SetBackgroundColor(Color color)
    {
        if (backgroundRenderer != null)
        {
            backgroundRenderer.color = color;
        }
    }

    public void RestoreBackgroundColor()
    {
        if (backgroundRenderer != null)
        {
            backgroundRenderer.color = originalBackgroundColor;
        }
    }

    public float GetBoundaryDistance(Vector2 worldPoint, Vector2 worldDirection)
    {
        Vector2 localPoint = boundary.transform.InverseTransformPoint(worldPoint);
        Vector2 localDirection = boundary.transform.InverseTransformVector(worldDirection).normalized;
        Vector2 halfSize = boundary.size * 0.5f;
        Vector2 localCenter = boundary.offset;
        float distance = float.PositiveInfinity;

        if (localDirection.x > 0f)
        {
            distance = Mathf.Min(
                distance,
                (localCenter.x + halfSize.x - localPoint.x) / localDirection.x
            );
        }
        else if (localDirection.x < 0f)
        {
            distance = Mathf.Min(
                distance,
                (localCenter.x - halfSize.x - localPoint.x) / localDirection.x
            );
        }

        if (localDirection.y > 0f)
        {
            distance = Mathf.Min(
                distance,
                (localCenter.y + halfSize.y - localPoint.y) / localDirection.y
            );
        }
        else if (localDirection.y < 0f)
        {
            distance = Mathf.Min(
                distance,
                (localCenter.y - halfSize.y - localPoint.y) / localDirection.y
            );
        }

        return distance;
    }
}
