using UnityEngine;

public enum ItemPickupType
{
    Fragment,
    Key,
    Collectible
}

public abstract class ItemPickup : MonoBehaviour
{
    public abstract ItemPickupType PickupType { get; }

    public abstract bool OnPickedUp(PlayerInventory inventory);
}
