using System.Collections.Generic;
using UnityEngine;

public class PowerUpManager : MonoBehaviour
{
    [SerializeField] private ActivePowerUpController activeController;

    // Passives don't need ordering/stacking UI, just existence + stack count.
    private readonly Dictionary<PowerUpData, PowerUp> passives = new Dictionary<PowerUpData, PowerUp>();
    private readonly Dictionary<PowerUpData, int> passiveStacks = new Dictionary<PowerUpData, int>();

    private void Awake()
    {
        if (activeController == null)
            activeController = GetComponent<ActivePowerUpController>();
    }

    // Single entry point for pickups, reward doors, capsules, starting power-up, etc.
    public void Grant(PowerUpData data)
    {
        if (data == null)
        {
            Debug.LogWarning("Tried to grant a null PowerUpData.");
            return;
        }

        if (data.BehaviorPrefab == null)
        {
            Debug.LogError($"{data.PowerUpName} has no Behavior Prefab assigned.");
            return;
        }

        if (RunManager.Instance != null) // NEW
            RunManager.Instance.RegisterAffinity(data.Type);

        if (data.Category == PowerUpCategory.Active)
            activeController.GrantActive(data);
        else
            GrantPassive(data);

        Debug.Log($"Affinity[{data.Type}] = {RunManager.Instance.GetAffinity(data.Type)}");
    }

    private void GrantPassive(PowerUpData data)
    {
        if (passives.TryGetValue(data, out PowerUp existing))
        {
            passiveStacks[data]++;
            existing.OnStackAdded(passiveStacks[data]);
        }
        else
        {
            PowerUp instance = Instantiate(data.BehaviorPrefab, transform);
            instance.Initialize(data);
            passives[data] = instance;
            passiveStacks[data] = 1;
        }
    }

    public int GetPassiveStackCount(PowerUpData data) =>
        passiveStacks.TryGetValue(data, out int count) ? count : 0;

    public bool HasPassive(PowerUpData data) => passives.ContainsKey(data);

    // Called on run reset/death — clears everything so a new run starts clean.
    public void ClearAll()
    {
        foreach (var kvp in passives)
        {
            kvp.Value.OnRemoved();
            Destroy(kvp.Value.gameObject);
        }
        passives.Clear();
        passiveStacks.Clear();

        // Actives clear themselves out as they're used, but a run reset should
        // wipe whatever's left too.
        foreach (var entry in activeController.Inventory.Actives)
        {
            entry.instance.OnRemoved();
        }
        // Inventory doesn't expose a bulk-clear yet — add one now:
        activeController.Inventory.ClearAll();
    }
}