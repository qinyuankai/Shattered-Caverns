using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    private LevelManager levelManager;

    private void Awake()
    {
        levelManager = FindObjectOfType<LevelManager>();

        if (levelManager == null)
        {
            Debug.LogError("LevelManager is not found.", this);
        }
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
        return levelManager != null
            && levelManager.TryCollectFragment(chunkId, fragmentId);
    }

    public bool HasFragment(int chunkId, int fragmentId)
    {
        return levelManager != null
            && levelManager.HasFragment(chunkId, fragmentId);
    }

    public int GetCollectedFragmentCount(int chunkId)
    {
        return levelManager == null
            ? 0
            : levelManager.GetCollectedFragmentCount(chunkId);
    }
}
