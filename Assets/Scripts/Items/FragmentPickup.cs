using UnityEngine;

public class FragmentPickup : ItemPickup
{
    [SerializeField] private int chunkId;
    [SerializeField] private int fragmentId;

    public override ItemPickupType PickupType => ItemPickupType.Fragment;

    public override bool OnPickedUp(PlayerInventory inventory)
    {
        return inventory.TryCollectFragment(chunkId, fragmentId);
    }
}
