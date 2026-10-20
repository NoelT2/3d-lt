using UnityEngine;

public class RangedAttack : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private float attackRange = 12f;
    [SerializeField] private float minEngageRange = 4f; // won't shoot if player is TOO close, backs off instead
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float projectileSpeed = 20f;
    [SerializeField] private LayerMask hitMask;

    private EnemyKnockdownState knockdownState;
    private EnemyGrappleTarget grappleTarget;
    private float nextAttackTime;
    private IEnemyMovement movement;

    private void Awake()
    {
        movement = GetComponent<IEnemyMovement>();
        movement.SetDesiredRange(minEngageRange, attackRange);
        knockdownState = GetComponent<EnemyKnockdownState>();
        grappleTarget = GetComponent<EnemyGrappleTarget>();
    }

    private void Start()
    {
        // Deferred to Start, not Awake — GroundMovement's own Awake must run
        // first to create `agent` before SetManualRotation touches it, and
        // Unity doesn't guarantee Awake order across different scripts.
        movement.SetManualRotation(true);
    }

    private bool IsDisabledByPhysicsState =>
        (knockdownState != null && knockdownState.State != EnemyKnockdownState.KnockState.Normal) ||
        (grappleTarget != null && grappleTarget.IsBeingGrappled);

    private void Update()
    {
        if (!movement.HasTarget || Time.time < nextAttackTime || IsDisabledByPhysicsState)
            return;

        if (movement.IsInRange(attackRange) && !movement.IsInRange(minEngageRange))
        {
            FireProjectile();
        }
    }

    private void LateUpdate()
    {
        if (!movement.HasTarget || IsDisabledByPhysicsState) return;

        Vector3 targetPos = movement.Target.position;

        // Body: horizontal-only facing, same as before
        Vector3 flatDir = targetPos - transform.position;
        flatDir.y = 0f;
        if (flatDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(flatDir);

        // Fire point: full 3D aim, independent of body rotation, so it can
        // pitch up/down at an airborne player even while the body stays upright
        if (firePoint != null)
        {
            Vector3 aimDir = targetPos - firePoint.position;
            if (aimDir.sqrMagnitude > 0.001f)
                firePoint.rotation = Quaternion.LookRotation(aimDir);
        }
    }

    private void FireProjectile()
    {
        nextAttackTime = Time.time + attackCooldown;

        if (projectilePrefab == null || firePoint == null)
            return;

        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);

        IEnemyProjectile enemyProjectile = projectile.GetComponent<IEnemyProjectile>();
        if (enemyProjectile != null)
            enemyProjectile.Init(hitMask);

        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = firePoint.forward * projectileSpeed;
        }

        Destroy(projectile, 5f);
    }
}