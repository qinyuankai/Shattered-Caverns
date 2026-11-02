using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerChunkTracker : MonoBehaviour
{
    private const string ChunkLayerName = "Chunk";

    [SerializeField] private ChunkController currentChunk;
    [SerializeField] private Vector2 currentChunkCenter;

    public ChunkController CurrentChunk => currentChunk;
    public Vector2 CurrentChunkCenter => currentChunkCenter;
    public event Action<ChunkController> CurrentChunkChanged;

    private readonly HashSet<ChunkController> trackedChunks =
        new HashSet<ChunkController>();
    private int chunkLayerMask;

    private void Awake()
    {
        chunkLayerMask = LayerMask.GetMask(ChunkLayerName);

        if (chunkLayerMask == 0)
        {
            Debug.LogError($"Layer '{ChunkLayerName}' does not exist.", this);
        }
    }

    private void Start()
    {
        RegisterOverlappingChunks();
        RefreshCurrentChunk();
    }

    private void LateUpdate()
    {
        RefreshCurrentChunk();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        RegisterChunkFromTrigger(other);
    }

    private void RegisterOverlappingChunks()
    {
        Collider2D[] overlappingColliders = Physics2D.OverlapPointAll(
            transform.position,
            chunkLayerMask
        );

        foreach (Collider2D collider in overlappingColliders)
        {
            ChunkController chunk = collider.GetComponentInParent<ChunkController>();

            if (chunk != null)
            {
                trackedChunks.Add(chunk);
            }
        }
    }

    private void RegisterChunkFromTrigger(Collider2D other)
    {
        if (!IsInChunkLayer(other.gameObject.layer))
        {
            return;
        }

        ChunkController chunk = other.GetComponentInParent<ChunkController>();

        if (chunk != null)
        {
            trackedChunks.Add(chunk);
        }
    }

    private void RefreshCurrentChunk()
    {
        ChunkController nextChunk = null;
        float nearestDistance = float.PositiveInfinity;
        List<ChunkController> invalidChunks = new List<ChunkController>();

        foreach (ChunkController trackedChunk in trackedChunks)
        {
            if (trackedChunk == null)
            {
                invalidChunks.Add(trackedChunk);
                continue;
            }

            if (!trackedChunk.ContainsWorldPoint(transform.position))
            {
                continue;
            }

            float distance = (trackedChunk.WorldCenter - (Vector2)transform.position).sqrMagnitude;

            if (distance < nearestDistance)
            {
                nextChunk = trackedChunk;
                nearestDistance = distance;
            }
        }

        foreach (ChunkController invalidChunk in invalidChunks)
        {
            trackedChunks.Remove(invalidChunk);
        }

        if (currentChunk == nextChunk)
        {
            currentChunkCenter = nextChunk == null ? Vector2.zero : nextChunk.WorldCenter;
            return;
        }

        currentChunk = nextChunk;
        currentChunkCenter = nextChunk == null ? Vector2.zero : nextChunk.WorldCenter;
        CurrentChunkChanged?.Invoke(currentChunk);
    }

    private bool IsInChunkLayer(int layer)
    {
        return (chunkLayerMask & (1 << layer)) != 0;
    }
}
