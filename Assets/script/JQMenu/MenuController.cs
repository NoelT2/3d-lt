using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    [Header("Buttons Fade-In")]
    public CanvasGroup buttonsGroup;     // the MenuButtons object's CanvasGroup
    public float fadeDelay = 0.5f;       // wait before fading in
    public float fadeDuration = 1.2f;    // fade length in seconds
    public bool fadeInOnStart = true;    // turn off later when the intro sequence calls ShowButtons()

    [Header("Actions")]
    public string gameSceneName = "";    // scene to load when Play is pressed
    public GameObject optionsPanel;      // optional: a panel to show for Options

    void Start()
    {
        if (buttonsGroup != null)
        {
            buttonsGroup.alpha = 0f;
            buttonsGroup.interactable = false;
            buttonsGroup.blocksRaycasts = false;
        }

        if (optionsPanel != null) optionsPanel.SetActive(false);

        if (fadeInOnStart) ShowButtons();
    }

    // Call this from the intro sequence later
    public void ShowButtons()
    {
        StopAllCoroutines();
        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        yield return new WaitForSeconds(fadeDelay);

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            buttonsGroup.alpha = Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }

        buttonsGroup.alpha = 1f;
        buttonsGroup.interactable = true;
        buttonsGroup.blocksRaycasts = true;
    }

    // ---------- Button callbacks (hook these up in each Button's OnClick) ----------

    public void OnPlay()
    {
        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.Log("Play pressed, but no Game Scene Name is set on MenuController.");
            return;
        }
        SceneManager.LoadScene(gameSceneName);
    }

    public void OnOptions()
    {
        if (optionsPanel != null)
            optionsPanel.SetActive(!optionsPanel.activeSelf);
        else
            Debug.Log("Options pressed (no options panel assigned yet).");
    }

    public void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}