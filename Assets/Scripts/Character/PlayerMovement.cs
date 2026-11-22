using System;
using System.Collections.Generic;
using UnityEngine;

public enum PlayerMovementState
{
    Idle,
    Running,
    Rising,
    Falling,
    Climbing
}

public enum PlayerFacingDirection
{
    Left,
    Right,
    Up,
    Down
}

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    private const float VerticalMovementTolerance = 0.01f;

    [SerializeField] private float moveSpeed = 4.2f;
    [SerializeField] private float jumpVelocity = 15f;
    [SerializeField] private bool isHorizontalInputInverted;
    [SerializeField] private bool isFrozen;
    [SerializeField, Min(0f)] private float verticalFacingSpeedThreshold = 18.5f;
    [SerializeField] private PlayerFacingDirection facingDirection = PlayerFacingDirection.Right;
    [SerializeField] private Vector2 groundCheckOffset = new Vector2(0f, -0.55f);
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.8f, 0.1f);
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private LayerMask updraftLayers;
    [SerializeField, Min(0f)] private float dandelionAirSpeed = 2f;
    [SerializeField, Min(0.01f)] private float dandelionDescentSpeed = 1f;
    [SerializeField, Min(0.01f)] private float climbSpeed = 3f;

    public PlayerMovementState CurrentState { get; private set; } = PlayerMovementState.Idle;
    public bool IsHorizontalInputInverted => isHorizontalInputInverted;
    public bool IsFrozen => isFrozen;
    public bool IsCarryingDandelion => inventory != null && inventory.IsCarryingDandelion;
    public PlayerFacingDirection FacingDirection => facingDirection;
    internal bool HasReceivedMovementInput { get; private set; }
    internal bool HasGroundSupport { get; private set; }
    internal event Action Respawned;

    public void SetHorizontalInputInverted(bool inverted)
    {
        isHorizontalInputInverted = inverted;
    }

    public void SetFrozen(bool frozen)
    {
        isFrozen = frozen;
        SyncFrozenState();
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;

        if (!enabled)
        {
            horizontalInput = 0f;
            verticalInput = 0f;
            jumpRequested = false;
        }
    }

    internal void RespawnAt(Vector3 position)
    {
        SetHorizontalInputInverted(false);
        ClearClimb();
        ResetDandelionFlight();
        HasReceivedMovementInput = false;
        horizontalInputAtRespawn = Input.GetAxisRaw("Horizontal");
        horizontalInput = 0f;
        jumpRequested = false;
        playerRigidbody.velocity = Vector2.zero;
        playerRigidbody.angularVelocity = 0f;
        transform.position = position;
        Physics2D.SyncTransforms();
        HasGroundSupport = false;
        SetFrozen(false);
        waitingForLandingAfterThaw = false;
        skipFirstThawLandingCheck = false;
        UpdateState(HasGroundSupport);
        UpdateFacingDirection();
        if (inventory != null)
        {
            inventory.ReturnCarriedDandelion(position);
        }
        Respawned?.Invoke();
    }

    private Rigidbody2D playerRigidbody;
    private PlayerFacingDirection lastHorizontalFacingDirection = PlayerFacingDirection.Right;
    private float horizontalInput;
    private float verticalInput;
    private float horizontalInputAtRespawn;
    private bool jumpRequested;
    private bool inputEnabled = true;
    private bool isPhysicsFrozen;
    private bool waitingForLandingAfterThaw;
    private bool skipFirstThawLandingCheck;
    private RigidbodyConstraints2D constraintsBeforeFreeze;
    private readonly List<ContactPoint2D> groundContacts = new List<ContactPoint2D>();
    private readonly List<Collider2D> movementOverlaps = new List<Collider2D>();
    private bool isClimbing;
    private bool waitForVineExit;
    private bool isRidingUpdraft;
    private bool isFlightControllingGravity;
    private float gravityScaleBeforeFlight;

    private void Reset()
    {
        GetComponent<Rigidbody2D>().gravityScale = 4.5f;
    }

    private void Awake()
    {
        playerRigidbody = GetComponent<Rigidbody2D>();
        SyncFrozenState();

        if (verticalFacingSpeedThreshold <= 0f)
        {
            Debug.LogError("Vertical facing speed threshold must be greater than zero.", this);
        }
    }

    private void OnDisable()
    {
        ClearClimb();
        ResetDandelionFlight();
    }

    private void Update()
    {
        SyncFrozenState();

        if (!inputEnabled || isFrozen)
        {
            horizontalInput = 0f;
            verticalInput = 0f;
            jumpRequested = false;
            return;
        }

        bool jumpPressed = Input.GetButtonDown("Jump");
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        if (horizontalInputAtRespawn * horizontalInput > 0f && !jumpPressed)
        {
            horizontalInput = 0f;
        }
        else
        {
            horizontalInputAtRespawn = 0f;
        }

        if (isHorizontalInputInverted)
        {
            horizontalInput = -horizontalInput;
        }

        if (jumpPressed)
        {
            jumpRequested = true;
        }

        if (horizontalInput != 0f || jumpRequested)
        {
            HasReceivedMovementInput = true;
        }
    }

    private void FixedUpdate()
    {
        HasGroundSupport = CheckGroundSupport();
        SyncFrozenState();

        if (isFrozen)
        {
            return;
        }

        if (waitingForLandingAfterThaw && !skipFirstThawLandingCheck && HasGroundSupport)
        {
            waitingForLandingAfterThaw = false;
        }
        skipFirstThawLandingCheck = false;

        if (!waitingForLandingAfterThaw)
        {
            ApplyMovement();
        }

        jumpRequested = false;
        UpdateState(HasGroundSupport);
        UpdateFacingDirection();
    }

    private void ApplyMovement()
    {
        Vector2 velocity = playerRigidbody.velocity;
        RefreshMovementTriggers();
        if (ApplyClimbing(ref velocity))
        {
            playerRigidbody.velocity = velocity;
            return;
        }

        bool jumped = jumpRequested && IsWithinJumpRange();
        if (jumped)
        {
            velocity.y = jumpVelocity;
        }

        bool carryingDandelion = IsCarryingDandelion;
        float updraftSpeed = carryingDandelion ? GetUpdraftSpeed() : 0f;
        bool leftUpdraft = isRidingUpdraft && updraftSpeed <= 0f;
        isRidingUpdraft = updraftSpeed > 0f;
        bool isAirborne = !HasGroundSupport || jumped || isRidingUpdraft;
        bool isGliding = carryingDandelion && isAirborne
            && (velocity.y <= 0f || (leftUpdraft && !jumped));

        velocity.x = horizontalInput * (carryingDandelion && isAirborne ? dandelionAirSpeed : moveSpeed);
        if (isRidingUpdraft)
        {
            velocity.y = updraftSpeed;
        }
        else if (isGliding)
        {
            velocity.y = -dandelionDescentSpeed;
        }

        SetFlightGravity(isRidingUpdraft || isGliding);
        playerRigidbody.velocity = velocity;
    }

    private bool ApplyClimbing(ref Vector2 velocity)
    {
        bool inVine = false;
        foreach (Collider2D overlap in movementOverlaps)
        {
            if (overlap.isTrigger && overlap.TryGetComponent(out VineWall vine) && vine.CanClimb)
            {
                inVine = true;
                break;
            }
        }

        if (!inVine)
        {
            ClearClimb();
            return false;
        }
        if (waitForVineExit)
        {
            return false;
        }

        isRidingUpdraft = false;
        isClimbing = !jumpRequested;
        waitForVineExit = jumpRequested;
        SetFlightGravity(isClimbing);
        velocity = new Vector2(horizontalInput * moveSpeed,
            isClimbing ? verticalInput * climbSpeed : jumpVelocity);
        return true;
    }

    private void ClearClimb()
    {
        isClimbing = false;
        waitForVineExit = false;
    }

    private void RefreshMovementTriggers()
    {
        if (bodyCollider == null)
        {
            movementOverlaps.Clear();
            return;
        }

        ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(updraftLayers);
        bodyCollider.OverlapCollider(filter, movementOverlaps);
    }

    private float GetUpdraftSpeed()
    {
        float strongestRiseSpeed = 0f;
        foreach (Collider2D overlap in movementOverlaps)
        {
            if (overlap.isTrigger && overlap.TryGetComponent(out Updraft updraft) && updraft.isActiveAndEnabled)
            {
                strongestRiseSpeed = Mathf.Max(strongestRiseSpeed, updraft.RiseSpeed);
            }
        }

        return strongestRiseSpeed;
    }

    private void ResetDandelionFlight()
    {
        isRidingUpdraft = false;
        if (!isClimbing)
        {
            SetFlightGravity(false);
        }
    }

    private void SetFlightGravity(bool controlsGravity)
    {
        if (controlsGravity == isFlightControllingGravity)
        {
            return;
        }

        if (controlsGravity)
        {
            gravityScaleBeforeFlight = playerRigidbody.gravityScale;
            playerRigidbody.gravityScale = 0f;
        }
        else if (playerRigidbody != null)
        {
            playerRigidbody.gravityScale = gravityScaleBeforeFlight;
        }

        isFlightControllingGravity = controlsGravity;
    }

    private void SyncFrozenState()
    {
        if (isPhysicsFrozen == isFrozen || playerRigidbody == null)
        {
            return;
        }

        horizontalInput = 0f;
        verticalInput = 0f;
        jumpRequested = false;
        playerRigidbody.velocity = Vector2.zero;
        playerRigidbody.angularVelocity = 0f;

        if (isFrozen)
        {
            ClearClimb();
            ResetDandelionFlight();
            constraintsBeforeFreeze = playerRigidbody.constraints;
            playerRigidbody.constraints = RigidbodyConstraints2D.FreezeAll;
        }
        else
        {
            playerRigidbody.constraints = constraintsBeforeFreeze;
            waitingForLandingAfterThaw = true;
            skipFirstThawLandingCheck = true;
        }

        isPhysicsFrozen = isFrozen;
        UpdateState(HasGroundSupport);
    }

    private void UpdateFacingDirection()
    {
        if (Mathf.Abs(horizontalInput) > 0.01f)
        {
            lastHorizontalFacingDirection = horizontalInput > 0f
                ? PlayerFacingDirection.Right
                : PlayerFacingDirection.Left;
        }

        if (Mathf.Abs(playerRigidbody.velocity.y) > verticalFacingSpeedThreshold)
        {
            facingDirection = playerRigidbody.velocity.y > 0f
                ? PlayerFacingDirection.Up
                : PlayerFacingDirection.Down;
            return;
        }

        facingDirection = lastHorizontalFacingDirection;
    }

    private void UpdateState(bool hasGroundSupport)
    {
        if (isFrozen)
        {
            SetState(PlayerMovementState.Idle);
            return;
        }

        if (isClimbing)
        {
            SetState(PlayerMovementState.Climbing);
            return;
        }

        if (playerRigidbody.velocity.y > VerticalMovementTolerance)
        {
            SetState(PlayerMovementState.Rising);
            return;
        }

        if (!hasGroundSupport)
        {
            SetState(PlayerMovementState.Falling);
            return;
        }

        if (!waitingForLandingAfterThaw && Mathf.Abs(horizontalInput) > 0.01f)
        {
            SetState(PlayerMovementState.Running);
            return;
        }

        SetState(PlayerMovementState.Idle);
    }

    private void SetState(PlayerMovementState nextState)
    {
        CurrentState = nextState;
    }

    private bool CheckGroundSupport()
    {
        ContactFilter2D groundFilter = new ContactFilter2D { useTriggers = false };
        groundFilter.SetLayerMask(groundLayer);
        playerRigidbody.GetContacts(groundFilter, groundContacts);
        float bodyCenterY = playerRigidbody.worldCenterOfMass.y;
        foreach (ContactPoint2D contact in groundContacts)
        {
            if (contact.enabled
                && contact.point.y < bodyCenterY
                && Mathf.Abs(contact.normal.y) > Mathf.Abs(contact.normal.x))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsWithinJumpRange()
    {
        Vector2 groundCheckPosition = transform.TransformPoint(groundCheckOffset);
        return Physics2D.OverlapBox(
            groundCheckPosition,
            groundCheckSize,
            transform.eulerAngles.z,
            groundLayer
        ) != null;
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 groundCheckPosition = transform.TransformPoint(groundCheckOffset);
        Gizmos.color = Color.yellow;
        Gizmos.matrix = Matrix4x4.TRS(
            groundCheckPosition,
            transform.rotation,
            Vector3.one
        );
        Gizmos.DrawWireCube(Vector3.zero, groundCheckSize);
        Gizmos.matrix = Matrix4x4.identity;
    }
}
