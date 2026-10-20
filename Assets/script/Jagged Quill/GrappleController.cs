using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[RequireComponent(typeof(CharacterController))]
public class GrappleController : MonoBehaviour
{
    public enum HookState { Ready, Attached, Reloading }

    private class HookSlot
    {
        public HookState state = HookState.Ready;
        public Vector3 grappleTarget;
        public Vector3 grappleSurfaceNormal;
        public Vector3 motionVelocity;
        public Vector3 landingMomentum;
        public bool isFlying;
        public float stuckTimer;
        public bool swinging;
        public float ropeLength;
        public float swingIdleTimer;
        public float reloadTimer;
        public float pullSpeed;   // current (ramping) grapple speed toward the hook

        public float wallContactTimer;
        public bool wallMomentumDisabled;
        public bool hitGrappleWallThisStep;
        public Vector3 grappleWallHitNormal;

        public EnemyGrappleTarget enemyTarget;
        public bool isEnemyGrapple;
    }

    [Header("References")]
    [SerializeField] private Transform playerCamera;
    [SerializeField] private Movement movement;
    [SerializeField] private PlayerDiveController diveController;

    [Header("Hook Origin Points")]
    [SerializeField] private Transform grapplePointE;
    [SerializeField] private Transform grapplePointQ;

    [Header("Rope Visual")]
    [SerializeField] private LineRenderer lineRendererE;
    [SerializeField] private LineRenderer lineRendererQ;

    [Header("Grapple Hit Settings")]
    [SerializeField] private float maxGrappleDistance = 30f;
    [SerializeField] private LayerMask grappleHitMask;

    [Header("Grapple Pull")]
    [Tooltip("Speed toward the hook the moment it attaches. The grapple then gets faster every second (see Pull Ramp).")]
    [SerializeField] private float pullStartSpeed = 12f;
    [Tooltip("How much faster the grapple gets every second while attached (m/s per second). A/D do NOT affect this.")]
    [SerializeField] private float pullRampPerSecond = 14f;
    [Tooltip("How quickly your velocity follows the target speed. High = snaps to the ramping speed.")]
    [SerializeField] private float pullAcceleration = 90f;
    [Tooltip("Top grapple speed without W.")]
    [SerializeField] private float maxGrappleSpeed = 35f;
    [Tooltip("Holding A or D multiplies how fast the grapple speeds up.")]
    [SerializeField] private float boostRampMultiplier = 3f;
    [Tooltip("Holding A or D raises the top grapple speed (and so the reel-in speed) by this factor. Speed eases back down when A/D are released.")]
    [SerializeField] private float boostMaxSpeedMultiplier = 1.4f;
    [SerializeField] private float pullStopDistance = 0.75f;
    [Tooltip("Gravity multiplier applied ONLY while a hook is attached. Lower = the pull overpowers gravity (AOT feel). " +
             "Normal gravity still applies the moment you release.")]
    [SerializeField, Range(0f, 1f)] private float grappleGravityMultiplier = 0.2f;

    [Header("Rope Tension")]
    [SerializeField] private float returnAcceleration = 180f;
    [SerializeField] private float outwardDamping = 12f;
    [SerializeField] private float maxOutwardSpeed = 12f;

    [Header("Vertical Control")]
    [SerializeField] private float maxUpwardLaunchSpeed = 8f;

    [Header("Release Launch Direction")]
    [Tooltip("On release, bend your velocity toward the rope line (hook direction). " +
            "0 = launch exactly along your current velocity (old behaviour), 1 = launch fully along the rope line. Speed is kept.")]
    [SerializeField, Range(0f, 1f)] private float releaseLineBias = 0.6f;
    [Tooltip("0 = same bias for every rope angle. 1 = flat ropes keep their sideways/spiral launch, steep ropes bend strongly " +
            "toward the line (the 'quadrant' feel: steep = mostly vertical, shallow = mostly horizontal).")]
    [SerializeField, Range(0f, 1f)] private float releaseSteepnessInfluence = 0.7f;
    [Tooltip("Rope angles (degrees above/below horizontal) where the steepness blend starts and reaches full strength.")]
    [SerializeField] private Vector2 releaseSteepAngleRange = new Vector2(20f, 60f);
    [Tooltip("Launch speed is never below this fraction of your speed at release (stops the bend from feeling like a brake).")]
    [SerializeField, Range(0f, 1f)] private float releaseMinSpeedKeep = 1f;

    [Header("Orbit swing (while hooked: A/D circle sideways AND keep reeling you in)")]
    [SerializeField] private bool swingEnabled = true;
    [Tooltip("Gravity multiplier while orbiting. Low = you stay level and circle SIDEWAYS around the hook (0 = perfectly level). " +
             "Raise it if you want a bit of sag.")]
    [SerializeField, Range(0f, 1f)] private float swingGravityMultiplier = 0.1f;
    [Tooltip("How quickly vertical drift is damped while orbiting (per second). Higher = holds height more strictly.")]
    [SerializeField] private float swingVerticalDamping = 2f;
    [Tooltip("Sideways acceleration from A/D along the circle around the hook (camera-relative).")]
    [SerializeField] private float swingInputAcceleration = 35f;
    [Tooltip("While circling (A/D) the rope keeps reeling you in along the rope line at this fraction of the current grapple speed. " +
             "0 = pure circle at a fixed distance, 1 = reel in as fast as the straight pull.")]
    [SerializeField, Range(0f, 1f)] private float swingReelInFactor = 0.6f;
    [Tooltip("Smallest circle radius (horizontal distance from the hook).")]
    [SerializeField] private float swingMinRopeLength = 3f;
    [Tooltip("When the orbit starts while flying at the hook, this fraction of the speed toward the hook becomes circling speed.")]
    [SerializeField, Range(0f, 1f)] private float swingEnterConversion = 0.7f;
    [Tooltip("Orbit lasts this long after the last A/D/S key is released (avoids flicker when switching keys).")]
    [SerializeField] private float swingExitDelay = 0.12f;

    [Header("Grapple Reload")]
    [SerializeField] private float reloadTime = 0.9f;

    [Header("Character Controller")]
    [Tooltip("Use Movement's gravity so falling after a release/launch matches normal falling.")]
    [SerializeField] private bool matchMovementGravity = true;
    [SerializeField] private float gravity = 20f;
    [Tooltip("Extra gravity while falling after a release (launch), so you drop faster instead of floating.")]
    [SerializeField] private float releasedFallGravityMultiplier = 1.8f;
    [Tooltip("Terminal fall speed after a release (m/s). 0 = no limit.")]
    [SerializeField] private float releasedMaxFallSpeed = 80f;

    [Header("Momentum")]
    [SerializeField] private float landingMomentumDecay = 3f;

    [Header("Launch Wall Impact (after release)")]
    [Tooltip("Minimum speed into a wall that counts as an impact.")]
    [SerializeField] private float wallImpactMinSpeed = 4f;
    [Tooltip("Fraction of momentum kept after slamming into a wall (0 = dead stop).")]
    [SerializeField, Range(0f, 1f)] private float wallImpactRetention = 0.15f;
    [Tooltip("Small push away from the wall so you don't stick to it.")]
    [SerializeField] private float wallImpactBounceSpeed = 1.5f;
    [Tooltip("Friction (1/sec) while scraping along a wall after release.")]
    [SerializeField] private float wallScrapeFriction = 10f;

    [Header("Launch Slide Control (WASD, camera-relative)")]
    [Tooltip("Steering acceleration from WASD while sliding on the ground after a launch.")]
    [SerializeField] private float slideControlAcceleration = 30f;
    [Tooltip("Extra deceleration when the input opposes your momentum (braking: hold S, or look back and hold W).")]
    [SerializeField] private float slideBrakeDeceleration = 80f;
    [Tooltip("Input can build speed up to this; above it, input can only steer/brake, never add speed.")]
    [SerializeField] private float slideControlMaxSpeed = 12f;
    [Tooltip("Control strength while airborne after a release (0 = none, 1 = same as ground).")]
    [SerializeField, Range(0f, 1f)] private float airControlMultiplier = 0.6f;

    [Header("Wall Momentum Lock")]
    [SerializeField] private float wallMomentumDisableDelay = 0.25f;

    [Header("Enemy Grapple")]
    [SerializeField] private float enemyPullForce = 100f;
    [SerializeField] private float enemyMaxPullSpeed = 20f;
    [SerializeField] private float enemyPlayerLaunchSpeed = 18f;
    [SerializeField] private float enemyPlayerPullAcceleration = 70f;
    [SerializeField] private float enemyPlayerMaxSpeed = 30f;

    [Header("Enemy Auto Blast")]
    [SerializeField] private WeaponController weaponController;
    [SerializeField] private float enemyBlastDistance = 2.5f;
    [SerializeField] private float blastHorizontalSpeed = 16f;
    [SerializeField] private float blastUpwardSpeed = 10f;
    [SerializeField] private float blastForwardSpeed = 3f;
    [SerializeField] private float enemyBlastKnockbackImpulse = 12f;
    [SerializeField] private float enemyBlastEnemyUpwardImpulse = 2f;
    [SerializeField] private float topBlastHeightThreshold = 1.25f;
    [SerializeField] private float topBlastHorizontalSpeed = 2f;
    [SerializeField] private float topBlastUpwardSpeed = 16f;

    private CharacterController controller;
    private Camera playerCameraComponent;

    private HookSlot hookE = new HookSlot();
    private HookSlot hookQ = new HookSlot();

    private float CurrentGravity =>
        (matchMovementGravity && movement != null) ? movement.GravityMagnitude : gravity;

    public bool IsGrappling =>
        hookE.state == HookState.Attached || hookQ.state == HookState.Attached;

    public float ReloadProgress01 => GetReloadProgress(hookE);

    // Wipes every source of leftover launch momentum (airborne release motion + ground slide momentum)
    // without touching reload cooldowns. Used by the dive so nothing overrides it.
    public void CancelLaunchMomentum()
    {
        foreach (HookSlot hook in new[] { hookE, hookQ })
        {
            if (hook.state == HookState.Attached) continue;

            hook.isFlying = false;
            hook.motionVelocity = Vector3.zero;
            hook.landingMomentum = Vector3.zero;
            hook.hitGrappleWallThisStep = false;
            hook.grappleWallHitNormal = Vector3.zero;
        }
    }

    private bool AnyHookBusy =>
        hookE.state == HookState.Attached || hookE.isFlying ||
        hookQ.state == HookState.Attached || hookQ.isFlying;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        if (playerCamera != null)
            playerCameraComponent = playerCamera.GetComponent<Camera>();

        if (playerCameraComponent == null)
            playerCameraComponent = Camera.main;

        if (movement == null)
            movement = GetComponent<Movement>();

        if (diveController == null)
            diveController = GetComponent<PlayerDiveController>();

        if (weaponController == null)
            weaponController = GetComponentInParent<WeaponController>();

        foreach (LineRenderer lr in new[] { lineRendererE, lineRendererQ })
        {
            if (lr != null)
            {
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.enabled = false;
            }
        }
    }

    private void Update()
    {
        if (PauseMenuController.InputBlocked) return; // pause menu
        if (Keyboard.current != null)
        {
            HandleHookInput(hookE, Keyboard.current.eKey);
            HandleHookInput(hookQ, Keyboard.current.qKey);

            if (AnyHookBusy && Keyboard.current.leftShiftKey.wasPressedThisFrame)
            {
                CancelGrappleWithDash();
            }
        }

        UpdateGrappleLine(hookE, lineRendererE, grapplePointE);
        UpdateGrappleLine(hookQ, lineRendererQ, grapplePointQ);
    }

    private void HandleHookInput(HookSlot hook, KeyControl key)
    {
        // Can't fire a hook while diving (the dive owns movement until impact).
        bool diving = diveController != null && diveController.IsDiving;

        if (key.wasPressedThisFrame && hook.state == HookState.Ready && !diving)
        {
            FireGrapple(hook);
        }
        if (key.wasReleasedThisFrame && hook.state == HookState.Attached)
        {
            ReleaseGrapple(hook);
        }
    }

    private bool slideControlUsedThisStep;

    private void FixedUpdate()
    {
        float deltaTime = Time.fixedDeltaTime;
        slideControlUsedThisStep = false;

        foreach (HookSlot hook in new[] { hookE, hookQ })
        {
            if (hook.state == HookState.Attached)
            {
                if (hook.isEnemyGrapple) ApplyEnemyGrappleMotion(hook, deltaTime);
                else ApplyGrappleMotion(hook, deltaTime);
            }

            if (hook.isFlying)
            {
                ApplyReleasedMotion(hook, deltaTime);
            }

            if (hook.state == HookState.Reloading)
            {
                float reloadMult = PlayerStats.Local != null ? PlayerStats.Local.GrappleReloadMultiplier : 1f;
                hook.reloadTimer -= deltaTime / Mathf.Max(0.1f, reloadMult);

                if (hook.reloadTimer <= 0f)
                {
                    hook.reloadTimer = 0f;
                    hook.state = HookState.Ready;
                }
            }

            ApplyLandingMomentum(hook, deltaTime);
        }
    }

    private float GetReloadProgress(HookSlot hook)
    {
        if (hook.state == HookState.Ready) return 1f;
        if (reloadTime <= 0f) return 1f;
        if (hook.state == HookState.Reloading) return 1f - Mathf.Clamp01(hook.reloadTimer / reloadTime);
        return 0f;
    }

    private void FireGrapple(HookSlot hook)
    {
        if (playerCameraComponent == null) return;

        Ray ray = new Ray(playerCameraComponent.transform.position, playerCameraComponent.transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, maxGrappleDistance, grappleHitMask, QueryTriggerInteraction.Ignore))
        {
            Debug.Log("Grapple missed.");
            return;
        }

        EnemyGrappleTarget hitEnemy = hit.collider.GetComponentInParent<EnemyGrappleTarget>();

        if (hitEnemy != null)
        {
            if (!hitEnemy.CanBeGrappled)
            {
                Debug.Log("This enemy cannot be grappled.");
                return;
            }

            BeginEnemyGrapple(hook, hitEnemy, hit.point);
            return;
        }

        hook.grappleTarget = hit.point;
        hook.grappleSurfaceNormal = hit.normal;
        hook.wallContactTimer = 0f;
        hook.wallMomentumDisabled = false;

        Vector3 origin = GetGrapplePhysicsOrigin();
        Vector3 toAnchor = hook.grappleTarget - origin;

        if (toAnchor.sqrMagnitude <= 0.001f) return;
        toAnchor.Normalize();

        Vector3 currentVelocity = TakeOverCurrentVelocity(hook);
        Vector3 sidewaysVelocity = currentVelocity - Vector3.Dot(currentVelocity, toAnchor) * toAnchor;

        Vector3 launchVelocity = sidewaysVelocity + toAnchor * pullStartSpeed;
        launchVelocity.y = Mathf.Min(launchVelocity.y, maxUpwardLaunchSpeed);
        hook.motionVelocity = launchVelocity;

        // The ramp starts from where you actually are (never slows you down on attach).
        hook.pullSpeed = Mathf.Clamp(launchVelocity.magnitude, pullStartSpeed, Mathf.Max(maxGrappleSpeed, pullStartSpeed));

        hook.landingMomentum = Vector3.zero;
        hook.isFlying = false;
        hook.swinging = false;
        hook.swingIdleTimer = 0f;
        hook.state = HookState.Attached;

        if (movement != null) movement.enabled = false;

        Debug.Log($"Grapple attached to: {hit.collider.gameObject.name}");
    }

    private void ApplyGrappleMotion(HookSlot hook, float deltaTime)
    {
        Vector3 origin = GetGrapplePhysicsOrigin();
        Vector3 toAnchor = hook.grappleTarget - origin;
        float distanceToAnchor = toAnchor.magnitude;

        if (distanceToAnchor <= 0.001f) return;

        Vector3 directionToAnchor = toAnchor / distanceToAnchor;

        if (hook.wallMomentumDisabled)
        {
            hook.motionVelocity = Vector3.zero;
            hook.landingMomentum = Vector3.zero;
            return;
        }

        // Time-based speed-up. Runs in both straight pull and orbit; holding A/D boosts it.
        UpdatePullSpeed(hook, deltaTime);

        // A/D/S held: pendulum swing around the hook instead of the straight pull.
        if (TryApplySwing(hook, origin, distanceToAnchor, toAnchor, deltaTime)) return;

        hook.hitGrappleWallThisStep = false;
        hook.motionVelocity += Vector3.down * (CurrentGravity * grappleGravityMultiplier) * deltaTime;

        if (distanceToAnchor > pullStopDistance)
        {
            hook.motionVelocity += directionToAnchor * pullAcceleration * deltaTime;
        }

        if (hook.motionVelocity.y > maxUpwardLaunchSpeed)
        {
            hook.motionVelocity.y = maxUpwardLaunchSpeed;
        }

        float radialSpeed = Vector3.Dot(hook.motionVelocity, directionToAnchor);

        if (radialSpeed < 0f && distanceToAnchor > pullStopDistance)
        {
            float outwardSpeed = -radialSpeed;

            hook.motionVelocity += directionToAnchor * returnAcceleration * deltaTime;
            hook.motionVelocity += directionToAnchor * outwardSpeed * outwardDamping * deltaTime;

            if (maxOutwardSpeed > 0f)
            {
                float correctedRadialSpeed = Vector3.Dot(hook.motionVelocity, directionToAnchor);
                if (correctedRadialSpeed < -maxOutwardSpeed)
                {
                    Vector3 tangentialVelocity = hook.motionVelocity - directionToAnchor * correctedRadialSpeed;
                    hook.motionVelocity = tangentialVelocity - directionToAnchor * maxOutwardSpeed;
                }
            }
        }

        if (distanceToAnchor <= pullStopDistance && radialSpeed > 0f)
        {
            hook.motionVelocity -= directionToAnchor * radialSpeed;
        }

        // The ramping speed IS the speed limit, so the pull starts slow and gets faster every moment.
        if (hook.motionVelocity.magnitude > hook.pullSpeed)
        {
            hook.motionVelocity = hook.motionVelocity.normalized * hook.pullSpeed;
        }

        controller.Move(hook.motionVelocity * deltaTime);

        UpdateWallContactTimer(hook, deltaTime);
    }

    // Grapple gets faster over time. Holding A/D = faster ramp + higher top speed.
    private void UpdatePullSpeed(HookSlot hook, float dt)
    {
        bool boost = Keyboard.current != null && (Keyboard.current.aKey.isPressed || Keyboard.current.dKey.isPressed);
        float baseMax = maxGrappleSpeed > 0f ? maxGrappleSpeed : 1000f;
        float topSpeed = baseMax * (boost ? Mathf.Max(1f, boostMaxSpeedMultiplier) : 1f);
        float ramp = pullRampPerSecond * (boost ? Mathf.Max(1f, boostRampMultiplier) : 1f);

        if (hook.pullSpeed < topSpeed)
            hook.pullSpeed = Mathf.Min(topSpeed, hook.pullSpeed + ramp * dt);
        else
            hook.pullSpeed = Mathf.MoveTowards(hook.pullSpeed, topSpeed, pullRampPerSecond * dt); // ease down after W is released
    }

    private void UpdateWallContactTimer(HookSlot hook, float deltaTime)
    {
        if (hook.hitGrappleWallThisStep)
        {
            hook.wallContactTimer += deltaTime;
            if (hook.wallContactTimer >= wallMomentumDisableDelay)
            {
                hook.wallMomentumDisabled = true;
                hook.motionVelocity = Vector3.zero;
                hook.landingMomentum = Vector3.zero;
            }
        }
        else
        {
            hook.wallContactTimer = 0f;
        }
    }

    // ---------------- Orbit swing ----------------
    // A level (sideways) circle around the vertical axis through the hook point, like orbiting a pole.
    // The "rope" is a horizontal radius: you keep your sideways speed and can't move farther out than the radius.

    private Vector3 FlatCameraRight()
    {
        Transform cam = playerCamera != null ? playerCamera : transform;
        Vector3 right = new Vector3(cam.right.x, 0f, cam.right.z);
        return right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
    }

    private Vector3 SwingPushDirection(Vector3 outH, float x)
    {
        if (Mathf.Approximately(x, 0f)) return Vector3.zero;

        Vector3 right = Vector3.ProjectOnPlane(FlatCameraRight(), outH);
        if (right.sqrMagnitude < 0.0001f) return Vector3.zero;

        return right.normalized * Mathf.Sign(x);
    }

    // Returns true if the orbit handled this physics step (including the Move).
    private bool TryApplySwing(HookSlot hook, Vector3 origin, float distance, Vector3 toAnchor, float dt)
    {
        if (!swingEnabled || hook.isEnemyGrapple || Keyboard.current == null)
        {
            hook.swinging = false;
            return false;
        }

        // Two hooks attached at once keep the old straight-pull behaviour (their velocities add together).
        HookSlot other = ReferenceEquals(hook, hookE) ? hookQ : hookE;
        if (other.state == HookState.Attached)
        {
            hook.swinging = false;
            return false;
        }

        Keyboard kb = Keyboard.current;
        float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);

        // Only A or D start the orbit (W/S do nothing on the grapple any more).
        bool swingKey = x != 0f;

        if (swingKey) hook.swingIdleTimer = swingExitDelay;
        else if (hook.swinging) hook.swingIdleTimer -= dt;

        if (!swingKey && !(hook.swinging && hook.swingIdleTimer > 0f))
        {
            hook.swinging = false;
            return false;
        }

        Vector3 anchor = hook.grappleTarget;
        Vector3 radialH = new Vector3(origin.x - anchor.x, 0f, origin.z - anchor.z);
        float radius = radialH.magnitude;

        // Directly above/below the hook there is no sideways circle: use the normal pull.
        if (radius < swingMinRopeLength * 0.5f)
        {
            hook.swinging = false;
            return false;
        }

        Vector3 outH = radialH / radius;                       // from the hook's vertical axis to the player
        Vector3 tanH = Vector3.Cross(Vector3.up, outH);        // along the circle
        float height = origin.y - anchor.y;
        float maxRadius = Mathf.Max(
            Mathf.Sqrt(Mathf.Max(0f, maxGrappleDistance * maxGrappleDistance - height * height)), radius);

        if (!hook.swinging)
        {
            hook.swinging = true;
            hook.ropeLength = Mathf.Clamp(radius, swingMinRopeLength, maxRadius);
            EnterSwing(hook, outH, x);
        }

        // Slack is taken up: the circle never stays bigger than where you actually are.
        if (radius < hook.ropeLength)
            hook.ropeLength = Mathf.Max(radius, swingMinRopeLength);

        Vector3 v = hook.motionVelocity;
        float prevRadius = hook.ropeLength;
        float newRadius = prevRadius;
        Vector3 reelMove = Vector3.zero;

        // While circling, the rope ALWAYS keeps reeling you in along the rope line (3D): you spiral toward the
        // hook, and with the hook below you that means spiraling DOWN as you go around.
        float reelSpeed = hook.pullSpeed * swingReelInFactor;
        reelMove = (toAnchor / distance) * reelSpeed * dt;
        if (reelMove.y > maxUpwardLaunchSpeed * dt) reelMove.y = maxUpwardLaunchSpeed * dt;

        float stepH = new Vector2(reelMove.x, reelMove.z).magnitude;
        float allowed = Mathf.Max(0f, prevRadius - swingMinRopeLength);

        if (stepH > allowed && stepH > 0.0001f)
        {
            // Circle already at its smallest: stop shrinking it, but KEEP reeling up/down the rope.
            float k = allowed / stepH;
            reelMove.x *= k;
            reelMove.z *= k;
            stepH = allowed;
        }

        newRadius = prevRadius - stepH;

        hook.ropeLength = newRadius;

        // Mostly level: tiny gravity, and vertical drift is damped.
        v += Vector3.down * (CurrentGravity * swingGravityMultiplier) * dt;
        v.y *= Mathf.Max(0f, 1f - swingVerticalDamping * dt);

        // Vertical movement comes from the reel-in above, so kill any leftover vertical drift.
        v.y = Mathf.MoveTowards(v.y, 0f, pullAcceleration * dt);

        Vector3 push = SwingPushDirection(outH, x);
        if (push.sqrMagnitude > 0f)
            v += push * swingInputAcceleration * dt;

        // Cap only the circling (horizontal) speed; the reel-in is a separate movement and never changes how fast A/D circle.
        if (maxGrappleSpeed > 0f)
        {
            Vector2 flat = new Vector2(v.x, v.z);
            if (flat.magnitude > maxGrappleSpeed)
            {
                flat = flat.normalized * maxGrappleSpeed;
                v.x = flat.x;
                v.z = flat.y;
            }
        }

        // Constraint: can't move farther from the hook's axis than the circle radius.
        // The sideways speed stays, the outward speed is removed.
        Vector3 correction = reelMove;
        Vector3 nextPos = origin + v * dt + reelMove;
        Vector3 nextH = new Vector3(nextPos.x - anchor.x, 0f, nextPos.z - anchor.z);
        float nextRadius = nextH.magnitude;

        if (nextRadius > newRadius && nextRadius > 0.001f)
        {
            Vector3 outNext = nextH / nextRadius;
            float outSpeed = v.x * outNext.x + v.z * outNext.z;
            if (outSpeed > 0f)
            {
                v.x -= outNext.x * outSpeed;
                v.z -= outNext.z * outSpeed;
            }

            nextPos = origin + v * dt + reelMove;
            nextH = new Vector3(nextPos.x - anchor.x, 0f, nextPos.z - anchor.z);
            nextRadius = nextH.magnitude;

            if (nextRadius > newRadius && nextRadius > 0.001f)
            {
                Vector3 onCircle = nextH / nextRadius * newRadius;
                correction += new Vector3(anchor.x + onCircle.x - nextPos.x, 0f, anchor.z + onCircle.z - nextPos.z);
            }
        }

        // Never farther than the maximum rope length in 3D either.
        Vector3 pos2 = origin + v * dt + correction;
        Vector3 from3 = pos2 - anchor;
        float d3 = from3.magnitude;
        if (d3 > maxGrappleDistance && d3 > 0.001f)
        {
            Vector3 o3 = from3 / d3;
            float os = Vector3.Dot(v, o3);
            if (os > 0f) v -= o3 * os;
            correction += (anchor + o3 * maxGrappleDistance) - pos2;
        }

        hook.hitGrappleWallThisStep = false;
        hook.motionVelocity = v;   // set before Move: wall hits inside Move edit motionVelocity

        controller.Move(hook.motionVelocity * dt + correction);

        UpdateWallContactTimer(hook, dt);
        return true;
    }

    // Starting the orbit while flying at the hook: turn some of the speed toward the hook into circling speed,
    // in the direction you were already moving sideways (or the A/D direction).
    private void EnterSwing(HookSlot hook, Vector3 outH, float x)
    {
        if (swingEnterConversion <= 0f) return;

        Vector3 v = hook.motionVelocity;
        float outSpeed = v.x * outH.x + v.z * outH.z;
        if (outSpeed >= 0f) return; // not moving toward the axis

        Vector3 tanH = Vector3.Cross(Vector3.up, outH);
        float tanSpeed = Vector3.Dot(v, tanH);

        float dirSign;
        if (Mathf.Abs(tanSpeed) > 0.5f)
        {
            dirSign = Mathf.Sign(tanSpeed);
        }
        else
        {
            Vector3 push = SwingPushDirection(outH, x);
            Vector3 reference = push.sqrMagnitude > 0f ? push : FlatCameraRight();
            dirSign = Vector3.Dot(reference, tanH) >= 0f ? 1f : -1f;
        }

        float converted = -outSpeed * swingEnterConversion;
        v += outH * converted;               // less speed toward the hook...
        v += tanH * dirSign * converted;     // ...more speed around it
        hook.motionVelocity = v;
    }

    private void ReleaseGrapple(HookSlot hook)
    {
        if (hook.state != HookState.Attached) return;

        if (hook.isEnemyGrapple)
        {
            EndEnemyGrapple(hook);
            return;
        }

        hook.state = HookState.Reloading;
        hook.reloadTimer = reloadTime;
        hook.swinging = false;

        // If the other hook is still attached, give it our momentum instead of free-falling separately.
        bool handedOff = HandOffMomentumToOtherHook(hook);
        if (!handedOff) ApplyReleaseLineBias(hook);
        hook.isFlying = !handedOff;

        hook.wallContactTimer = 0f;
        hook.wallMomentumDisabled = false;
        hook.hitGrappleWallThisStep = false;
        hook.grappleWallHitNormal = Vector3.zero;

        Debug.Log("Grapple released.");
    }

    // Quick-swap fix: if the other hook was just released and is still "flying" (free-falling on its own
    // velocity), that velocity would keep being applied on top of the new hook's pull and cancel it until
    // landing. Take its velocity as the player's current velocity and stop it, so only the new hook drives movement.
    private Vector3 TakeOverCurrentVelocity(HookSlot newHook)
    {
        HookSlot other = ReferenceEquals(newHook, hookE) ? hookQ : hookE;

        if (other.isFlying)
        {
            Vector3 velocity = other.motionVelocity;
            other.motionVelocity = Vector3.zero;
            other.landingMomentum = Vector3.zero;
            other.isFlying = false;
            return velocity;
        }

        return controller.velocity;
    }

    // Both hooks call controller.Move() with their own velocity, so the player's real velocity is the sum.
    // When one hook lets go while the other is still attached, a separate "flying" velocity would fall under
    // gravity and cancel the remaining hook's pull until landing. Instead, merge the released hook's velocity
    // into the attached one so it keeps pulling from the current momentum.
    private bool HandOffMomentumToOtherHook(HookSlot releasing)
    {
        HookSlot other = ReferenceEquals(releasing, hookE) ? hookQ : hookE;
        if (other.state != HookState.Attached) return false;

        other.motionVelocity += releasing.motionVelocity;
        other.landingMomentum = Vector3.zero;

        releasing.motionVelocity = Vector3.zero;
        releasing.landingMomentum = Vector3.zero;
        return true;
    }

    private void ApplyReleasedMotion(HookSlot hook, float deltaTime)
    {
        if (!slideControlUsedThisStep)
        {
            slideControlUsedThisStep = true;
            Vector3 h = ApplySlideControl(
                new Vector3(hook.motionVelocity.x, 0f, hook.motionVelocity.z), airControlMultiplier, deltaTime);
            hook.motionVelocity.x = h.x;
            hook.motionVelocity.z = h.z;
        }

        float releasedGravity = CurrentGravity;
        if (hook.motionVelocity.y < 0f) releasedGravity *= releasedFallGravityMultiplier;
        hook.motionVelocity += Vector3.down * releasedGravity * deltaTime;
        if (releasedMaxFallSpeed > 0f && hook.motionVelocity.y < -releasedMaxFallSpeed)
            hook.motionVelocity.y = -releasedMaxFallSpeed;

        CollisionFlags collisionFlags = controller.Move(hook.motionVelocity * deltaTime);

        if ((collisionFlags & CollisionFlags.Below) != 0)
        {
            hook.landingMomentum = new Vector3(hook.motionVelocity.x, 0f, hook.motionVelocity.z);
            hook.motionVelocity = Vector3.zero;
            hook.isFlying = false;
            hook.stuckTimer = 0f;

            if (movement != null && !AnyHookBusy)
            {
                movement.enabled = true;
            }
            return;
        }

        // Safety: if we're being pushed but the controller isn't actually moving (embedded in a collider,
        // or never reporting "Below"), don't stay in the flying state forever with Movement disabled.
        if (hook.motionVelocity.sqrMagnitude > 4f && controller.velocity.sqrMagnitude < 0.25f)
            hook.stuckTimer += deltaTime;
        else
            hook.stuckTimer = 0f;

        if (hook.stuckTimer > 0.5f)
        {
            hook.motionVelocity = Vector3.zero;
            hook.landingMomentum = Vector3.zero;
            hook.isFlying = false;
            hook.stuckTimer = 0f;

            if (movement != null && !AnyHookBusy)
                movement.enabled = true;
        }
    }

    // Bends the velocity at the moment of release toward the rope line. Smooth blend (no hard X/Y switch),
    // weighted by how steep the rope is right now. Not clamped by maxUpwardLaunchSpeed, only gravity acts afterwards.
    private void ApplyReleaseLineBias(HookSlot hook)
    {
        if (releaseLineBias <= 0f) return;

        Vector3 v = hook.motionVelocity;
        float speed = v.magnitude;
        if (speed < 0.5f) return;

        Vector3 toAnchor = hook.grappleTarget - GetGrapplePhysicsOrigin();
        float dist = toAnchor.magnitude;
        if (dist <= pullStopDistance) return;          // right at the hook: rope direction is meaningless

        Vector3 lineDir = toAnchor / dist;
        Vector3 velDir = v / speed;

        // Steepness of the rope: 0 = horizontal, 1 = vertical, smoothed between the two angles.
        float angle = Mathf.Asin(Mathf.Clamp01(Mathf.Abs(lineDir.y))) * Mathf.Rad2Deg;
        float steep01 = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(releaseSteepAngleRange.x, Mathf.Max(releaseSteepAngleRange.x + 0.01f, releaseSteepAngleRange.y), angle));

        float weight = releaseLineBias * Mathf.Lerp(1f, steep01, releaseSteepnessInfluence);

        Vector3 newDir = Vector3.Slerp(velDir, lineDir, weight);
        float newSpeed = Mathf.Max(speed * releaseMinSpeedKeep, 0f);

        hook.motionVelocity = newDir.normalized * newSpeed;
    }

    private void ApplyLandingMomentum(HookSlot hook, float deltaTime)
    {
        if (hook.isFlying || hook.state == HookState.Attached || hook.landingMomentum.sqrMagnitude <= 0.001f)
            return;

        if (!slideControlUsedThisStep)
        {
            slideControlUsedThisStep = true;
            hook.landingMomentum = ApplySlideControl(hook.landingMomentum, 1f, deltaTime);
        }

        controller.Move(hook.landingMomentum * deltaTime);
        hook.landingMomentum = Vector3.MoveTowards(hook.landingMomentum, Vector3.zero, landingMomentumDecay * deltaTime);
    }

    // Only one LineRenderer, so only one rope can be shown at a time. E takes priority if both are attached.
    private void UpdateGrappleLine(HookSlot hook, LineRenderer lr, Transform originTransform)
    {
        if (lr == null) return;

        lr.enabled = hook.state == HookState.Attached;

        if (hook.state != HookState.Attached) return;

        if (hook.isEnemyGrapple && hook.enemyTarget != null)
        {
            hook.grappleTarget = hook.enemyTarget.GrapplePosition;
        }

        Vector3 origin = originTransform != null ? originTransform.position : transform.position;

        lr.SetPosition(0, origin);
        lr.SetPosition(1, hook.grappleTarget);
    }

    private void OnDisable()
    {
        foreach (HookSlot hook in new[] { hookE, hookQ })
        {
            hook.state = HookState.Ready;
            hook.reloadTimer = 0f;
            hook.isFlying = false;
            hook.motionVelocity = Vector3.zero;
            hook.landingMomentum = Vector3.zero;
            hook.wallContactTimer = 0f;
            hook.wallMomentumDisabled = false;
            hook.grappleSurfaceNormal = Vector3.zero;
            hook.hitGrappleWallThisStep = false;
            hook.grappleWallHitNormal = Vector3.zero;

            if (hook.enemyTarget != null) hook.enemyTarget.EndGrapple();
            hook.enemyTarget = null;
            hook.isEnemyGrapple = false;
        }

        if (lineRendererE != null) lineRendererE.enabled = false;
        if (lineRendererQ != null) lineRendererQ.enabled = false;
        if (movement != null) movement.enabled = true;
    }

    private Vector3 GetGrapplePhysicsOrigin()
    {
        if (controller != null) return controller.bounds.center;
        return transform.position;
    }

    private Vector3 GetWishDirection()
    {
        if (Keyboard.current == null) return Vector3.zero;

        float x = (Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed ? 1f : 0f);
        float y = (Keyboard.current.wKey.isPressed ? 1f : 0f) - (Keyboard.current.sKey.isPressed ? 1f : 0f);
        if (x == 0f && y == 0f) return Vector3.zero;

        Transform cam = playerCamera != null ? playerCamera : transform;
        Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        return Vector3.ClampMagnitude(forward * y + right * x, 1f);
    }

    // Steer / brake horizontal launch momentum with WASD (camera-relative).
    // Opposing input brakes harder; input can build speed only up to slideControlMaxSpeed,
    // so it can never be used to boost a launch.
    private Vector3 ApplySlideControl(Vector3 horizontal, float strength, float deltaTime)
    {
        Vector3 wish = GetWishDirection();
        if (wish.sqrMagnitude < 0.0001f || strength <= 0f) return horizontal;

        float speedBefore = horizontal.magnitude;

        if (speedBefore > 0.01f)
        {
            float alignment = Vector3.Dot(wish.normalized, horizontal / speedBefore);
            if (alignment < 0f)
            {
                float brake = slideBrakeDeceleration * strength * -alignment * wish.magnitude * deltaTime;
                horizontal = Vector3.MoveTowards(horizontal, Vector3.zero, brake);
            }
        }

        horizontal += wish * (slideControlAcceleration * strength * deltaTime);

        float cap = Mathf.Max(speedBefore, slideControlMaxSpeed);
        if (horizontal.magnitude > cap)
            horizontal = horizontal.normalized * cap;

        return horizontal;
    }

    // After a release (airborne or sliding), hitting a wall kills most momentum instead of letting you stick/slide along it.
    private void ApplyLaunchWallImpact(HookSlot hook, Vector3 wallNormal)
    {
        wallNormal.y = 0f;
        if (wallNormal.sqrMagnitude < 0.0001f) return;
        wallNormal.Normalize();

        bool airborne = hook.isFlying;
        Vector3 v = airborne ? hook.motionVelocity : hook.landingMomentum;

        float into = Vector3.Dot(v, wallNormal);
        if (into < -wallImpactMinSpeed)
        {
            if (movement != null) movement.NotifyWallHit(wallNormal, -into); // arms the super dash
            // Real impact: drop the into-wall part, keep a small fraction of the rest, nudge away from the wall.
            v -= wallNormal * into;
            v *= wallImpactRetention;
            v += wallNormal * wallImpactBounceSpeed;
        }
        else
        {
            // Scraping along the wall: strong friction so you can't ride it.
            float f = Mathf.Max(0f, 1f - wallScrapeFriction * Time.fixedDeltaTime);
            Vector3 intoPart = into < 0f ? wallNormal * into : Vector3.zero;
            v = (v - intoPart) * f;
        }

        // Never let the wall impact add vertical speed.
        if (airborne)
        {
            float vy = hook.motionVelocity.y;
            hook.motionVelocity = new Vector3(v.x, vy, v.z);
            if (vy > 0f) hook.motionVelocity.y = vy * wallImpactRetention;
        }
        else
        {
            hook.landingMomentum = new Vector3(v.x, 0f, v.z);
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        bool hitIsWall = Mathf.Abs(hit.normal.y) < 0.7f;

        foreach (HookSlot hook in new[] { hookE, hookQ })
        {
            if (hitIsWall && hook.state != HookState.Attached &&
                (hook.isFlying || hook.landingMomentum.sqrMagnitude > 0.001f))
            {
                ApplyLaunchWallImpact(hook, hit.normal);
                continue;
            }

            if (hook.state != HookState.Attached) continue;

            if (hitIsWall && movement != null)
            {
                float intoWall = -Vector3.Dot(hook.motionVelocity, hit.normal);
                if (intoWall > 0f) movement.NotifyWallHit(hit.normal, intoWall); // arms the super dash
            }

            bool isWall = Mathf.Abs(hit.normal.y) < 0.7f;
            bool isSameSurface = Vector3.Dot(hit.normal, hook.grappleSurfaceNormal) > 0.5f;

            if (isWall && isSameSurface)
            {
                float velocityIntoSurface = Vector3.Dot(hook.motionVelocity, hit.normal);
                if (velocityIntoSurface < 0f)
                {
                    hook.motionVelocity -= hit.normal * velocityIntoSurface;
                }

                hook.hitGrappleWallThisStep = true;
                hook.grappleWallHitNormal = hit.normal;
            }
        }
    }

    private void CancelGrappleWithDash()
    {
        if (movement == null) return;
        if (!movement.TryStartDashFromGrapple()) return;

        // Dash fully wipes grapple state: hooks go back to Ready (no cooldown) and lines are hidden,
        // so the player must press Q/E again to grapple.
        foreach (HookSlot hook in new[] { hookE, hookQ })
        {
            if (hook.enemyTarget != null) hook.enemyTarget.EndGrapple();
            hook.enemyTarget = null;
            hook.isEnemyGrapple = false;

            hook.state = HookState.Ready;
            hook.reloadTimer = 0f;
            hook.grappleTarget = Vector3.zero;
            hook.grappleSurfaceNormal = Vector3.zero;
            hook.motionVelocity = Vector3.zero;
            hook.landingMomentum = Vector3.zero;
            hook.isFlying = false;
            hook.swinging = false;
            hook.wallContactTimer = 0f;
            hook.wallMomentumDisabled = false;
            hook.hitGrappleWallThisStep = false;
            hook.grappleWallHitNormal = Vector3.zero;
        }

        if (lineRendererE != null) lineRendererE.enabled = false;
        if (lineRendererQ != null) lineRendererQ.enabled = false;

        movement.enabled = true;
        Debug.Log("Grapple momentum cancelled by dash.");
    }

    private void BeginEnemyGrapple(HookSlot hook, EnemyGrappleTarget target, Vector3 hitPoint)
    {
        hook.enemyTarget = target;
        hook.isEnemyGrapple = true;
        hook.enemyTarget.BeginGrapple(hitPoint);
        hook.grappleTarget = hook.enemyTarget.GrapplePosition;

        Vector3 playerPosition = GetGrapplePhysicsOrigin();
        Vector3 toEnemy = hook.grappleTarget - playerPosition;

        if (toEnemy.sqrMagnitude <= 0.001f)
        {
            EndEnemyGrapple(hook);
            return;
        }

        toEnemy.Normalize();

        Vector3 currentVelocity = TakeOverCurrentVelocity(hook);
        Vector3 sidewaysVelocity = currentVelocity - Vector3.Dot(currentVelocity, toEnemy) * toEnemy;

        hook.motionVelocity = sidewaysVelocity + toEnemy * enemyPlayerLaunchSpeed;
        hook.motionVelocity.y = Mathf.Min(hook.motionVelocity.y, maxUpwardLaunchSpeed);

        hook.landingMomentum = Vector3.zero;
        hook.isFlying = false;
        hook.wallContactTimer = 0f;
        hook.wallMomentumDisabled = false;
        hook.state = HookState.Attached;

        if (movement != null) movement.enabled = false;

        Debug.Log($"Enemy grapple attached to: {hook.enemyTarget.name}");
    }

    private void ApplyEnemyGrappleMotion(HookSlot hook, float deltaTime)
    {
        if (hook.enemyTarget == null)
        {
            EndEnemyGrapple(hook);
            return;
        }

        Vector3 playerPosition = GetGrapplePhysicsOrigin();
        Vector3 enemyPosition = hook.enemyTarget.GrapplePosition;
        Vector3 toEnemy = enemyPosition - playerPosition;
        float distanceToEnemy = toEnemy.magnitude;

        hook.grappleTarget = enemyPosition;

        if (distanceToEnemy <= enemyBlastDistance)
        {
            TriggerEnemyBlast(hook, playerPosition, enemyPosition);
            return;
        }

        if (distanceToEnemy > 0.001f)
        {
            Vector3 directionToEnemy = toEnemy / distanceToEnemy;
            hook.motionVelocity += directionToEnemy * enemyPlayerPullAcceleration * deltaTime;
        }

        hook.motionVelocity += Vector3.down * CurrentGravity * deltaTime;

        if (hook.motionVelocity.y > maxUpwardLaunchSpeed)
        {
            hook.motionVelocity.y = maxUpwardLaunchSpeed;
        }

        if (enemyPlayerMaxSpeed > 0f && hook.motionVelocity.magnitude > enemyPlayerMaxSpeed)
        {
            hook.motionVelocity = hook.motionVelocity.normalized * enemyPlayerMaxSpeed;
        }

        CollisionFlags collisionFlags = controller.Move(hook.motionVelocity * deltaTime);

        if ((collisionFlags & CollisionFlags.Below) != 0 && hook.motionVelocity.y < 0f)
        {
            hook.motionVelocity.y = 0f;
        }

        hook.enemyTarget.PullToward(playerPosition, enemyPullForce, enemyMaxPullSpeed);
    }

    private void TriggerEnemyBlast(HookSlot hook, Vector3 playerPosition, Vector3 enemyPosition)
    {
        float verticalDifference = playerPosition.y - enemyPosition.y;
        bool playerIsAboveEnemy = verticalDifference >= topBlastHeightThreshold;

        Vector3 horizontalDirection = playerPosition - enemyPosition;
        horizontalDirection.y = 0f;

        if (horizontalDirection.sqrMagnitude <= 0.001f && playerCamera != null)
        {
            horizontalDirection = playerCamera.position - enemyPosition;
            horizontalDirection.y = 0f;
        }

        if (horizontalDirection.sqrMagnitude > 0.001f)
        {
            horizontalDirection.Normalize();
        }

        float selectedHorizontalSpeed = playerIsAboveEnemy ? topBlastHorizontalSpeed : blastHorizontalSpeed;
        float selectedUpwardSpeed = playerIsAboveEnemy ? topBlastUpwardSpeed : blastUpwardSpeed;

        hook.motionVelocity = Vector3.zero;

        if (horizontalDirection.sqrMagnitude > 0.001f)
        {
            hook.motionVelocity += horizontalDirection * selectedHorizontalSpeed;
        }

        hook.motionVelocity += Vector3.up * selectedUpwardSpeed;

        if (!playerIsAboveEnemy && blastForwardSpeed != 0f)
        {
            Vector3 forwardBias = playerCamera != null ? playerCamera.forward : transform.forward;
            forwardBias.y = 0f;

            if (forwardBias.sqrMagnitude > 0.001f)
            {
                forwardBias.Normalize();
                hook.motionVelocity += forwardBias * blastForwardSpeed;
            }
        }

        if (hook.enemyTarget != null)
        {
            hook.enemyTarget.BlastAwayFrom(playerPosition, enemyBlastKnockbackImpulse, enemyBlastEnemyUpwardImpulse);
            hook.enemyTarget.EndGrapple();
        }

        hook.enemyTarget = null;
        hook.isEnemyGrapple = false;
        hook.state = HookState.Reloading;
        hook.reloadTimer = reloadTime;
        hook.isFlying = true;
        hook.wallContactTimer = 0f;
        hook.wallMomentumDisabled = false;
        hook.hitGrappleWallThisStep = false;

        if (weaponController != null) weaponController.PlayGrappleBlastAnimation();

        Debug.Log("Enemy grapple auto blast triggered.");
    }

    private void EndEnemyGrapple(HookSlot hook)
    {
        if (hook.enemyTarget != null) hook.enemyTarget.EndGrapple();

        hook.enemyTarget = null;
        hook.isEnemyGrapple = false;
        hook.state = HookState.Reloading;
        hook.reloadTimer = reloadTime;
        hook.isFlying = !HandOffMomentumToOtherHook(hook);
        hook.wallContactTimer = 0f;
        hook.wallMomentumDisabled = false;
        hook.hitGrappleWallThisStep = false;

        Debug.Log("Enemy grapple released.");
    }
}