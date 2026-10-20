using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CapsuleStation : MonoBehaviour
{
    [System.Serializable]
    public class StatOption
    {
        public StatCapsule.StatType stat;
        public float amount = 0.1f;
        public string title;
        [TextArea] public string description;
        public Sprite icon;
    }

    [SerializeField] private StatOption[] pool;
    [SerializeField] private int choiceCount = 3;

    private bool used;
    private bool playerInRange;
    private PlayerStats targetStats;

    private void OnTriggerEnter(Collider other)
    {
        PlayerStats stats = other.GetComponentInParent<PlayerStats>();
        if (stats != null) { playerInRange = true; targetStats = stats; }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerStats>() != null) playerInRange = false;
    }

    private void Update()
    {
        if (used || !playerInRange || Keyboard.current == null) return;
        if (Keyboard.current.fKey.wasPressedThisFrame)
            Open();
    }

    private void Open()
    {
        if (RewardSelectionUI.Instance == null || pool.Length == 0) return;

        List<StatOption> picks = new List<StatOption>(pool).GetRange(0, Mathf.Min(choiceCount, pool.Length));

        List<RewardChoice> choices = new List<RewardChoice>();
        foreach (StatOption opt in picks)
        {
            choices.Add(new RewardChoice
            {
                Title = opt.title,
                Description = opt.description,
                Icon = opt.icon,
                Payload = opt
            });
        }

        used = true;
        RewardSelectionUI.Instance.Show(choices, OnChosen);
    }

    private void OnChosen(RewardChoice choice)
    {
        StatOption opt = (StatOption)choice.Payload;

        switch (opt.stat)
        {
            case StatCapsule.StatType.Damage: targetStats.AddDamageMultiplier(opt.amount); break;
            case StatCapsule.StatType.MoveSpeed: targetStats.AddMoveSpeedMultiplier(opt.amount); break;
            case StatCapsule.StatType.DashCooldown: targetStats.AddDashCooldownMultiplier(-opt.amount); break;
            case StatCapsule.StatType.GrappleReload: targetStats.AddGrappleReloadMultiplier(-opt.amount); break;
            case StatCapsule.StatType.MaxHealth: targetStats.AddMaxHealthMultiplier(opt.amount); break;
        }

        Destroy(gameObject);
    }
}