using UnityEngine;

public enum PlayerMovementState
{
    Idle,
    Running,
    Rising,
    Falling
}

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpVelocity = 8f;
    [SerializeField] private Vector2 groundCheckOffset = new Vector2(0f, -0.55f);
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.8f, 0.1f);
    [SerializeField] private LayerMask groundLayer;

    public PlayerMovementState CurrentState { get; private set; } = PlayerMovementState.Idle;

    private Rigidbody2D playerRigidbody;
    private float horizontalInput;
    private bool jumpRequested;

    private void Awake()
    {
        playerRigidbody = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");

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
