using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(RewardChoiceOption))]
public class RewardChoiceInteractable : MonoBehaviour
{
    [SerializeField] private float interactRange = 2.5f;
    private Transform player;
    private RewardChoiceOption option;
    private bool playerInRange;

    private void Awake()
    {
        option = GetComponent<RewardChoiceOption>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            player = other.transform;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInRange = false;
    }

    private void Update()
    {
        if (!playerInRange || Keyboard.current == null || player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);
        if (distance > interactRange) return;

        if (Keyboard.current.fKey.wasPressedThisFrame)
            option.Choose();
    }
}