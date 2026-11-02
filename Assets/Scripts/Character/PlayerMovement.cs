using UnityEngine;

public enum PlayerMovementState
{
    Idle,
    Running,
    Rising,
    Falling
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
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpVelocity = 8f;
    [SerializeField] private bool isHorizontalInputInverted;
    [SerializeField, Min(0f)] private float verticalFacingSpeedThreshold = 10f;
    [SerializeField] private PlayerFacingDirection facingDirection = PlayerFacingDirection.Right;
    [SerializeField] private Vector2 groundCheckOffset = new Vector2(0f, -0.55f);
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.8f, 0.1f);
    [SerializeField] private LayerMask groundLayer;

    public PlayerMovementState CurrentState { get; private set; } = PlayerMovementState.Idle;
    public bool IsHorizontalInputInverted => isHorizontalInputInverted;
    public PlayerFacingDirection FacingDirection => facingDirection;

    public void SetHorizontalInputInverted(bool inverted)
    {
        isHorizontalInputInverted = inverted;
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;

        if (!enabled)
        {
            horizontalInput = 0f;
            jumpRequested = false;
        }
    }

    private Rigidbody2D playerRigidbody;
    private PlayerFacingDirection lastHorizontalFacingDirection = PlayerFacingDirection.Right;
    private float horizontalInput;
    private bool jumpRequested;
    private bool inputEnabled = true;

    private void Awake()
    {
        playerRigidbody = GetComponent<Rigidbody2D>();

        if (verticalFacingSpeedThreshold <= 0f)
        {
            Debug.LogError("Vertical facing speed threshold must be greater than zero.", this);
        }
    }

    private void Update()
    {
        if (!inputEnabled)
        {
            horizontalInput = 0f;
            jumpRequested = false;
            return;
        }

        horizontalInput = Input.GetAxisRaw("Horizontal");

        if (isHorizontalInputInverted)
        {
            horizontalInput = -horizontalInput;
        }

        if (Input.GetButtonDown("Jump"))
        {
            jumpRequested = true;
        }
    }

    private void FixedUpdate()
    {
        playerRigidbody.velocity = new Vector2(horizontalInput * moveSpeed, playerRigidbody.velocity.y);

        if (jumpRequested && IsGrounded())
        {
            playerRigidbody.velocity = new Vector2(playerRigidbody.velocity.x, jumpVelocity);
        }

        jumpRequested = false;
        UpdateState();
        UpdateFacingDirection();
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

    private void UpdateState()
    {
        if (playerRigidbody.velocity.y > 0.01f)
        {
            SetState(PlayerMovementState.Rising);
            return;
        }

        if (!IsGrounded())
        {
            SetState(PlayerMovementState.Falling);
            return;
        }

        if (Mathf.Abs(horizontalInput) > 0.01f)
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

    private bool IsGrounded()
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
