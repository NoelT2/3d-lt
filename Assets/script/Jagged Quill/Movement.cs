using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class Movement : MonoBehaviour
{
    private CharacterController controller;
    private Vector2 moveInput;

    [Header("Movement Speeds")]
    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float gravity = -19.81f;
    [Tooltip("Gravity is multiplied by this while falling (velocity.y < 0). 1 = symmetric jump, 1.5-2.5 = snappy, less floaty fall.")]
    [SerializeField] private float fallGravityMultiplier = 1.8f;
    [Tooltip("Terminal fall speed (m/s). 0 = no limit.")]
    [SerializeField] private float maxFallSpeed = 80f;
    [SerializeField] private float jumpHeight = 2f;

    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed = 25f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1.5f;
    [Tooltip("Downward speed you start with right after a dash ends, so gravity takes over immediately instead of hovering.")]
    [SerializeField] private float postDashFallSpeed = 6f;

    [Header("Super Dash (after hitting a wall)")]
    [SerializeField] private bool superDashEnabled = true;
    [Tooltip("Minimum speed INTO a wall for the hit to count (walking into walls doesn't arm it).")]
    [SerializeField] private float superDashWallMinSpeed = 10f;
    [Tooltip("Seconds after a wall hit during which Shift is a super dash. After that it is the normal dash again.")]
    [SerializeField] private float superDashWindow = 3f;
    [SerializeField] private float superDashDuration = 0.55f;
    [SerializeField] private float superDashStartSpeed = 30f;
    [SerializeField] private float superDashMaxSpeed = 110f;
    [Tooltip("1 = linear speed build-up, 2+ = slow start and a harder kick at the end.")]
    [SerializeField, Range(1f, 4f)] private float superDashBuildupPower = 2f;
    [Tooltip("Fraction of the final speed kept as launch momentum when the super dash ends.")]
    [SerializeField, Range(0f, 1f)] private float superDashCarry = 0.6f;
    [SerializeField] private float superDashAimRange = 200f;
    [Tooltip("Highest upward angle (degrees) a super dash can aim. Downward is unlimited. Stops endless climbing.")]
    [SerializeField, Range(0f, 89f)] private float superDashMaxUpPitch = 55f;
    [Tooltip("Max super dashes in a row without touching the ground (0 = unlimited).")]
    [SerializeField] private int superDashMaxChain = 0;

    [Header("Camera Reference")]
    [SerializeField] private Transform playerCamera;

    [Header("Slope Handling")]
    [SerializeField] private float maxJumpableSlopeAngle = 45f;
    [SerializeField] private float slopeSlideSpeed = 8f;
    private Vector3 lastGroundNormal = Vector3.up;

    private Vector3 velocity;
    private bool isGrounded;

    private bool isDashing = false;
    private float dashTimeRemaining = 0f;
    private float nextDashTime = 0f;
    private Vector3 dashDirection;
    private float currentSlopeAngle;

    // Super dash state
    private bool isSuperDashing;
    private float activeDashDuration;
    private float lastDashSpeed;
    private float superDashReadyUntil = -1f;
    private int superDashChain;
    private Vector3 currentMoveVelocity; // velocity of the Move() call in progress (for wall-hit speed)

    public bool SuperDashReady => superDashEnabled && Time.time <= superDashReadyUntil;
    public bool IsSuperDashing => isSuperDashing;
    /// <summary>1 right after a wall hit, falling to 0 as the window runs out (for UI).</summary>
    public float SuperDashWindow01 =>
        SuperDashReady ? Mathf.Clamp01((superDashReadyUntil - Time.time) / Mathf.Max(0.01f, superDashWindow)) : 0f;

    [SerializeField] private PlayerStats stats;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        if (stats == null) stats = GetComponent<PlayerStats>();

        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;
    }
    
    void Update()
    {
        if (PauseMenuController.InputBlocked) return; // pause menu
        isGrounded = controller.isGrounded;
        if (isGrounded) superDashChain = 0;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        Vector3 groundNormal = isGrounded ? lastGroundNormal : Vector3.up;
        currentSlopeAngle = Vector3.Angle(groundNormal, Vector3.up);


        bool onSteepSlope = isGrounded && currentSlopeAngle > maxJumpableSlopeAngle;

        if (onSteepSlope)
        {
            Vector3 slideDir = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
            velocity.x = slideDir.x * slopeSlideSpeed;
            velocity.z = slideDir.z * slopeSlideSpeed;
            velocity.y = -2f;
        }
        else if (isGrounded)
        {
            velocity.x = 0f;
            velocity.z = 0f;
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded && !onSteepSlope)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        CollisionFlags frameFlags = CollisionFlags.None;

        Vector3 move = Vector3.zero;
        if (!isDashing && Keyboard.current != null)
        {
            float moveX = 0f;
            float moveZ = 0f;

            if (Keyboard.current.wKey.isPressed) moveZ = 1f;
            if (Keyboard.current.sKey.isPressed) moveZ = -1f;
            if (Keyboard.current.aKey.isPressed) moveX = -1f;
            if (Keyboard.current.dKey.isPressed) moveX = 1f;

            move = transform.right * moveX + transform.forward * moveZ;

            // While sliding, don't let normal walk input fully cancel the downhill push.
            if (!onSteepSlope)
            {
                float speedMult = stats != null ? stats.MoveSpeedMultiplier : 1f;
                currentMoveVelocity = move * walkSpeed * speedMult;
                frameFlags |= controller.Move(move * walkSpeed * speedMult * Time.deltaTime);
            }

            if (Keyboard.current.leftShiftKey.wasPressedThisFrame && Time.time >= nextDashTime)
            {
                StartDash();
            }
        }

        if (isDashing)
        {
            dashTimeRemaining -= Time.deltaTime;

            float currentDashSpeed = dashSpeed;
            if (isSuperDashing)
            {
                // Momentum build-up: speed ramps from start to max over the dash.
                float t = 1f - Mathf.Clamp01(dashTimeRemaining / Mathf.Max(0.01f, activeDashDuration));
                currentDashSpeed = Mathf.Lerp(superDashStartSpeed, superDashMaxSpeed, Mathf.Pow(t, superDashBuildupPower));
                lastDashSpeed = currentDashSpeed;
            }

            currentMoveVelocity = dashDirection * currentDashSpeed;
            controller.Move(dashDirection * currentDashSpeed * Time.deltaTime);

            if (dashTimeRemaining <= 0f)
            {
                isDashing = false;

                if (isSuperDashing)
                {
                    // Launch: keep the built-up speed as momentum (gravity takes over from here).
                    isSuperDashing = false;
                    velocity = dashDirection * lastDashSpeed * superDashCarry;
                }
                else
                {
                    // Start falling right away (old code zeroed this, so you hung in the air before gravity built up).
                    velocity = new Vector3(0f, -postDashFallSpeed, 0f);
                }
            }
        }
        else
        {
            if (!onSteepSlope)
            {
                float g = velocity.y < 0f ? gravity * fallGravityMultiplier : gravity;
                velocity.y += g * Time.deltaTime;
                if (maxFallSpeed > 0f && velocity.y < -maxFallSpeed) velocity.y = -maxFallSpeed;
            }
            currentMoveVelocity = velocity;
            frameFlags |= controller.Move(velocity * Time.deltaTime);
        }

        if (!isGrounded && (frameFlags & CollisionFlags.Sides) != 0 && velocity.y > 0f)
        {
            velocity.y = 0f;
        }

        // Hits caused by other scripts' Move() calls (grapple, dive) must not use this frame's velocity.
        currentMoveVelocity = Vector3.zero;
    }

    private void StartDash()
    {
        if (SuperDashReady && (superDashMaxChain <= 0 || superDashChain < superDashMaxChain))
        {
            StartSuperDash();
            return;
        }

        isDashing = true;
        isSuperDashing = false;
        activeDashDuration = dashDuration;
        lastDashSpeed = dashSpeed;
        dashTimeRemaining = dashDuration;
        float cooldownMult = stats != null ? stats.DashCooldownMultiplier : 1f;
        nextDashTime = Time.time + dashCooldown * Mathf.Max(0.1f, cooldownMult);

        if (playerCamera != null)
        {
            dashDirection = playerCamera.forward;
        }
        else
        {
            dashDirection = transform.forward;
        }
        
        dashDirection.y = Mathf.Clamp(dashDirection.y, -0.2f, 0.5f);
        dashDirection = dashDirection.normalized;

        GameEvents.RaiseDash();
    }

    private void StartSuperDash()
    {
        isDashing = true;
        isSuperDashing = true;
        activeDashDuration = Mathf.Max(0.05f, superDashDuration);
        dashTimeRemaining = activeDashDuration;
        lastDashSpeed = superDashStartSpeed;

        superDashReadyUntil = -1f;   // the window is spent; hitting another wall arms it again
        superDashChain++;

        float cooldownMult = stats != null ? stats.DashCooldownMultiplier : 1f;
        nextDashTime = Time.time + dashCooldown * Mathf.Max(0.1f, cooldownMult);

        dashDirection = GetSuperDashAimDirection();
        velocity = Vector3.zero;

        GameEvents.RaiseDash();
        Debug.Log("Super dash!");
    }

    // Aims at whatever the crosshair is pointing at (not just the camera's forward), with an upward-pitch limit.
    private Vector3 GetSuperDashAimDirection()
    {
        Transform cam = playerCamera != null ? playerCamera : transform;
        Vector3 center = transform.position + controller.center;
        Vector3 aimPoint = cam.position + cam.forward * superDashAimRange;

        RaycastHit[] hits = Physics.RaycastAll(cam.position, cam.forward, superDashAimRange, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform.root == transform.root) continue; // not ourselves
            if (h.distance < best)
            {
                best = h.distance;
                aimPoint = h.point;
            }
        }

        Vector3 dir = aimPoint - center;
        if (dir.sqrMagnitude < 4f) dir = cam.forward; // aiming at something right next to us
        dir.Normalize();

        float maxUp = Mathf.Sin(superDashMaxUpPitch * Mathf.Deg2Rad);
        if (dir.y > maxUp)
        {
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            flat = flat.sqrMagnitude > 0.0001f ? flat.normalized : new Vector3(cam.forward.x, 0f, cam.forward.z).normalized;
            dir = (flat * Mathf.Cos(superDashMaxUpPitch * Mathf.Deg2Rad) + Vector3.up * maxUp).normalized;
        }

        return dir;
    }

    /// <summary>
    /// Called when the player slams into a wall (walking/dashing here, launches and grapples from GrappleController).
    /// Resets the dash and opens the super-dash window; a super dash that hits a wall stops like a wall hit.
    /// </summary>
    public void NotifyWallHit(Vector3 wallNormal, float speedIntoWall)
    {
        if (!superDashEnabled || speedIntoWall < superDashWallMinSpeed) return;

        if (isSuperDashing)
        {
            Vector3 v = dashDirection * lastDashSpeed;
            v = Vector3.ProjectOnPlane(v, wallNormal) * 0.2f + wallNormal * 2f;
            velocity = new Vector3(v.x, Mathf.Min(v.y, 0f), v.z);

            isDashing = false;
            isSuperDashing = false;
            dashTimeRemaining = 0f;
        }

        superDashReadyUntil = Time.time + superDashWindow;
        nextDashTime = 0f;   // dash is reset
    }

    // Gravity strength (positive) so other scripts can match it.
    public float GravityMagnitude => Mathf.Abs(gravity);

    // Clears stored jump/fall velocity and any dash in progress (used when the dive takes over).
    public void CancelMomentum()
    {
        velocity = Vector3.zero;
        isDashing = false;
        isSuperDashing = false;
        dashTimeRemaining = 0f;
    }

    public bool TryStartDashFromGrapple()
    {
        if (isDashing ||
            Time.time < nextDashTime)
        {
            return false;
        }

        StartDash();
        return true;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.normal.y > 0.1f)
        {
            lastGroundNormal = hit.normal;
        }

        // Wall hit: kill the horizontal airborne momentum into the wall and let the super dash check it.
        if (Mathf.Abs(hit.normal.y) < 0.7f)
        {
            float into = -Vector3.Dot(currentMoveVelocity, hit.normal);
            if (into > 0f) NotifyWallHit(hit.normal, into);

            float d = velocity.x * hit.normal.x + velocity.z * hit.normal.z;
            if (d < 0f)
            {
                velocity.x -= hit.normal.x * d;
                velocity.z -= hit.normal.z * d;
            }
        }
    }
}