using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private PlayerChunkTracker playerChunkTracker;
    [SerializeField] private Transform player;
    [SerializeField, Min(0f)] private float smoothTime = 0.2f;
    [SerializeField] private float cameraZ = -10f;

    private Vector3 movementVelocity;

    private void Awake()
    {
        if (playerChunkTracker == null)
        {
            playerChunkTracker = FindObjectOfType<PlayerChunkTracker>();
        }

        if (player == null && playerChunkTracker != null)
        {
            player = playerChunkTracker.transform;
        }

        if (playerChunkTracker == null)
        {
            Debug.LogError("PlayerChunkTracker is not found.", this);
        }
    }

    private void LateUpdate()
    {
        if (playerChunkTracker == null)
        {
            return;
        }

        Vector2 targetPosition = GetTargetPosition();
        Vector3 target = new Vector3(targetPosition.x, targetPosition.y, cameraZ);

        transform.position = Vector3.SmoothDamp(
            transform.position,
            target,
            ref movementVelocity,
            smoothTime
        );
    }

    private Vector2 GetTargetPosition()
    {
        if (playerChunkTracker.CurrentChunk != null)
        {
            return playerChunkTracker.CurrentChunkCenter;
        }

        return player == null ? transform.position : player.position;
    }
}
