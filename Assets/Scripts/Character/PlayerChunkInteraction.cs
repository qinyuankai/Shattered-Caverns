using System;
using System.Collections.Generic;
using UnityEngine;

public enum PlayerChunkInteractionState
{
    Normal,
    Removing,
    Placing,
    Rotating
}

public class PlayerChunkInteraction : MonoBehaviour
{
    private const string SolidLayerName = "Solid";
    private const float PositionTolerance = 0.05f;
    private const float QuarterTurnDegrees = 90f;

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
    internal bool IsPlayerFrozen => playerMovement != null && playerMovement.IsFrozen;
    internal event Action InteractionStateChanged;

    private LevelManager levelManager;
    private int solidLayerMask;
    private ChunkController placementSourceChunk;
    private Vector2 placementTargetCenter;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Vector3 originalScale;
    private bool originalActiveState;
    private Vector3 originalPlayerPosition;
    private Vector3 rotationPlayerLocalPosition;
    private CameraController interactionCamera;
    private readonly List<RaycastHit2D> boundaryHits = new List<RaycastHit2D>();

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

    private void OnDisable()
    {
        CancelCurrentInteraction();
    }

    private void Update()
    {
        GameFlowController flow = GameFlowController.Instance;
        if (flow == null || flow.BlocksGameplayInput)
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

        bool isRotating = currentState == PlayerChunkInteractionState.Rotating;
        bool isPlacing = currentState == PlayerChunkInteractionState.Placing;

        if (IsPlayerFrozen != isRotating
            || (isRotating && (selectedChunk == null || !selectedChunk.isActiveAndEnabled))
            || (isPlacing && (levelManager == null || placementSourceChunk == null || !placementSourceChunk.isActiveAndEnabled
                || !levelManager.IsChunkPlaced(placementSourceChunk))))
        {
            CancelInteraction();
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

        if (!isRotating && !isPlacing)
        {
            return;
        }

        float rotationDelta = 0f;
        if (Input.GetKeyDown(KeyCode.Q))
        {
            rotationDelta = QuarterTurnDegrees;
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            rotationDelta = -QuarterTurnDegrees;
        }

        if (rotationDelta != 0f)
        {
            if (isRotating)
            {
                RotateCurrentChunk(rotationDelta);
            }
            else
            {
                placementRotation = Mathf.Repeat(placementRotation + rotationDelta, 360f);
                ApplyPlacementPreview();
            }
        }
        else if (isPlacing && Input.GetKeyDown(KeyCode.R))
        {
            SelectNextAvailableChunk();
        }
    }

    private void TryEnterInteractionState()
    {
        if (playerMovement == null
            || playerChunkTracker == null
            || levelManager == null)
        {
            return;
        }

        ChunkController sourceChunk = playerChunkTracker.CurrentChunk;
        if (sourceChunk == null || !sourceChunk.isActiveAndEnabled || !levelManager.IsChunkPlaced(sourceChunk))
        {
            return;
        }

        if (IsPlayerFrozen)
        {
            BeginRotation(sourceChunk);
            return;
        }

        Vector2 direction = GetFacingDirection();

        if (!HasClearPathToBoundary(sourceChunk, direction))
        {
            return;
        }

        Vector2 sourceCenter = sourceChunk.WorldCenter;
        Vector2 targetCenter = sourceCenter + direction * sourceChunk.GetWorldSizeAlongDirection(direction);

        LevelChunkConfiguration target = levelManager.FindPlacedChunkAt(targetCenter, PositionTolerance);
        if (target != null)
        {
            if (!target.isMovable)
            {
                return;
            }

            selectedChunk = target.chunk;
            selectedChunk.SetBackgroundColor(GetInteractionColor(Color.red));
            FocusCameraForInteraction(sourceCenter, targetCenter);
            SetInteractionState(PlayerChunkInteractionState.Removing);
            return;
        }

        if (levelManager.AvailableChunks.Count == 0)
        {
            return;
        }

        selectedAvailableChunkIndex = 0;
        placementRotation = 0f;
        placementMirrored = playerMovement.IsHorizontalInputInverted;
        placementSourceChunk = sourceChunk;
        placementTargetCenter = targetCenter;
        SelectAvailableChunk(selectedAvailableChunkIndex);
        FocusCameraForInteraction(sourceCenter, targetCenter);
        SetInteractionState(PlayerChunkInteractionState.Placing);
    }

    private void BeginRotation(ChunkController currentChunk)
    {
        selectedChunk = currentChunk;
        SaveSelectedChunkTransform();
        originalPlayerPosition = playerMovement.transform.position;
        rotationPlayerLocalPosition = currentChunk.transform.InverseTransformPoint(originalPlayerPosition);

        if (playerMovement.IsHorizontalInputInverted)
        {
            Vector3 mirroredScale = originalScale;
            mirroredScale.x = -mirroredScale.x;
            PreviewCurrentChunk(originalRotation, mirroredScale);
        }

        selectedChunk.SetBackgroundColor(GetInteractionColor(Color.green));
        SetInteractionState(PlayerChunkInteractionState.Rotating);
    }

    private void RotateCurrentChunk(float rotationDelta)
    {
        Transform chunkTransform = selectedChunk.transform;
        Quaternion nextRotation = Quaternion.AngleAxis(rotationDelta, Vector3.forward) * chunkTransform.rotation;
        PreviewCurrentChunk(nextRotation, chunkTransform.localScale);
    }

    private void PreviewCurrentChunk(Quaternion rotation, Vector3 scale)
    {
        Transform chunkTransform = selectedChunk.transform;
        Vector2 previousChunkCenter = selectedChunk.WorldCenter;

        chunkTransform.rotation = rotation;
        chunkTransform.localScale = scale;
        chunkTransform.position += (Vector3)(previousChunkCenter - selectedChunk.WorldCenter);
        playerMovement.transform.position = chunkTransform.TransformPoint(rotationPlayerLocalPosition);
        levelManager.NotifyChunkLayoutChanged();
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
        if (selectedChunk == null)
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

        Vector2 centerOffset = placementTargetCenter - selectedChunk.WorldCenter;
        selectedTransform.position += (Vector3)centerOffset;
        selectedTransform.gameObject.SetActive(true);
        selectedChunk.SetBackgroundColor(GetInteractionColor(Color.green));
        levelManager.NotifyChunkLayoutChanged();
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
        else if (currentState == PlayerChunkInteractionState.Rotating)
        {
            levelManager.NotifyChunkLayoutChanged();
            ExitInteraction();
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
            || levelManager.FindPlacedChunkAt(placementTargetCenter, PositionTolerance) != null)
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
        bool restoreLayout = currentState == PlayerChunkInteractionState.Placing
            || currentState == PlayerChunkInteractionState.Rotating;

        if (currentState == PlayerChunkInteractionState.Placing)
        {
            RestoreSelectedChunk();
        }
        else if (currentState == PlayerChunkInteractionState.Rotating)
        {
            if (selectedChunk != null)
            {
                selectedChunk.transform.SetPositionAndRotation(originalPosition, originalRotation);
                selectedChunk.transform.localScale = originalScale;
            }

            if (playerMovement != null)
            {
                playerMovement.transform.position = originalPlayerPosition;
            }
        }

        if (restoreLayout && levelManager != null)
        {
            levelManager.NotifyChunkLayoutChanged();
        }

        ExitInteraction();
    }

    private void ExitInteraction()
    {
        if (selectedChunk != null)
        {
            selectedChunk.RestoreBackgroundColor();
        }

        if (interactionCamera != null)
        {
            interactionCamera.RestoreFollowView();
            interactionCamera = null;
        }

        selectedChunk = null;
        placementSourceChunk = null;
        SetInteractionState(PlayerChunkInteractionState.Normal);
    }

    private void SetInteractionState(PlayerChunkInteractionState state)
    {
        currentState = state;
        InteractionStateChanged?.Invoke();
    }

    private void FocusCameraForInteraction(Vector2 sourceCenter, Vector2 targetCenter)
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null || !mainCamera.TryGetComponent(out interactionCamera))
        {
            Debug.LogError("MainCamera with CameraController is not found.", this);
            return;
        }

        interactionCamera.FocusForInteraction((sourceCenter + targetCenter) * 0.5f);
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

    private bool HasClearPathToBoundary(ChunkController currentChunk, Vector2 direction)
    {
        float boundaryDistance = currentChunk.GetBoundaryDistance(
            transform.position,
            direction
        );

        if (boundaryDistance <= 0f || float.IsInfinity(boundaryDistance))
        {
            return false;
        }

        ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(solidLayerMask);
        Physics2D.Linecast(
            transform.position,
            (Vector2)transform.position + direction * boundaryDistance,
            filter,
            boundaryHits
        );

        foreach (RaycastHit2D hit in boundaryHits)
        {
            if (hit.collider.GetComponentInParent<ChunkController>() == currentChunk)
            {
                return false;
            }
        }

        return true;
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
