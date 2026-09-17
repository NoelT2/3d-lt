using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;

    private float health;
    private Rigidbody enemyRigidbody;

    private void Awake()
    {
        health = maxHealth;
        enemyRigidbody = GetComponent<Rigidbody>();
    }

    // Damage without knockback
    public void TakeDamage(float damageAmount)
    {
        TakeDamage(
            damageAmount,
            Vector3.zero,
            0f,
            transform.position
        );
    }

    // Damage with knockback, using the enemy center as the force point
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

    // Damage with knockback and a specific hit point
    public void TakeDamage(
        float damageAmount,
        Vector3 knockbackDirection,
        float knockbackForce,
        Vector3 hitPoint)
    {
        if (damageAmount <= 0f)
            return;

        health -= damageAmount;

        if (knockbackForce > 0f &&
            enemyRigidbody != null &&
            !enemyRigidbody.isKinematic &&
            knockbackDirection.sqrMagnitude > 0.001f)
        {
            Vector3 force =
                knockbackDirection.normalized * knockbackForce;

            enemyRigidbody.AddForceAtPosition(
                force,
                hitPoint,
                ForceMode.Impulse
            );
        }

        Debug.Log(
            $"{gameObject.name} took {damageAmount} damage. Health: {health}"
        );

        if (health <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} died.");
        Destroy(gameObject);
    }
}