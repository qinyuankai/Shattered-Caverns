using UnityEngine;

[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(20)]
public class CameraController : MonoBehaviour
{
    [SerializeField] private PlayerChunkTracker playerChunkTracker;
    [SerializeField] private Transform player;
    [SerializeField, Min(0f)] private float smoothTime = 0.2f;
    [SerializeField] private float cameraZ = -10f;
    [SerializeField, Min(0.01f)] private float interactionSize = 12f;

    private Camera controlledCamera;
    private Vector3 movementVelocity;
    private bool isFocusedForInteraction;
    private Vector3 followPositionBeforeInteraction;
    private float followSizeBeforeInteraction;

    private void Awake()
    {
        controlledCamera = GetComponent<Camera>();

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
        if (isFocusedForInteraction || playerChunkTracker == null)
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

    internal void FocusForInteraction(Vector2 targetPosition)
    {
        if (!isFocusedForInteraction)
        {
            followPositionBeforeInteraction = transform.position;
            followSizeBeforeInteraction = controlledCamera.orthographicSize;
            movementVelocity = Vector3.zero;
            isFocusedForInteraction = true;
        }

        controlledCamera.orthographicSize = interactionSize;
        transform.position = new Vector3(targetPosition.x, targetPosition.y, followPositionBeforeInteraction.z);
    }

    internal void RestoreFollowView()
    {
        if (!isFocusedForInteraction)
        {
            return;
        }

        transform.position = followPositionBeforeInteraction;
        controlledCamera.orthographicSize = followSizeBeforeInteraction;
        isFocusedForInteraction = false;
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
