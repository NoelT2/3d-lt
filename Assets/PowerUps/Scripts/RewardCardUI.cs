using System;
using UnityEngine;
using UnityEngine.UI;

public class RewardCardUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private Text titleText;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Button button;

    private Action onClicked;

    public void Setup(RewardChoice choice, Action onClick)
    {
        if (icon != null) icon.sprite = choice.Icon;
        if (titleText != null) titleText.text = choice.Title;
        if (descriptionText != null) descriptionText.text = choice.Description;

        onClicked = onClick;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClicked?.Invoke());
    }
}