using UnityEngine;

public class MeleeAttack : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackDamage = 15f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private float attackKnockback = 4f;

    [Header("Target")]
    [SerializeField] private LayerMask playerMask;

    private float nextAttackTime;
    private IEnemyMovement movement;

    private void Awake()
    {
        movement = GetComponent<IEnemyMovement>();
    }

    private void Update()
    {
        if (!movement.HasTarget || Time.time < nextAttackTime)
            return;

        if (movement.IsInRange(attackRange))
        {
            TryAttack();
        }
    }

    private void TryAttack()
    {
        nextAttackTime = Time.time + attackCooldown;

        Collider[] hits = Physics.OverlapSphere(transform.position, attackRange, playerMask);
        foreach (Collider hit in hits)
        {
            PlayerHealth playerHealth = hit.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                Vector3 knockbackDir = hit.transform.position - transform.position;
                knockbackDir.y = 0f;

                playerHealth.TakeDamage(attackDamage, knockbackDir, attackKnockback);
            }

            Debug.Log($"{name} melee-hit {hit.name} for {attackDamage}");
        }

        // trigger attack animation here
    }
}