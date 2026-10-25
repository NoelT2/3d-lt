using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(CharacterController))]
public class PlayerDiveController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerCamera;
    [SerializeField] private Movement movement;
    [SerializeField] private GrappleController grappleController;

    [Header("Dive Settings")]
    [SerializeField] private float initialForwardSpeed = 10f;
    [Tooltip("Speed gained per second along the dive direction (replaces the old downward gravity).")]
    [SerializeField] private float diveGravity = 40f;
    [SerializeField] private float maxDiveSpeed = 45f;

    [Header("Cursor Aim (anti super-jump)")]
    [Tooltip("The dive always points at least this many degrees BELOW the horizontal, even if the cursor looks up or level. " +
             "This guarantees the dive can never gain height, and caps horizontal travel to roughly height * cot(angle).")]
    [SerializeField, Range(5f, 80f)] private float minDivePitchDegrees = 25f;
    [Tooltip("How fast (degrees/sec) the dive direction turns toward the cursor.")]
    [SerializeField] private float aimTurnRateDegrees = 180f;

    [Header("Impact AOE")]
    [SerializeField] private float impactAOERadius = 5f;
    [Tooltip("Dive damage at full height bonus = ImpactSettings.maxImpactDamage x this. 0.27 keeps the old 40 when max is 150.")]
    [SerializeField] private float diveDamageScale = 0.27f;
    [SerializeField] private float impactKnockbackForce = 14f;
    [SerializeField] private LayerMask impactDetectMask = ~0;
    [SerializeField] private ParticleSystem impactEffectPrefab;

    [Header("Camera Shake")]
    [SerializeField] private CameraShake cameraShake;
    [SerializeField] private float diveTraumaPerSecond = 0.6f;
    [SerializeField] private float impactTrauma = 0.9f;

    [Header("Dive Availability")]
    [SerializeField] private float diveCooldown = 2f;
    [SerializeField] private bool requireAirborne = true;
    [SerializeField] private float maxDiveDuration = 2.5f;

    [Header("Height-Scaled Impact")]
    [SerializeField] private float minHeightForBonus = 2f;
    [SerializeField] private float maxHeightForFullBonus = 20f;
    [SerializeField] private float maxHeightTraumaMultiplier = 2f;
    [SerializeField] private LayerMask groundCheckMask = ~0;
    [SerializeField] private float groundCheckMaxDistance = 200f;
    
    [Header("Impact Crater")]
    [SerializeField] private bool leaveCrater = true;
    [Tooltip("Crater strength for the lowest dive. Full height dive = 1.")]
    [SerializeField, Range(0f, 1f)] private float minCraterIntensity = 0.35f;

    private float diveStartHeight;
    private float diveStartTime;

    private CharacterController controller;
    private Vector3 diveVelocity;
    private Vector3 diveDirection;
    private float diveSpeed;
    private bool isDiving;
    private float nextDiveTime;

    private bool impactThisFrame;
    private Vector3 impactPoint;
    private Vector3 impactNormal;

    public bool IsDiving => isDiving;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        if (movement == null)
            movement = GetComponent<Movement>();

        if (grappleController == null)
            grappleController = GetComponent<GrappleController>();

        if (cameraShake == null && playerCamera != null)
            cameraShake = playerCamera.GetComponent<CameraShake>();
    }

    private void Update()
    {
        if (PauseMenuController.InputBlocked) return; // pause menu
        if (Keyboard.current == null) return;

        if (Keyboard.current.cKey.wasPressedThisFrame &&
            !isDiving &&
            Time.time >= nextDiveTime &&
            CanStartDive())
        {
            StartDive();
        }
    }

    private bool CanStartDive()
    {
        if (grappleController != null && grappleController.IsGrappling)
            return false;

        if (requireAirborne && controller.isGrounded)
            return false;

        return true;
    }

    private void StartDive()
    {
        isDiving = true;
        nextDiveTime = Time.time + diveCooldown;
        diveStartTime = Time.time;

        if (Physics.Raycast(
                transform.position,
                Vector3.down,
                out RaycastHit groundHit,
                groundCheckMaxDistance,
                groundCheckMask,
                QueryTriggerInteraction.Ignore))
        {
            diveStartHeight = groundHit.distance;
        }
        else
        {
            diveStartHeight = maxHeightForFullBonus;
        }

        // The dive overrides everything: wipe launch/slide momentum, jump velocity and any dash in progress.
        if (grappleController != null) grappleController.CancelLaunchMomentum();
        if (movement != null) movement.CancelMomentum();

        diveDirection = GetClampedAimDirection();
        diveSpeed = initialForwardSpeed;
        diveVelocity = diveDirection * diveSpeed;

        if (movement != null)
            movement.enabled = false;

        Debug.Log("Dive started.");
    }

    private void FixedUpdate()
    {
        if (!isDiving) return;

        float dt = Time.fixedDeltaTime;

        if (Time.time - diveStartTime > maxDiveDuration)
        {
            EndDiveWithImpact(transform.position, Vector3.up);
            return;
        }

        if (cameraShake != null)
        {
            float speedRatio = diveVelocity.magnitude / maxDiveSpeed;
            cameraShake.AddTrauma(diveTraumaPerSecond * speedRatio * dt);
        }

        // Steer toward the cursor (pitch-clamped so the dive can never point level or upward).
        Vector3 aimDir = GetClampedAimDirection();
        diveDirection = Vector3.RotateTowards(
            diveDirection, aimDir, aimTurnRateDegrees * Mathf.Deg2Rad * dt, 0f).normalized;

        diveSpeed = Mathf.MoveTowards(diveSpeed, maxDiveSpeed, diveGravity * dt);
        diveVelocity = diveDirection * diveSpeed;

        float moveDistance = diveVelocity.magnitude * dt;
        if (moveDistance > 0.0001f)
        {
            Vector3 castDir = diveVelocity.normalized;
            float castRadius = Mathf.Max(controller.radius - 0.02f, 0.05f);

            if (Physics.SphereCast(
                    controller.bounds.center,
                    castRadius,
                    castDir,
                    out RaycastHit hit,
                    moveDistance,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                EndDiveWithImpact(hit.point, hit.normal);
                return;
            }
        }

        impactThisFrame = false;

        CollisionFlags flags = controller.Move(diveVelocity * dt);

        if (impactThisFrame)
        {
            EndDiveWithImpact(impactPoint, impactNormal);
        }
        else if ((flags & CollisionFlags.Below) != 0)
        {
            EndDiveWithImpact(transform.position, Vector3.up);
        }
    }

    // Camera aim direction, but forced to point at least minDivePitchDegrees below the horizon.
    // Looking up/level => shallow downward dive along your facing; looking down => as steep as you aim.
    private Vector3 GetClampedAimDirection()
    {
        Vector3 aim = playerCamera != null ? playerCamera.forward : transform.forward;

        Vector3 flat = new Vector3(aim.x, 0f, aim.z);
        if (flat.sqrMagnitude < 0.0001f)
        {
            // Looking (almost) straight up/down: fall back to the body's facing for the horizontal part.
            flat = new Vector3(transform.forward.x, 0f, transform.forward.z);
            if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;
        }
        flat.Normalize();

        float pitch = Mathf.Asin(Mathf.Clamp(aim.y, -1f, 1f));          // + up, - down (radians)
        float maxPitch = -minDivePitchDegrees * Mathf.Deg2Rad;          // highest allowed pitch (below horizon)
        pitch = Mathf.Min(pitch, maxPitch);

        return (flat * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch)).normalized;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!isDiving) return;

        if (Vector3.Dot(diveVelocity.normalized, -hit.normal) < 0.3f)
            return;

        impactThisFrame = true;
        impactPoint = hit.point;
        impactNormal = hit.normal;
    }

    private void EndDiveWithImpact(Vector3 point, Vector3 normal)
    {
        isDiving = false;
        diveVelocity = Vector3.zero;
        diveSpeed = 0f;

        if (movement != null)
            movement.enabled = true;

        float heightRatio = Mathf.InverseLerp(
            minHeightForBonus,
            maxHeightForFullBonus,
            diveStartHeight
        );

        float scaledDamage = Mathf.Lerp(1f, ImpactSettings.Current.maxImpactDamage * diveDamageScale, heightRatio);

        ApplyImpactAOE(point, normal, scaledDamage);
        LeaveCrater(point, normal, heightRatio);
        GameEvents.RaiseDiveImpact(point, scaledDamage);

        Debug.Log($"Dive impact at {point}. Damage: {scaledDamage:F1}");

        if (cameraShake != null)
        {
            float scaledTrauma = impactTrauma * Mathf.Lerp(
                1f,
                maxHeightTraumaMultiplier,
                heightRatio
            );

            cameraShake.AddTrauma(scaledTrauma);
        }
    }

    private void ApplyImpactAOE(Vector3 point, Vector3 normal, float damageAtImpact)
    {
        SpawnImpactEffect(point, normal);

        Collider[] hits = Physics.OverlapSphere(
            point, impactAOERadius, impactDetectMask, QueryTriggerInteraction.Ignore);

        HashSet<EnemyHealth> damaged = new HashSet<EnemyHealth>();

        foreach (Collider col in hits)
        {
            EnemyHealth health = col.GetComponentInParent<EnemyHealth>();
            if (health == null || damaged.Contains(health))
                continue;

            damaged.Add(health);

            Vector3 away = health.transform.position - point;
            float distance = away.magnitude;
            float falloff = 1f - Mathf.Clamp01(distance / impactAOERadius);

            if (away.sqrMagnitude <= 0.001f)
                away = normal;
            else
                away.Normalize();

            float damage = Mathf.Max(1f, damageAtImpact * falloff);
            float knockback = impactKnockbackForce * falloff;

            health.TakeDamage(damage, away, knockback, point);
        }
    }

    private void LeaveCrater(Vector3 point, Vector3 normal, float heightRatio)
    {
        if (!leaveCrater) return;

        Collider surfaceCollider = null;
        Color surface = Color.gray;

        if (Physics.Raycast(point + normal * 0.5f, -normal, out RaycastHit hit, 1.5f,
                            groundCheckMask, QueryTriggerInteraction.Ignore)
            && hit.collider != controller)
        {
            // Moving props would leave a crater floating in the air.
            if (hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic)
                return;

            surfaceCollider = hit.collider;
            surface = ImpactSurface.GetColor(hit.collider, Color.gray);
        }

        GameEvents.RaiseEnemyImpact(new ImpactInfo
        {
            enemy = null,   // no enemy: only the DiveImpactFX object reacts
            other = surfaceCollider,
            point = point,
            normal = normal,
            intensity = Mathf.Lerp(minCraterIntensity, 1f, heightRatio),
            hitEnvironment = true,
            surfaceColor = surface
        });
    }

    private void SpawnImpactEffect(Vector3 point, Vector3 normal)
    {
        if (impactEffectPrefab == null) return;

        Quaternion rotation = normal.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(normal)
            : Quaternion.identity;

        ParticleSystem fx = Instantiate(impactEffectPrefab, point, rotation);
        fx.Play();
        Destroy(fx.gameObject, 5f);
    }
}