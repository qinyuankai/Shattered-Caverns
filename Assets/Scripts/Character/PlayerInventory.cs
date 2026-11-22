using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    private LevelManager levelManager;
    private DandelionPickup carriedDandelion;

    public bool IsCarryingDandelion => carriedDandelion != null;

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
            itemPickup.OnPickupSucceeded();
        }
    }

    internal bool TryCollectDandelion(DandelionPickup pickup)
    {
        if (IsCarryingDandelion)
        {
            return false;
        }

        carriedDandelion = pickup;
        return true;
    }

    internal void ReturnCarriedDandelion(Vector3 spawnPosition)
    {
        DandelionPickup pickup = carriedDandelion;
        carriedDandelion = null;
        if (pickup != null)
        {
            pickup.ReturnToSpawn(spawnPosition);
        }
    }

    internal bool TryCollectFragment(ChunkController chunk, int fragmentId)
    {
        return levelManager != null
            && levelManager.TryCollectFragment(chunk, fragmentId);
    }
}
