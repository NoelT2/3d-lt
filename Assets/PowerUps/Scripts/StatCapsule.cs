using UnityEngine;
using UnityEngine.InputSystem;

public class StatCapsule : MonoBehaviour
{
    public enum StatType { Damage, MoveSpeed, DashCooldown, GrappleReload, MaxHealth }

    [Header("What this capsule boosts")]
    [SerializeField] private StatType stat;
    [SerializeField] private float amount = 0.1f; // additive delta, e.g. 0.1 = +10%

    [Header("Interaction")]
    [SerializeField] private GameObject consumeVfx;

    private bool playerInRange;
    private bool consumed;
    private PlayerStats targetStats;

    private void OnTriggerEnter(Collider other)
    {
        if (consumed) return;
        PlayerStats stats = other.GetComponentInParent<PlayerStats>();
        if (stats != null)
        {
            playerInRange = true;
            targetStats = stats;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerStats>() != null)
            playerInRange = false;
    }

    private void Update()
    {
        if (!playerInRange || consumed || Keyboard.current == null) return;

        if (Keyboard.current.fKey.wasPressedThisFrame)
            Consume();
    }

    private void Consume()
    {
        consumed = true;

        switch (stat)
        {
            case StatType.Damage: targetStats.AddDamageMultiplier(amount); break;
            case StatType.MoveSpeed: targetStats.AddMoveSpeedMultiplier(amount); break;
            case StatType.DashCooldown: targetStats.AddDashCooldownMultiplier(-amount); break;
            case StatType.GrappleReload: targetStats.AddGrappleReloadMultiplier(-amount); break;
            case StatType.MaxHealth: targetStats.AddMaxHealthMultiplier(amount); break;
        }

        Debug.Log($"Capsule consumed: {stat} {(amount >= 0 ? "+" : "")}{amount:P0}");

        if (consumeVfx != null)
            Instantiate(consumeVfx, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}