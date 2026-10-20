using UnityEngine;
using UnityEngine.UI;

public class TwoFrameFlipbook : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite frameA;
    [SerializeField] private Sprite frameB;
    [SerializeField] private float frameDuration = 0.5f;
    [Tooltip("Keeps animating while Time.timeScale = 0 (needed for the pause menu).")]
    [SerializeField] private bool useUnscaledTime = true;

    private float timer;
    private bool showingA = true;

    private void Awake()
    {
        if (targetImage == null) targetImage = GetComponent<Image>();
        if (targetImage != null) targetImage.sprite = frameA;
    }

    private void Update()
    {
        timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (timer >= frameDuration)
        {
            timer -= frameDuration;
            showingA = !showingA;
            targetImage.sprite = showingA ? frameA : frameB;
        }
    }
}