using UnityEngine;

public class PullProjectile : MonoBehaviour, IEnemyProjectile
{
    [SerializeField] private float damage = 5f;
    [SerializeField] private float pullDistance = 4f;
    [SerializeField] private float stopDistance = 1.5f; // never yank the player into the enemy

    private LayerMask hitMask;
    private Vector3 spawnPosition;

    private void Awake()
    {
        spawnPosition = transform.position; // the shooter's fire point
    }

    public void Init(LayerMask mask)
    {
        hitMask = mask;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger) return;

        if (other.GetComponentInParent<EnemyHealth>() != null) return;
        if (other.GetComponentInParent<IEnemyProjectile>() != null) return;

        bool isTarget = (hitMask.value & (1 << other.gameObject.layer)) != 0;

        if (isTarget)
        {
            PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                Vector3 toShooter = spawnPosition - other.transform.position;
                toShooter.y = 0f;

                // Clamp so the pull can't overshoot past the shooter
                float pull = Mathf.Min(pullDistance, Mathf.Max(0f, toShooter.magnitude - stopDistance));

                playerHealth.TakeDamage(damage, toShooter, pull);
            }
        }

        Destroy(gameObject);
    }
}