using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class ConfusionMushroom : MonoBehaviour
{
    private bool hasTriggered;

    private void Reset()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActiveAndEnabled || hasTriggered || other.isTrigger)
        {
            return;
        }

        Rigidbody2D playerBody = other.attachedRigidbody;
        if (playerBody == null || !playerBody.TryGetComponent(out PlayerMovement playerMovement))
        {
            return;
        }

        hasTriggered = true;
        playerMovement.SetHorizontalInputInverted(true);
        gameObject.SetActive(false);
    }
}
