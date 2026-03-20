using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Top speed when grounded")]
    public float moveSpeed = 8f;
    [Tooltip("How quickly the player reaches max speed")]
    public float acceleration = 60f;
    [Tooltip("How quickly the player slows down when no input")]
    public float deceleration = 40f;
    [Tooltip("Multiplier applied to acceleration while airborne")]
    public float airControlMultiplier = 0.4f;

    [Header("Jumping")]
    public float jumpForce = 7f;
    [Tooltip("Extra gravity applied while falling for snappier feel")]
    public float fallGravityMultiplier = 2.5f;
    [Tooltip("Extra gravity when jump button released early (short hop)")]
    public float lowJumpMultiplier = 2f;
    [Tooltip("How long after walking off a ledge you can still jump")]
    public float coyoteTime = 0.12f;
    [Tooltip("How early before landing a jump press is buffered")]
    public float jumpBufferTime = 0.15f;

    [Header("Ground Check")]
    [Tooltip("Layer(s) considered as ground")]
    public LayerMask groundMask = ~0;
    [Tooltip("Distance below the capsule to detect ground")]
    public float groundCheckDistance = 0.1f;

    [Header("Camera")]
    public Transform cameraTransform;
    public float mouseSensitivity = 2f;

    // ── private state ────────────────────────────────────────────────────────
    private Rigidbody rb;
    private CapsuleCollider capsule;

    private float yaw;

    private bool isGrounded;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private bool jumpHeld;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        // Lock and hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Initialise yaw to current facing direction
        yaw = transform.eulerAngles.y;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        HandleCameraRotation();
        HandleJumpInput();
    }

    void FixedUpdate()
    {
        CheckGround();
        HandleMovement();
        HandleJumpPhysics();
        TryJump();
    }

    // ── camera ───────────────────────────────────────────────────────────────

    void HandleCameraRotation()
    {
        float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;

        yaw += mouseX;

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.identity;
    }

    // ── ground check ─────────────────────────────────────────────────────────

    void CheckGround()
    {
        float radius = capsule != null ? capsule.radius * 0.95f : 0.3f;
        float halfHeight = capsule != null ? capsule.height * 0.5f : 1f;
        Vector3 bottom = transform.position + Vector3.down * (halfHeight - radius);

        isGrounded = Physics.SphereCast(
            bottom + Vector3.up * radius,
            radius,
            Vector3.down,
            out _,
            radius + groundCheckDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        if (isGrounded)
            coyoteTimer = coyoteTime;
        else
            coyoteTimer -= Time.fixedDeltaTime;
    }

    // ── movement ─────────────────────────────────────────────────────────────

    void HandleMovement()
    {
        float h = Input.GetAxisRaw("Horizontal"); // A/D
        float v = Input.GetAxisRaw("Vertical");   // W/S

        Vector3 input = new Vector3(-h, 0f, -v);
        if (input.sqrMagnitude > 1f) input.Normalize();

        // Translate input relative to player facing direction
        Vector3 desiredVelocity = transform.TransformDirection(input) * moveSpeed;

        Vector3 currentHorizontal = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float control = isGrounded ? 1f : airControlMultiplier;

        float accel = input.sqrMagnitude > 0.01f ? acceleration : deceleration;
        Vector3 newHorizontal = Vector3.MoveTowards(
            currentHorizontal,
            desiredVelocity,
            accel * control * Time.fixedDeltaTime
        );

        rb.linearVelocity = new Vector3(newHorizontal.x, rb.linearVelocity.y, newHorizontal.z);
    }

    // ── jump input (buffered in Update) ──────────────────────────────────────

    void HandleJumpInput()
    {
        if (Input.GetButtonDown("Jump"))
            jumpBufferTimer = jumpBufferTime;
        else
            jumpBufferTimer -= Time.deltaTime;

        jumpHeld = Input.GetButton("Jump");
    }

    // ── jump execution & variable height ─────────────────────────────────────

    void TryJump()
    {
        bool canJump = coyoteTimer > 0f && jumpBufferTimer > 0f;
        if (!canJump) return;

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
    }

    void HandleJumpPhysics()
    {
        if (rb.linearVelocity.y < 0f)
        {
            // Falling — apply extra gravity
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallGravityMultiplier - 1f) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0f && !jumpHeld)
        {
            // Rising but button released — short hop
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1f) * Time.fixedDeltaTime;
        }
    }
}
