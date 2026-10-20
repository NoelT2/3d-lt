using UnityEngine;

public class EnemyProjectile : MonoBehaviour, IEnemyProjectile
{
    [SerializeField] private float damage = 10f;
    [SerializeField] private float knockbackForce = 4f;

    private LayerMask hitMask;

    // Called by RangedAttack right after spawning
    public void Init(LayerMask mask)
    {
        hitMask = mask;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger) return;

        // Pass through enemies and other projectiles
        if (other.GetComponentInParent<EnemyHealth>() != null) return;
        if (other.GetComponentInParent<IEnemyProjectile>() != null) return;

        bool isTarget = (hitMask.value & (1 << other.gameObject.layer)) != 0;

        if (isTarget)
        {
            PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                Vector3 dir = transform.forward;
                dir.y = 0f;
                playerHealth.TakeDamage(damage, dir, knockbackForce);
            }
        }

        // Hit the player or a wall: either way the bullet is done
        Destroy(gameObject);
    }
}