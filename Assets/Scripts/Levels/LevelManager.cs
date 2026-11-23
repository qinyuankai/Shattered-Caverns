using System;
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
    internal event Action ChunkLayoutChanged;

    private readonly Dictionary<ChunkController, HashSet<int>> collectedFragments =
        new Dictionary<ChunkController, HashSet<int>>();

    private void Awake()
    {
        ApplyChunkConfigurations();
        ApplyPlacementStates();
        ValidateAvailableChunks();
        InitializeFragmentRecords();
    }

    private void Start()
    {
        NotifyChunkLayoutChanged();
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

    internal bool IsChunkPlaced(ChunkController chunk)
    {
        return chunk != null && FindConfiguration(chunk)?.IsPlaced == true;
    }

    private void ValidateAvailableChunks()
    {
        var uniqueChunks = new HashSet<ChunkController>();
        availableChunks.RemoveAll(chunk =>
        {
            LevelChunkConfiguration configuration = chunk == null ? null : FindConfiguration(chunk);
            bool isValid = configuration != null && configuration.isMovable
                && !configuration.IsPlaced && uniqueChunks.Add(chunk);

            if (!isValid)
            {
                Debug.LogWarning($"Invalid or duplicate Available Chunks entry: {chunk}.", this);
            }

            return !isValid;
        });
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
        NotifyChunkLayoutChanged();
        return true;
    }

    internal void NotifyChunkLayoutChanged()
    {
        Physics2D.SyncTransforms();
        ChunkLayoutChanged?.Invoke();
    }

    public bool IsChunkAvailable(int chunkId)
    {
        LevelChunkConfiguration configuration = FindConfiguration(chunkId);
        return configuration != null
            && configuration.isMovable
            && !configuration.IsPlaced
            && availableChunks.Contains(configuration.chunk);
    }

    public bool IsChunkUnlocked(int chunkId)
    {
        LevelChunkConfiguration configuration = FindConfiguration(chunkId);
        return configuration != null && IsChunkUnlocked(configuration);
    }

    internal LevelChunkConfiguration FindPlacedChunkAt(Vector2 worldPosition, float tolerance)
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
                return configuration;
            }
        }

        return null;
    }

    internal bool TryCollectFragment(ChunkController chunk, int fragmentId)
    {
        return chunk != null && TryCollectFragment(FindConfiguration(chunk), fragmentId);
    }

    private bool TryCollectFragment(LevelChunkConfiguration configuration, int fragmentId)
    {
        if (configuration == null || IsChunkUnlocked(configuration)
            || !collectedFragments.TryGetValue(configuration.chunk, out HashSet<int> fragments))
        {
            return false;
        }

        bool collected = fragments.Add(fragmentId);

        if (collected && fragments.Count >= configuration.requiredFragmentCount)
        {
            availableChunks.Add(configuration.chunk);
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
        return configuration != null
            && collectedFragments.TryGetValue(configuration.chunk, out HashSet<int> fragments)
            && fragments.Contains(fragmentId);
    }

    public int GetCollectedFragmentCount(int chunkId)
    {
        return GetCollectedFragmentCount(FindConfiguration(chunkId));
    }

    private int GetCollectedFragmentCount(LevelChunkConfiguration configuration)
    {
        if (configuration == null)
        {
            return 0;
        }

        if (IsChunkUnlocked(configuration))
        {
            return configuration.requiredFragmentCount;
        }

        return collectedFragments.TryGetValue(configuration.chunk, out HashSet<int> fragments)
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

            if (IsChunkUnlocked(configuration))
            {
                continue;
            }

            if (configuration.requiredFragmentCount >= 1
                && !collectedFragments.ContainsKey(configuration.chunk))
            {
                collectedFragments.Add(configuration.chunk, new HashSet<int>());
            }
        }

        RefreshDebugView();
    }

    private bool IsChunkUnlocked(LevelChunkConfiguration configuration)
    {
        return configuration.IsPlaced || availableChunks.Contains(configuration.chunk);
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

        foreach (KeyValuePair<ChunkController, HashSet<int>> entry in collectedFragments)
        {
            if (entry.Key == null)
            {
                continue;
            }

            LevelChunkConfiguration configuration = FindConfiguration(entry.Key);
            if (configuration == null)
            {
                continue;
            }

            CollectedFragmentDebugRecord record = new CollectedFragmentDebugRecord
            {
                chunkId = entry.Key.ChunkId,
                fragmentIds = new List<int>(entry.Value),
                collectedFragmentCount = GetCollectedFragmentCount(configuration),
                requiredFragmentCount = configuration.requiredFragmentCount,
                isAvailable = IsChunkAvailable(entry.Key.ChunkId),
                isPlaced = configuration.IsPlaced
            };

            record.fragmentIds.Sort();
            debugCollectedFragments.Add(record);
        }
    }
}
