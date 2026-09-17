using UnityEngine;

public class BulletDamage : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private float damage = 10f;

    [Header("Knockback")]
    [SerializeField] private float knockbackRange = 15f;
    [SerializeField] private float knockbackForce = 8f;

    private bool hasHit = false;
    private Vector3 spawnPosition;

    private void Awake()
    {
        // The bullet's original position, usually the weapon muzzle
        spawnPosition = transform.position;

        LayerSetup();
    }

    private void OnTriggerEnter(Collider other)
    {
        Vector3 hitPoint = other.bounds.ClosestPoint(transform.position);
        if (hasHit)
            return;
        // Ignore other bullets
        if (other.GetComponentInParent<BulletDamage>() != null)
            return;
        hasHit = true;
        EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();
        if (enemy != null)
        {
            float distanceFromSpawn =
                Vector3.Distance(spawnPosition, enemy.transform.position);
            if (distanceFromSpawn <= knockbackRange)
            {
                Vector3 knockbackDirection =
                    enemy.transform.position - spawnPosition;
                knockbackDirection.y = 0f;
                enemy.TakeDamage(
                    damage,
                    knockbackDirection,
                    knockbackForce,
                    hitPoint
                );
                Debug.Log(
                    $"Enemy hit within knockback range: " +
                    $"{distanceFromSpawn:F1}m"
                );
            }
            else
            {
                // Damage only; no knockback
                enemy.TakeDamage(damage);
                Debug.Log(
                    $"Enemy hit outside knockback range: " +
                    $"{distanceFromSpawn:F1}m"
                );
            }
        }
        else
        {
            Debug.Log($"Bullet hit environment: {other.gameObject.name}");
        }
        Destroy(gameObject);
    }

    private void LayerSetup()
    {
        int bulletLayer = LayerMask.NameToLayer("Bullet");

        if (bulletLayer == -1)
        {
            Debug.LogError("The Bullet layer does not exist.");
            return;
        }

        Physics.IgnoreLayerCollision(
            bulletLayer,
            bulletLayer,
            true
        );
    }
}