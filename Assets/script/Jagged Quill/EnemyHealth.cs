using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Knockback")]
    [SerializeField] private float knockbackResistance = 1f;

    [Header("Movement Immunity")]
    [Tooltip("Permanently immune to knockdown and pull, independent of armor. Use this for Giants/Bosses.")]
    [SerializeField] private bool immuneToKnockdownAndPull = false;

    [Header("Hit Flinch")]
    [Tooltip("How long a light hit suppresses GroundMovement's velocity override, letting the impulse visibly budge the enemy even if it's too weak to trigger a full knockdown.")]
    [SerializeField] private float flinchSuppressDuration = 0.2f;

    private float health;
    private Rigidbody enemyRigidbody;
    private EnemyArmor armor;
    private EnemyHitFlash hitFlash;
    public float KnockbackResistance => knockbackResistance;
    public bool IsArmored => armor != null;
    public bool IsImmuneToKnockdownAndPull => immuneToKnockdownAndPull || IsArmored;

    public void SetArmor(EnemyArmor newArmor) => armor = newArmor;

    private void Awake()
    {
        health = maxHealth;
        enemyRigidbody = GetComponent<Rigidbody>();

        // Every enemy with EnemyHealth gets the red hit flash automatically.
        hitFlash = GetComponent<EnemyHitFlash>();
        if (hitFlash == null) hitFlash = gameObject.AddComponent<EnemyHitFlash>();
    }

    public void TakeDamage(float damageAmount)
    {
        TakeDamage(
            damageAmount,
            Vector3.zero,
            0f,
            transform.position
        );
    }

    public void TakeDamage(
        float damageAmount,
        Vector3 knockbackDirection,
        float knockbackForce)
    {
        TakeDamage(
            damageAmount,
            knockbackDirection,
            knockbackForce,
            transform.position
        );
    }

    public void TakeDamage(
        float damageAmount,
        Vector3 knockbackDirection,
        float knockbackForce,
        Vector3 hitPoint)
    {
        if (damageAmount <= 0f)
            return;
        
        if (armor != null && armor.AbsorbDamage(damageAmount))
        {
            hitFlash.Flash();
            Debug.Log($"{gameObject.name}'s armor absorbed {damageAmount} damage.");
            return; // no health loss, no knockback, while armored
        }

        health -= damageAmount;
        hitFlash.Flash();
        GameEvents.RaiseEnemyHit(this, damageAmount);

        if (knockbackForce > 0f &&
            enemyRigidbody != null &&
            !enemyRigidbody.isKinematic &&
            knockbackDirection.sqrMagnitude > 0.001f)
        {
            Vector3 force =
                knockbackDirection.normalized * (knockbackForce / Mathf.Max(0.01f, knockbackResistance));

            enemyRigidbody.AddForceAtPosition(
                force,
                hitPoint,
                ForceMode.Impulse
            );
            GetComponent<EnemyKnockdownState>()?.NotifyForceApplied(force.magnitude);
            GetComponent<GroundMovement>()?.SuppressMovementOverride(flinchSuppressDuration);
        }

        Debug.Log(
            $"{gameObject.name} took {damageAmount} damage. Health: {health}"
        );

        if (health <= 0f)
        {
            Die();
        }
    }

    private bool collisionDamageInProgress;
    private ImpactInfo lastCollisionImpact;

    /// <summary>Damage that comes from a collision. If it kills, OnEnemyKilledByCollision is raised (knockout effect).</summary>
    public void TakeCollisionDamage(float damage, ImpactInfo info, Vector3 knockbackDirection, float knockbackForce)
    {
        lastCollisionImpact = info;
        collisionDamageInProgress = true;
        TakeDamage(damage, knockbackDirection, knockbackForce, info.point);
        collisionDamageInProgress = false;
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} died.");
        if (collisionDamageInProgress)
            GameEvents.RaiseEnemyKilledByCollision(this, lastCollisionImpact);
        GameEvents.RaiseEnemyKilled(this);
        Destroy(gameObject);
    }
}