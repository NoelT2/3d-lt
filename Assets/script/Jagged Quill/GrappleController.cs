using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class GrappleController : MonoBehaviour
{
    public enum HookState
    {
        Ready,
        Attached,
        Reloading
    }

    [Header("References")]
    [SerializeField] private Transform playerCamera;
    [SerializeField] private Transform grapplePoint;
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Movement movement;

    [Header("Grapple Hit Settings")]
    [SerializeField] private float maxGrappleDistance = 30f;

    // Keep this field and your current assigned mask unchanged.
    [SerializeField] private LayerMask grappleHitMask;

    [Header("Grapple Pull")]
    [SerializeField] private float launchSpeed = 30f;
    [SerializeField] private float pullAcceleration = 90f;
    [SerializeField] private float maxGrappleSpeed = 35f;

    [Tooltip("Distance from the anchor where inward velocity is removed.")]
    [SerializeField] private float pullStopDistance = 0.75f;

    [Header("Rope Tension")]
    [SerializeField] private float returnAcceleration = 180f;
    [SerializeField] private float outwardDamping = 12f;
    [SerializeField] private float maxOutwardSpeed = 12f;

    [Header("Vertical Control")]
    [SerializeField] private float maxUpwardLaunchSpeed = 8f;

    [Header("Grapple Reload")]
    [SerializeField] private float reloadTime = 0.9f;

    [Header("Character Controller")]
    [SerializeField] private float gravity = 20f;

    [Header("Momentum")]
    [Tooltip("How quickly momentum fades after landing.")]
    [SerializeField] private float landingMomentumDecay = 3f;

    [Header("Wall Momentum Lock")]
    [SerializeField] private float wallMomentumDisableDelay = 0.25f;
    private float wallContactTimer;
    private bool wallMomentumDisabled;
    private Vector3 grappleSurfaceNormal;

    private CharacterController controller;
    private Camera playerCameraComponent;

    private HookState state = HookState.Ready;

    private Vector3 grappleTarget;
    private Vector3 motionVelocity;
    private Vector3 landingMomentum;

    private float reloadTimer;
    private bool isFlying;

    private bool hitGrappleWallThisStep;
    private Vector3 grappleWallHitNormal;

    public bool IsGrappling =>
        state == HookState.Attached;

    public HookState State => state;
    public Vector3 Anchor => grappleTarget;

    public float ReloadProgress01
    {
        get
        {
            if (state == HookState.Ready)
                return 1f;

            if (reloadTime <= 0f)
                return 1f;

            if (state == HookState.Reloading)
            {
                return 1f -
                       Mathf.Clamp01(reloadTimer / reloadTime);
            }

            return 0f;
        }
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }

        if (playerCamera != null)
        {
            playerCameraComponent =
                playerCamera.GetComponent<Camera>();
        }

        if (playerCameraComponent == null)
        {
            playerCameraComponent = Camera.main;
        }

        if (movement == null)
        {
            movement = GetComponent<Movement>();
        }

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = true;
            lineRenderer.enabled = false;
        }
    }

    private void Update()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.eKey.wasPressedThisFrame &&
                state == HookState.Ready)
            {
                FireGrapple();
            }
            if (Keyboard.current.eKey.wasReleasedThisFrame &&
                state == HookState.Attached)
            {
                ReleaseGrapple();
            }
            if (isFlying &&
                Keyboard.current.leftShiftKey.wasPressedThisFrame)
            {
                CancelGrappleWithDash();
            }
        }
        UpdateGrappleLine();
    }

    private void FixedUpdate()
    {
        float deltaTime = Time.fixedDeltaTime;

        if (state == HookState.Attached)
        {
            ApplyGrappleMotion(deltaTime);
        }

        if (isFlying)
        {
            ApplyReleasedMotion(deltaTime);
        }

        if (state == HookState.Reloading)
        {
            reloadTimer -= deltaTime;

            if (reloadTimer <= 0f)
            {
                reloadTimer = 0f;
                state = HookState.Ready;
            }
        }

        ApplyLandingMomentum(deltaTime);
    }

    private void FireGrapple()
    {
        if (playerCameraComponent == null)
            return;

        Ray ray = new Ray(
            playerCameraComponent.transform.position,
            playerCameraComponent.transform.forward
        );

        // The hit mask is intentionally unchanged.
        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                maxGrappleDistance,
                grappleHitMask,
                QueryTriggerInteraction.Ignore))
        {
            Debug.Log("Grapple missed.");
            return;
        }

        grappleTarget = hit.point;

        grappleSurfaceNormal = hit.normal;
        wallContactTimer = 0f;
        wallMomentumDisabled = false;

        Vector3 origin = GetGrapplePhysicsOrigin();

        Vector3 toAnchor = grappleTarget - origin;

        if (toAnchor.sqrMagnitude <= 0.001f)
            return;

        toAnchor.Normalize();

        // Preserve sideways momentum while adding launch momentum
        // toward the grapple point.
        Vector3 currentVelocity = controller.velocity;

        Vector3 sidewaysVelocity =
            currentVelocity -
            Vector3.Dot(currentVelocity, toAnchor) *
            toAnchor;

        Vector3 launchVelocity =
            sidewaysVelocity +
            toAnchor * launchSpeed;
        // Prevent looking upward at a high grapple point
        // from creating an excessive vertical launch.
        launchVelocity.y = Mathf.Min(
            launchVelocity.y,
            maxUpwardLaunchSpeed
        );
        motionVelocity = launchVelocity;

        landingMomentum = Vector3.zero;
        isFlying = false;
        state = HookState.Attached;

        if (movement != null)
        {
            movement.enabled = false;
        }

        if (lineRenderer != null)
        {
            lineRenderer.enabled = true;
        }

        Debug.Log(
            $"Grapple attached to: {hit.collider.gameObject.name}"
        );
    }

    private void ApplyGrappleMotion(float deltaTime)
    {
        Vector3 origin = GetGrapplePhysicsOrigin();

        Vector3 toAnchor = grappleTarget - origin;
        float distanceToAnchor = toAnchor.magnitude;

        if (distanceToAnchor <= 0.001f)
            return;

        Vector3 directionToAnchor =
            toAnchor / distanceToAnchor;

        if (wallMomentumDisabled)
        {
            motionVelocity = Vector3.zero;
            landingMomentum = Vector3.zero;
            return;
        }

        hitGrappleWallThisStep = false;

        // Gravity affects the swing.
        motionVelocity +=
            Vector3.down *
            gravity *
            deltaTime;

        // Pull toward the grapple point.
        if (distanceToAnchor > pullStopDistance)
        {
            motionVelocity +=
                directionToAnchor *
                pullAcceleration *
                deltaTime;
        }

        if (motionVelocity.y > maxUpwardLaunchSpeed)
        {
            motionVelocity.y = maxUpwardLaunchSpeed;
        }

        float radialSpeed =
            Vector3.Dot(
                motionVelocity,
                directionToAnchor
            );

        // radialSpeed < 0 means the player is moving away
        // from the grapple point.
        if (radialSpeed < 0f &&
            distanceToAnchor > pullStopDistance)
        {
            float outwardSpeed = -radialSpeed;

            // Strong correction force when momentum carries
            // the player away from the wall.
            motionVelocity +=
                directionToAnchor *
                returnAcceleration *
                deltaTime;

            // Remove part of the outward momentum smoothly.
            motionVelocity +=
                directionToAnchor *
                outwardSpeed *
                outwardDamping *
                deltaTime;

            // Optional hard limit for outward movement.
            if (maxOutwardSpeed > 0f)
            {
                float correctedRadialSpeed =
                    Vector3.Dot(
                        motionVelocity,
                        directionToAnchor
                    );
                if (correctedRadialSpeed < -maxOutwardSpeed)
                {
                    Vector3 tangentialVelocity =
                        motionVelocity -
                        directionToAnchor *
                        correctedRadialSpeed;
                    motionVelocity =
                        tangentialVelocity -
                        directionToAnchor *
                        maxOutwardSpeed;
                }
            }
        }

        // Near the grapple point, remove only inward velocity.
        // Sideways velocity remains for swinging.
        if (distanceToAnchor <= pullStopDistance &&
            radialSpeed > 0f)
        {
            motionVelocity -=
                directionToAnchor *
                radialSpeed;
        }

        if (maxGrappleSpeed > 0f &&
            motionVelocity.magnitude > maxGrappleSpeed)
        {
            motionVelocity =
                motionVelocity.normalized *
                maxGrappleSpeed;
        }

        controller.Move(
            motionVelocity *
            deltaTime
        );
        // Lock the player based on actual wall contact.
        if (hitGrappleWallThisStep)
        {
            wallContactTimer += deltaTime;
            if (wallContactTimer >= wallMomentumDisableDelay)
            {
                wallMomentumDisabled = true;
                motionVelocity = Vector3.zero;
                landingMomentum = Vector3.zero;
            }
        }
        else
        {
            wallContactTimer = 0f;
        }
    }

    private void ReleaseGrapple()
    {
        if (state != HookState.Attached)
            return;

        state = HookState.Reloading;
        reloadTimer = reloadTime;
        isFlying = true;

        wallContactTimer = 0f;
        wallMomentumDisabled = false;

        hitGrappleWallThisStep = false;
        grappleWallHitNormal = Vector3.zero;

        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }

        // No artificial release boost.
        // Existing momentum is carried naturally.
        Debug.Log("Grapple released.");
    }

    private void ApplyReleasedMotion(float deltaTime)
    {
        motionVelocity +=
            Vector3.down *
            gravity *
            deltaTime;

        CollisionFlags collisionFlags =
            controller.Move(
                motionVelocity *
                deltaTime
            );

        if ((collisionFlags & CollisionFlags.Below) != 0)
        {
            // Preserve horizontal velocity when landing.
            landingMomentum = new Vector3(
                motionVelocity.x,
                0f,
                motionVelocity.z
            );

            motionVelocity = Vector3.zero;
            isFlying = false;

            if (movement != null)
            {
                movement.enabled = true;
            }
        }
    }

    private void ApplyLandingMomentum(float deltaTime)
    {
        if (isFlying ||
            state == HookState.Attached ||
            landingMomentum.sqrMagnitude <= 0.001f)
        {
            return;
        }

        controller.Move(
            landingMomentum *
            deltaTime
        );

        landingMomentum = Vector3.MoveTowards(
            landingMomentum,
            Vector3.zero,
            landingMomentumDecay *
            deltaTime
        );
    }

    private void UpdateGrappleLine()
    {
        if (lineRenderer == null)
            return;

        lineRenderer.enabled =
            state == HookState.Attached;

        if (state != HookState.Attached)
            return;

        Vector3 origin = grapplePoint != null
            ? grapplePoint.position
            : transform.position;

        lineRenderer.SetPosition(0, origin);
        lineRenderer.SetPosition(1, grappleTarget);
    }

    private void OnDisable()
    {
        state = HookState.Ready;
        reloadTimer = 0f;
        isFlying = false;

        motionVelocity = Vector3.zero;
        landingMomentum = Vector3.zero;
        wallContactTimer = 0f;
        wallMomentumDisabled = false;
        grappleSurfaceNormal = Vector3.zero;
        hitGrappleWallThisStep = false;
        grappleWallHitNormal = Vector3.zero;

        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }

        if (movement != null)
        {
            movement.enabled = true;
        }
    }

    private Vector3 GetGrapplePhysicsOrigin()
    {
        if (controller != null)
        {
            return controller.bounds.center;
        }
        return transform.position;
    }

    private void OnControllerColliderHit(
    ControllerColliderHit hit)
    {
        if (state != HookState.Attached)
            return;

        // Stop velocity going into any contacted surface.
        // This prevents the player from repeatedly pushing
        // and bouncing through a cube.
        float velocityIntoSurface =
            Vector3.Dot(
                motionVelocity,
                hit.normal
            );

        if (velocityIntoSurface < 0f)
        {
            motionVelocity -=
                hit.normal *
                velocityIntoSurface;
        }

        bool isWall =
            Mathf.Abs(hit.normal.y) < 0.7f;

        bool isSameSurface =
            Vector3.Dot(
                hit.normal,
                grappleSurfaceNormal
            ) > 0.5f;

        if (isWall && isSameSurface)
        {
            hitGrappleWallThisStep = true;
            grappleWallHitNormal = hit.normal;
        }
    }

    private void CancelGrappleWithDash()
    {
        if (movement == null)
            return;

        if (!movement.TryStartDashFromGrapple())
            return;

        motionVelocity = Vector3.zero;
        landingMomentum = Vector3.zero;
        isFlying = false;

        wallContactTimer = 0f;
        wallMomentumDisabled = false;

        movement.enabled = true;

        Debug.Log("Grapple momentum cancelled by dash.");
    }
}