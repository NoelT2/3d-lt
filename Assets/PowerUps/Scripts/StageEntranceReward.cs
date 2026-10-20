using UnityEngine;

public class StageEntranceReward : MonoBehaviour
{
    [Header("First-Stage Fallback")]
    [Tooltip("If there's no pending reward (e.g. first stage of a run), pick randomly from this pool.")]
    [SerializeField] private PowerUpData[] startingRewardPool;

    [Header("Visual (optional)")]
    [SerializeField] private GameObject grantVfx;

    private bool granted;

    private void Start()
    {
        // Small delay-free guard: only try once per scene load.
        if (granted) return;
        TryGrantEntranceReward();
    }

    private void TryGrantEntranceReward()
    {
        if (RunManager.Instance == null)
        {
            Debug.LogWarning("StageEntranceReward: no RunManager in scene.");
            return;
        }

        PowerUpData rewardToGrant = RunManager.Instance.HasPendingReward
            ? RunManager.Instance.PendingReward
            : RollStartingReward();

        if (rewardToGrant == null)
        {
            Debug.LogWarning("StageEntranceReward: nothing to grant (no pending reward, empty starting pool).");
            return;
        }

        PowerUpManager playerManager = FindAnyObjectByType<PowerUpManager>();
        if (playerManager == null)
        {
            Debug.LogWarning("StageEntranceReward: no PowerUpManager found in scene.");
            return;
        }

        granted = true;
        playerManager.Grant(rewardToGrant);
        RunManager.Instance.ClearPendingReward();

        if (grantVfx != null)
            Instantiate(grantVfx, transform.position, Quaternion.identity);

        Debug.Log($"Stage entrance granted: {rewardToGrant.PowerUpName}");
    }

    private PowerUpData RollStartingReward()
    {
        if (startingRewardPool == null || startingRewardPool.Length == 0)
            return null;

        return startingRewardPool[Random.Range(0, startingRewardPool.Length)];
    }
}