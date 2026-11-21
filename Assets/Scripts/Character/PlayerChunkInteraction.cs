using UnityEngine;

public enum PlayerChunkInteractionState
{
    Normal,
    Removing,
    Placing
}

public class PlayerChunkInteraction : MonoBehaviour
{
    private const string SolidLayerName = "Solid";
    private const float PositionTolerance = 0.05f;

    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerChunkTracker playerChunkTracker;
    [SerializeField] private PlayerChunkInteractionState currentState;
    [SerializeField] private ChunkController selectedChunk;
    [SerializeField] private int selectedAvailableChunkIndex;
    [SerializeField] private float placementRotation;
    [SerializeField] private bool placementMirrored;
    [SerializeField, Range(0f, 1f)] private float interactionColorAlpha = 0.2f;

    public PlayerChunkInteractionState CurrentState => currentState;
    public ChunkController SelectedChunk => selectedChunk;

    private LevelManager levelManager;
    private int solidLayerMask;
    private float previousTimeScale;
    private Vector2 operationDirection;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Vector3 originalScale;
    private bool originalActiveState;
    private Camera mainCamera;
    private CameraController cameraController;
    private Vector3 originalCameraPosition;
    private float originalCameraOrthographicSize;
    private bool originalCameraControllerEnabled;
    private bool cameraStateSaved;

    private void Awake()
    {
        solidLayerMask = LayerMask.GetMask(SolidLayerName);

        if (playerMovement == null)
        {
            Debug.LogError("PlayerMovement is not assigned.", this);
        }

        if (playerChunkTracker == null)
        {
            Debug.LogError("PlayerChunkTracker is not assigned.", this);
        }

        levelManager = FindObjectOfType<LevelManager>();

        if (levelManager == null)
        {
            Debug.LogError("LevelManager is not found.", this);
        }
    }

    private void Update()
    {
        if (GameFlowController.Instance != null && GameFlowController.Instance.BlocksGameplayInput)
        {
            return;
        }

        if (currentState == PlayerChunkInteractionState.Normal)
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                TryEnterInteractionState();
            }

            return;
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            ConfirmInteraction();
            return;
        }

        if (Input.GetKeyDown(KeyCode.X))
        {
            CancelInteraction();
            return;
        }

        if (currentState == PlayerChunkInteractionState.Placing)
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                placementRotation = Mathf.Repeat(placementRotation + 90f, 360f);
                ApplyPlacementPreview();
            }
            else if (Input.GetKeyDown(KeyCode.E))
            {
                SelectNextAvailableChunk();
            }
        }
    }

    private void TryEnterInteractionState()
    {
        if (playerMovement == null
            || playerChunkTracker == null
            || levelManager == null
            || playerChunkTracker.CurrentChunk == null)
        {
            return;
        }

        operationDirection = GetFacingDirection();

        if (!HasClearPathToBoundary(operationDirection))
        {
            return;
        }

        Vector2 targetCenter = GetTargetCenter();

        if (levelManager.TryGetPlacedMovableChunkAt(
                targetCenter,
                PositionTolerance,
                out ChunkController removableChunk))
        {
            selectedChunk = removableChunk;
            selectedChunk.SetBackgroundColor(GetInteractionColor(Color.red));
            currentState = PlayerChunkInteractionState.Removing;
            FocusCameraForInteraction(targetCenter);
            PauseTime();
            return;
        }

        if (levelManager.IsChunkPositionOccupiedAt(targetCenter, PositionTolerance)
            || levelManager.AvailableChunks.Count == 0)
        {
            return;
        }

        selectedAvailableChunkIndex = 0;
        placementRotation = 0f;
        placementMirrored = playerMovement.IsHorizontalInputInverted;
        SelectAvailableChunk(selectedAvailableChunkIndex);
        currentState = PlayerChunkInteractionState.Placing;
        FocusCameraForInteraction(targetCenter);
        PauseTime();
    }

    private void SelectNextAvailableChunk()
    {
        RestoreSelectedChunk();

        if (levelManager.AvailableChunks.Count == 0)
        {
            CancelInteraction();
            return;
        }

        selectedAvailableChunkIndex =
            (selectedAvailableChunkIndex + 1) % levelManager.AvailableChunks.Count;
        SelectAvailableChunk(selectedAvailableChunkIndex);
    }

    private void SelectAvailableChunk(int index)
    {
        selectedChunk = levelManager.AvailableChunks[index];
        SaveSelectedChunkTransform();
        ApplyPlacementPreview();
    }

    private void ApplyPlacementPreview()
    {
        if (selectedChunk == null || playerChunkTracker.CurrentChunk == null)
        {
            return;
        }

        Transform selectedTransform = selectedChunk.transform;
        selectedTransform.rotation = Quaternion.Euler(0f, 0f, placementRotation);

        Vector3 previewScale = originalScale;
        previewScale.x = placementMirrored
            ? -Mathf.Abs(originalScale.x)
            : Mathf.Abs(originalScale.x);
        selectedTransform.localScale = previewScale;

        Vector2 targetCenter = GetTargetCenter();
        Vector2 centerOffset = targetCenter - selectedChunk.WorldCenter;
        selectedTransform.position += (Vector3)centerOffset;
        selectedTransform.gameObject.SetActive(true);
        selectedChunk.SetBackgroundColor(GetInteractionColor(Color.green));
    }

    private void ConfirmInteraction()
    {
        if (currentState == PlayerChunkInteractionState.Removing)
        {
            ConfirmRemoving();
            return;
        }

        if (currentState == PlayerChunkInteractionState.Placing)
        {
            ConfirmPlacing();
        }
    }

    private void ConfirmRemoving()
    {
        if (selectedChunk != null)
        {
            levelManager.TrySetChunkPlaced(selectedChunk.ChunkId, false);
        }

        ExitInteraction();
    }

    private void ConfirmPlacing()
    {
        if (selectedChunk == null
            || levelManager.IsChunkPositionOccupiedAt(
                GetTargetCenter(),
                PositionTolerance))
        {
            CancelInteraction();
            return;
        }

        if (!levelManager.TrySetChunkPlaced(selectedChunk.ChunkId, true))
        {
            CancelInteraction();
            return;
        }

        ExitInteraction();
    }

    public void CancelCurrentInteraction()
    {
        if (currentState != PlayerChunkInteractionState.Normal) CancelInteraction();
    }

    private void CancelInteraction()
    {
        if (currentState == PlayerChunkInteractionState.Placing)
        {
            RestoreSelectedChunk();
        }

        ExitInteraction();
    }

    private void ExitInteraction()
    {
        if (selectedChunk != null)
        {
            selectedChunk.RestoreBackgroundColor();
        }

        RestoreCamera();
        Time.timeScale = previousTimeScale;
        playerMovement.SetInputEnabled(true);
        currentState = PlayerChunkInteractionState.Normal;
        selectedChunk = null;
    }

    private void PauseTime()
    {
        previousTimeScale = Time.timeScale;
        playerMovement.SetInputEnabled(false);
        Time.timeScale = 0f;
    }

    private void FocusCameraForInteraction(Vector2 targetCenter)
    {
        mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError("MainCamera is not found.", this);
            return;
        }

        if (!cameraStateSaved)
        {
            cameraController = mainCamera.GetComponent<CameraController>();
            originalCameraPosition = mainCamera.transform.position;
            originalCameraOrthographicSize = mainCamera.orthographicSize;
            originalCameraControllerEnabled = cameraController != null
                && cameraController.enabled;
            cameraStateSaved = true;
        }

        if (cameraController != null)
        {
            cameraController.enabled = false;
        }

        Vector2 currentChunkCenter = playerChunkTracker.CurrentChunk.WorldCenter;
        Vector2 midpoint = (currentChunkCenter + targetCenter) * 0.5f;
        mainCamera.orthographicSize = 12f;
        mainCamera.transform.position = new Vector3(
            midpoint.x,
            midpoint.y,
            originalCameraPosition.z
        );
    }

    private void RestoreCamera()
    {
        if (!cameraStateSaved)
        {
            return;
        }

        if (mainCamera != null)
        {
            mainCamera.transform.position = originalCameraPosition;
            mainCamera.orthographicSize = originalCameraOrthographicSize;
        }

        if (cameraController != null)
        {
            cameraController.enabled = originalCameraControllerEnabled;
        }

        cameraStateSaved = false;
    }

    private void SaveSelectedChunkTransform()
    {
        originalPosition = selectedChunk.transform.position;
        originalRotation = selectedChunk.transform.rotation;
        originalScale = selectedChunk.transform.localScale;
        originalActiveState = selectedChunk.gameObject.activeSelf;
    }

    private void RestoreSelectedChunk()
    {
        if (selectedChunk == null)
        {
            return;
        }

        selectedChunk.transform.SetPositionAndRotation(
            originalPosition,
            originalRotation
        );
        selectedChunk.transform.localScale = originalScale;
        selectedChunk.gameObject.SetActive(originalActiveState);
        selectedChunk.RestoreBackgroundColor();
    }

    private bool HasClearPathToBoundary(Vector2 direction)
    {
        ChunkController currentChunk = playerChunkTracker.CurrentChunk;
        float boundaryDistance = currentChunk.GetBoundaryDistance(
            transform.position,
            direction
        );

        if (boundaryDistance <= 0f || float.IsInfinity(boundaryDistance))
        {
            return false;
        }

        RaycastHit2D hit = Physics2D.Linecast(
            transform.position,
            (Vector2)transform.position + direction * boundaryDistance,
            solidLayerMask
        );

        return hit.collider == null;
    }

    private Vector2 GetTargetCenter()
    {
        ChunkController currentChunk = playerChunkTracker.CurrentChunk;
        float chunkSize = currentChunk.GetWorldSizeAlongDirection(operationDirection);

        return currentChunk.WorldCenter + operationDirection * chunkSize;
    }

    private Vector2 GetFacingDirection()
    {
        switch (playerMovement.FacingDirection)
        {
            case PlayerFacingDirection.Left:
                return Vector2.left;
            case PlayerFacingDirection.Right:
                return Vector2.right;
            case PlayerFacingDirection.Up:
                return Vector2.up;
            case PlayerFacingDirection.Down:
                return Vector2.down;
            default:
                return Vector2.right;
        }
    }

    private Color GetInteractionColor(Color color)
    {
        color.a = interactionColorAlpha;
        return color;
    }
}
