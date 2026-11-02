using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CollectedFragmentDebugRecord
{
    public int chunkId;
    public List<int> fragmentIds = new List<int>();
    public int collectedFragmentCount;
    public int requiredFragmentCount;
    public bool isAvailable;
    public bool isPlaced;
}

[System.Serializable]
public class LevelChunkConfiguration
{
    public ChunkController chunk;
    public bool isMovable = true;
    [Min(1)] public int requiredFragmentCount = 1;

    [SerializeField] private bool isPlaced = true;

    public bool IsPlaced => isPlaced;

    internal void AssignPlacedState(bool placed)
    {
        isPlaced = placed;
    }
}

[DefaultExecutionOrder(-100)]
public class LevelManager : MonoBehaviour
{
    [SerializeField] private List<LevelChunkConfiguration> chunkConfigurations =
        new List<LevelChunkConfiguration>();
    [SerializeField] private List<ChunkController> availableChunks =
        new List<ChunkController>();
    [SerializeField] private List<CollectedFragmentDebugRecord> debugCollectedFragments =
        new List<CollectedFragmentDebugRecord>();

    public IReadOnlyList<LevelChunkConfiguration> ChunkConfigurations => chunkConfigurations;
    public IReadOnlyList<ChunkController> AvailableChunks => availableChunks;

    private readonly Dictionary<int, HashSet<int>> collectedFragments =
        new Dictionary<int, HashSet<int>>();

    private void Awake()
    {
        ApplyChunkConfigurations();
        ApplyPlacementStates();
        InitializeFragmentRecords();
    }

    private void OnValidate()
    {
        ApplyChunkConfigurations();
    }

    private void ApplyChunkConfigurations()
    {
        int chunkId = 1;

        foreach (LevelChunkConfiguration configuration in chunkConfigurations)
        {
            if (configuration == null || configuration.chunk == null)
            {
                continue;
            }

            configuration.chunk.AssignChunkId(chunkId);
            configuration.chunk.AssignMovableState(configuration.isMovable);
            configuration.chunk.gameObject.name = $"Chunk_{chunkId:000}";
            chunkId++;
        }
    }

    private void ApplyPlacementStates()
    {
        foreach (LevelChunkConfiguration configuration in chunkConfigurations)
        {
            if (configuration == null || configuration.chunk == null)
            {
                continue;
            }

            bool isPlaced = !configuration.isMovable || configuration.IsPlaced;
            configuration.AssignPlacedState(isPlaced);
            configuration.chunk.gameObject.SetActive(isPlaced);
        }
    }

    public bool IsChunkPlaced(int chunkId)
    {
        LevelChunkConfiguration configuration = FindConfiguration(chunkId);
        return configuration != null && configuration.IsPlaced;
    }

    public bool TrySetChunkPlaced(int chunkId, bool placed)
    {
        LevelChunkConfiguration configuration = FindConfiguration(chunkId);

        if (configuration == null || !configuration.isMovable)
        {
            return false;
        }

        if (placed && configuration.IsPlaced)
        {
            return true;
        }

        if (placed && !availableChunks.Contains(configuration.chunk))
        {
            return false;
        }

        if (!placed && !configuration.IsPlaced)
        {
            return false;
        }

        configuration.AssignPlacedState(placed);
        configuration.chunk.gameObject.SetActive(placed);

        if (placed)
        {
            availableChunks.Remove(configuration.chunk);
        }
        else if (!availableChunks.Contains(configuration.chunk))
        {
            availableChunks.Add(configuration.chunk);
        }

        RefreshDebugView();
        return true;
    }

    public bool IsChunkAvailable(int chunkId)
    {
        LevelChunkConfiguration configuration = FindConfiguration(chunkId);
        return configuration != null
            && !configuration.IsPlaced
            && availableChunks.Contains(configuration.chunk);
    }

    public bool IsChunkPositionOccupiedAt(Vector2 worldPosition, float tolerance)
    {
        float squaredTolerance = tolerance * tolerance;

        foreach (LevelChunkConfiguration configuration in chunkConfigurations)
        {
            if (configuration == null
                || configuration.chunk == null
                || !configuration.IsPlaced)
            {
                continue;
            }

            Vector2 offset = configuration.chunk.WorldCenter - worldPosition;

            if (offset.sqrMagnitude <= squaredTolerance)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryGetPlacedMovableChunkAt(
        Vector2 worldPosition,
        float tolerance,
        out ChunkController chunk)
    {
        float squaredTolerance = tolerance * tolerance;

        foreach (LevelChunkConfiguration configuration in chunkConfigurations)
        {
            if (configuration == null
                || configuration.chunk == null
                || !configuration.isMovable
                || !configuration.IsPlaced)
            {
                continue;
            }

            Vector2 offset = configuration.chunk.WorldCenter - worldPosition;

            if (offset.sqrMagnitude <= squaredTolerance)
            {
                chunk = configuration.chunk;
                return true;
            }
        }

        chunk = null;
        return false;
    }

    public bool TryCollectFragment(int chunkId, int fragmentId)
    {
        if (!collectedFragments.TryGetValue(chunkId, out HashSet<int> fragments))
        {
            return false;
        }

        LevelChunkConfiguration configuration = FindConfiguration(chunkId);

        if (configuration == null
            || configuration.IsPlaced
            || availableChunks.Contains(configuration.chunk))
        {
            return false;
        }

        bool collected = fragments.Add(fragmentId);

        if (collected && IsFragmentRequirementComplete(chunkId, fragments.Count))
        {
            if (!availableChunks.Contains(configuration.chunk))
            {
                availableChunks.Add(configuration.chunk);
            }
        }

        if (collected)
        {
            RefreshDebugView();
        }

        return collected;
    }

    public bool HasFragment(int chunkId, int fragmentId)
    {
        LevelChunkConfiguration configuration = FindConfiguration(chunkId);

        if (configuration != null
            && (configuration.IsPlaced || availableChunks.Contains(configuration.chunk)))
        {
            return true;
        }

        return collectedFragments.TryGetValue(chunkId, out HashSet<int> fragments)
            && fragments.Contains(fragmentId);
    }

    public int GetCollectedFragmentCount(int chunkId)
    {
        LevelChunkConfiguration configuration = FindConfiguration(chunkId);

        if (configuration != null
            && (configuration.IsPlaced || availableChunks.Contains(configuration.chunk)))
        {
            return configuration.requiredFragmentCount;
        }

        return collectedFragments.TryGetValue(chunkId, out HashSet<int> fragments)
            ? fragments.Count
            : 0;
    }

    private void InitializeFragmentRecords()
    {
        collectedFragments.Clear();

        foreach (LevelChunkConfiguration configuration in chunkConfigurations)
        {
            if (configuration == null
                || configuration.chunk == null
                || !configuration.isMovable)
            {
                continue;
            }

            int chunkId = configuration.chunk.ChunkId;

            if (configuration.IsPlaced)
            {
                continue;
            }

            if (configuration.requiredFragmentCount >= 1
                && !collectedFragments.ContainsKey(chunkId))
            {
                collectedFragments.Add(chunkId, new HashSet<int>());
            }
        }

        RefreshDebugView();
    }

    private bool IsFragmentRequirementComplete(int chunkId, int collectedCount)
    {
        LevelChunkConfiguration configuration = FindConfiguration(chunkId);
        return configuration != null
            && collectedCount >= configuration.requiredFragmentCount;
    }

    private LevelChunkConfiguration FindConfiguration(int chunkId)
    {
        foreach (LevelChunkConfiguration configuration in chunkConfigurations)
        {
            if (configuration != null
                && configuration.chunk != null
                && configuration.chunk.ChunkId == chunkId)
            {
                return configuration;
            }
        }

        return null;
    }

    private LevelChunkConfiguration FindConfiguration(ChunkController chunk)
    {
        foreach (LevelChunkConfiguration configuration in chunkConfigurations)
        {
            if (configuration != null && configuration.chunk == chunk)
            {
                return configuration;
            }
        }

        return null;
    }

    private void RefreshDebugView()
    {
        debugCollectedFragments.Clear();

        foreach (KeyValuePair<int, HashSet<int>> entry in collectedFragments)
        {
            CollectedFragmentDebugRecord record = new CollectedFragmentDebugRecord
            {
                chunkId = entry.Key,
                fragmentIds = new List<int>(entry.Value),
                collectedFragmentCount = GetCollectedFragmentCount(entry.Key),
                requiredFragmentCount = GetRequiredFragmentCount(entry.Key),
                isAvailable = IsChunkAvailable(entry.Key),
                isPlaced = IsChunkPlaced(entry.Key)
            };

            record.fragmentIds.Sort();
            debugCollectedFragments.Add(record);
        }
    }

    private int GetRequiredFragmentCount(int chunkId)
    {
        LevelChunkConfiguration configuration = FindConfiguration(chunkId);
        return configuration == null ? 0 : configuration.requiredFragmentCount;
    }
}
