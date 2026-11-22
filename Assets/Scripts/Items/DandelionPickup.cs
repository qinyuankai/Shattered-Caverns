using UnityEngine;

public sealed class DandelionPickup : ItemPickup
{
    public override bool OnPickedUp(PlayerInventory inventory)
    {
        return inventory.TryCollectDandelion(this);
    }

    public override void OnPickupSucceeded()
    {
        gameObject.SetActive(false);
    }

    internal void ReturnToSpawn(Vector3 spawnPosition)
    {
        transform.SetParent(null, true);
        transform.position = spawnPosition;
        gameObject.SetActive(true);
    }
}
