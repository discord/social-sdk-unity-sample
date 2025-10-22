using UnityEngine;

/// PlayerController handles 2D top-down player movement using keyboard input.
/// Movement is enabled by default but can be toggled on/off.
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    
    [Header("Movement Type")]
    [Tooltip("If true, player can move diagonally. If false, restricts to 4-directional movement.")]
    [SerializeField] private bool allowDiagonalMovement = true;

    private Rigidbody2D rb;
    private Vector2 movementInput;
    private Vector2 lastNonZeroDirection = Vector2.down;
    private bool canMove = true;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        
        if (rb == null)
        {
            Debug.LogError("PlayerController requires a Rigidbody2D component!");
        }
    }

    void Update()
    {
        if (!canMove)
        {
            movementInput = Vector2.zero;
            return;
        }

        // Get input from keyboard (WASD or Arrow Keys)
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        // Store raw input
        movementInput = new Vector2(horizontal, vertical);

        // Handle 4-directional movement if diagonal movement is disabled
        if (!allowDiagonalMovement && movementInput.sqrMagnitude > 0)
        {
            // Prioritize the axis with larger input
            if (Mathf.Abs(horizontal) > Mathf.Abs(vertical))
            {
                movementInput = new Vector2(horizontal, 0);
            }
            else
            {
                movementInput = new Vector2(0, vertical);
            }
        }

        // Track last direction for potential future use (animations, facing direction, etc.)
        if (movementInput.sqrMagnitude > 0)
        {
            lastNonZeroDirection = movementInput.normalized;
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        // Apply movement
        Vector2 movement = movementInput.normalized * moveSpeed;
        rb.linearVelocity = movement;
    }

    /// Enable or disable player movement
    public void SetCanMove(bool canMove)
    {
        this.canMove = canMove;
        if (!canMove)
        {
            // Stop movement when disabled
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }
    }

    /// Get the current movement input (for UI or other systems)
    public Vector2 GetMovementInput()
    {
        return movementInput;
    }

    /// Get the last non-zero direction the player was facing
    public Vector2 GetFacingDirection()
    {
        return lastNonZeroDirection;
    }

    /// Set the player's movement speed
    public void SetMoveSpeed(float speed)
    {
        moveSpeed = Mathf.Max(0, speed);
    }

    /// Get the player's current movement speed
    public float GetMoveSpeed()
    {
        return moveSpeed;
    }
}

