using UnityEngine;

public class PU_ManualFireball : PowerUp
{
    [SerializeField] private float damage = 30f;
    [SerializeField] private float radius = 4f;
    [SerializeField] private LayerMask enemyMask = ~0;

    public override bool Use()
    {
        Debug.Log("Fireball used!");

        Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward * 3f, radius, enemyMask);
        foreach (Collider hit in hits)
        {
            EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();
            if (enemy != null) enemy.TakeDamage(damage);
        }

        return true;
    }
}