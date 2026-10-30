using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CollectedFragmentDebugRecord
{
    public int chunkId;
    public List<int> fragmentIds = new List<int>();
}

public class PlayerInventory : MonoBehaviour
{
    [SerializeField] private List<CollectedFragmentDebugRecord> debugCollectedFragments =
        new List<CollectedFragmentDebugRecord>();

    private readonly Dictionary<int, HashSet<int>> collectedFragments =
        new Dictionary<int, HashSet<int>>();

    private void Awake()
    {
        InitializeFragmentRecords(FindObjectOfType<LevelFragmentManager>());
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        ItemPickup itemPickup = other.GetComponentInParent<ItemPickup>();

        if (itemPickup == null)
        {
            return;
        }

        if (itemPickup.OnPickedUp(this))
        {
            Destroy(itemPickup.gameObject);
        }
    }

    public bool TryCollectFragment(int chunkId, int fragmentId)
    {
        if (!collectedFragments.TryGetValue(chunkId, out HashSet<int> fragments))
        {
            return false;
        }

        bool collected = fragments.Add(fragmentId);

        if (collected)
        {
            RefreshDebugView();
        }

        return collected;
    }

    public bool HasFragment(int chunkId, int fragmentId)
    {
        return collectedFragments.TryGetValue(chunkId, out HashSet<int> fragments)
            && fragments.Contains(fragmentId);
    }

    public int GetCollectedFragmentCount(int chunkId)
    {
        return collectedFragments.TryGetValue(chunkId, out HashSet<int> fragments)
            ? fragments.Count
            : 0;
    }

    private void InitializeFragmentRecords(LevelFragmentManager levelFragmentManager)
    {
        if (levelFragmentManager == null)
        {
            Debug.LogError("LevelFragmentManager is not assigned.", this);
            return;
        }

        foreach (ChunkFragmentRequirement requirement in levelFragmentManager.ChunkRequirements)
        {
            if (requirement == null || requirement.requiredFragmentCount < 1)
            {
                continue;
            }

            if (!collectedFragments.ContainsKey(requirement.chunkId))
            {
                collectedFragments.Add(requirement.chunkId, new HashSet<int>());
            }
        }

        RefreshDebugView();
    }

    private void RefreshDebugView()
    {
        debugCollectedFragments.Clear();

        foreach (KeyValuePair<int, HashSet<int>> entry in collectedFragments)
        {
            CollectedFragmentDebugRecord record = new CollectedFragmentDebugRecord
            {
                chunkId = entry.Key,
                fragmentIds = new List<int>(entry.Value)
            };

            record.fragmentIds.Sort();
            debugCollectedFragments.Add(record);
        }
    }
}
