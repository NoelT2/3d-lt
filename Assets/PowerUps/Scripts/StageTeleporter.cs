using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class StageTeleporter : MonoBehaviour
{
    [Header("Reward Choice")]
    [SerializeField] private PowerUpData[] rewardPool;
    [SerializeField] private int choiceCount = 3;

    [Header("Destination")]
    [SerializeField] private string nextSceneName;

    private bool playerInRange;
    private bool triggered;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) playerInRange = false;
    }

    private void Update()
    {
        if (triggered || !playerInRange || Keyboard.current == null) return;
        if (Keyboard.current.fKey.wasPressedThisFrame)
            Interact();
    }

    private void Interact()
    {
        if (RewardSelectionUI.Instance == null || rewardPool.Length == 0)
        {
            GoToNextStage();
            return;
        }

        triggered = true;
        RewardSelectionUI.Instance.Show(BuildChoices(), OnChosen);
    }

    private List<RewardChoice> BuildChoices()
    {
        List<PowerUpData> shuffled = new List<PowerUpData>(rewardPool);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        List<RewardChoice> result = new List<RewardChoice>();
        foreach (PowerUpData data in shuffled.GetRange(0, Mathf.Min(choiceCount, shuffled.Count)))
        {
            result.Add(new RewardChoice
            {
                Title = data.PowerUpName,
                Description = data.Description,
                Icon = data.Icon,
                Payload = data
            });
        }
        return result;
    }

    private void OnChosen(RewardChoice choice)
    {
        RunManager.Instance?.SetPendingReward((PowerUpData)choice.Payload);
        GoToNextStage();
    }

    private void GoToNextStage()
    {
        RunManager.Instance?.AdvanceStage();
        if (!string.IsNullOrEmpty(nextSceneName))
            SceneManager.LoadScene(nextSceneName);
    }
}