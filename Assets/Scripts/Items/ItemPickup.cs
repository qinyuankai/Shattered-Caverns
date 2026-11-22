using UnityEngine;

public abstract class ItemPickup : MonoBehaviour
{
    public abstract bool OnPickedUp(PlayerInventory inventory);

    public virtual void OnPickupSucceeded()
    {
        Destroy(gameObject);
    }
}
