using UnityEngine;

public class FragmentPickup : ItemPickup
{
    [SerializeField] private ChunkController targetChunk;
    [SerializeField] private int fragmentId;

    public override bool OnPickedUp(PlayerInventory inventory)
    {
        return inventory.TryCollectFragment(targetChunk, fragmentId);
    }
}
