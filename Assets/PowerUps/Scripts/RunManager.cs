using System.Collections.Generic;
using UnityEngine;

public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    [Header("Run State")]
    [SerializeField] private int currentStageIndex;
    public int CurrentStageIndex => currentStageIndex;

    // How many power-ups of each type have been picked up this run.
    private readonly Dictionary<PowerUpType, int> affinity = new Dictionary<PowerUpType, int>();

    // The reward chosen at the previous transition room, waiting to be
    // granted at the next stage's entrance teleport space.
    public PowerUpData PendingReward { get; private set; }
    public bool HasPendingReward => PendingReward != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Call this whenever a power-up is actually granted to the player
    // (from PowerUpManager.Grant, ideally), so affinity always reflects
    // what the player is actually running, not just what was offered.
    public void RegisterAffinity(PowerUpType type)
    {
        if (type == PowerUpType.Neutral) return; // neutral doesn't build toward anything

        if (!affinity.ContainsKey(type))
            affinity[type] = 0;

        affinity[type]++;
    }

    public int GetAffinity(PowerUpType type) =>
        affinity.TryGetValue(type, out int count) ? count : 0;

    // Called from a transition room when the player commits to a reward choice.
    public void SetPendingReward(PowerUpData data)
    {
        PendingReward = data;
    }

    // Called by the next stage's entrance teleport space once it grants the reward.
    public void ClearPendingReward()
    {
        PendingReward = null;
    }

    public void AdvanceStage()
    {
        currentStageIndex++;
    }

    // Call this on death / run end to fully reset for a new run.
    public void ResetRun()
    {
        currentStageIndex = 0;
        affinity.Clear();
        PendingReward = null;
    }
}