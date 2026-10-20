using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pause menu (Esc). Same idea as MainMenuController, but with Resume + Quit.
/// Hook the buttons' OnClick to OnResumePressed / OnQuitPressed.
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Root object of the pause menu (the panel containing your sprite + buttons). Starts hidden.")]
    [SerializeField] private GameObject pausePanel;
    [Tooltip("Optional: the Image that shows your pause menu sprite.")]
    [SerializeField] private Image menuImage;
    [Tooltip("Optional: drop your sprite here and it is assigned to Menu Image automatically.")]
    [SerializeField] private Sprite menuSprite;

    [Header("Background Blur (screenshot based, works with Overlay canvases)")]
    [Tooltip("A full-screen RawImage placed BEHIND the pause panel inside PauseCanvas. It shows the blurred screenshot.")]
    [SerializeField] private RawImage blurBackdrop;
    [Tooltip("Higher = blurrier (each step halves the resolution).")]
    [SerializeField, Range(2, 7)] private int blurSteps = 4;
    [SerializeField] private float blurFadeTime = 0.2f;

    [Header("Options")]
    [SerializeField] private bool pauseAudio = true;

    public enum QuitAction { RestartScene, QuitApplication }

    [Header("Quit button")]
    [Tooltip("RestartScene = clean refresh (like End Task): resets time, input, run data and reloads the scene. QuitApplication = close the game / stop Play mode.")]
    [SerializeField] private QuitAction quitAction = QuitAction.RestartScene;

    // Gameplay scripts check InputBlocked at the top of Update(). We do NOT disable them:
    // GrappleController.OnDisable() wipes hook state and momentum, which was cutting
    // your momentum to 0 on resume. timeScale = 0 already freezes all physics/FixedUpdate.
    private static int resumeFrame = -1;

    private RenderTexture blurTexture;
    private float backdropAlpha;
    private Coroutine captureRoutine;

    public static bool IsPaused { get; private set; }

    // Statics survive between Play sessions when "Reload Domain" is off (Enter Play Mode Options),
    // so a stale resumeFrame could keep input blocked after a restart. Reset them every time Play starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsPaused = false;
        resumeFrame = -1;
    }

    // True while paused, and for the frame of resuming (so the Resume click isn't read as a shot/grapple).
    public static bool InputBlocked => IsPaused || Time.frameCount <= resumeFrame;

    private void Awake()
    {
        if (menuImage != null && menuSprite != null)
            menuImage.sprite = menuSprite;

        if (pausePanel != null) pausePanel.SetActive(false);
        if (blurBackdrop != null) blurBackdrop.gameObject.SetActive(false);
        IsPaused = false;
        resumeFrame = -1;
    }

    private void Update()
    {
        UpdateBackdropFade();

        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;

        // Don't pause over the reward-selection screen (it manages timeScale + cursor itself).
        if (!IsPaused && RewardSelectionUI.Instance != null && RewardSelectionUI.Instance.IsOpen) return;

        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;

        Time.timeScale = 0f;
        if (pauseAudio) AudioListener.pause = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // The panel is shown after the screenshot is taken, so the menu itself isn't in the blurred image.
        if (captureRoutine != null) StopCoroutine(captureRoutine);
        captureRoutine = StartCoroutine(CaptureBlurAndShowPanel());
    }

    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;

        if (captureRoutine != null) { StopCoroutine(captureRoutine); captureRoutine = null; }
        if (pausePanel != null) pausePanel.SetActive(false);

        Time.timeScale = 1f;
        if (pauseAudio) AudioListener.pause = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        resumeFrame = Time.frameCount;
    }

    public void OnResumePressed() => Resume();

    public void OnQuitPressed()
    {
        ResetGlobalState();

        if (quitAction == QuitAction.RestartScene)
        {
            RestartScene();
            return;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Application.Quit() does nothing in the Editor
#else
        Application.Quit();
#endif
    }

    // Everything that outlives a scene load or a Play session.
    private void ResetGlobalState()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        IsPaused = false;
        resumeFrame = -1;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void RestartScene()
    {
        // RunManager is DontDestroyOnLoad: if it survives, the new scene keeps old run data
        // (stage index, affinity, pending reward) and the new RunManager is thrown away as a duplicate.
        if (RunManager.Instance != null)
            Destroy(RunManager.Instance.gameObject);

        Scene active = SceneManager.GetActiveScene();
        if (active.buildIndex < 0)
        {
            Debug.LogError("PauseMenuController: scene '" + active.name + "' is not in File > Build Profiles (Scene List). Add it, or the restart can't load it.");
            return;
        }

        SceneManager.LoadScene(active.buildIndex);
        Resources.UnloadUnusedAssets();
    }

    private IEnumerator CaptureBlurAndShowPanel()
    {
        // Wait until the frame (world + HUD) is fully rendered, then grab it.
        yield return new WaitForEndOfFrame();

        if (IsPaused && blurBackdrop != null)
        {
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            BuildBlur(shot);
            Destroy(shot);

            blurBackdrop.texture = blurTexture;
            backdropAlpha = 0f;
            SetBackdropAlpha(0f);
            blurBackdrop.gameObject.SetActive(true);
        }

        if (IsPaused && pausePanel != null) pausePanel.SetActive(true);
        captureRoutine = null;
    }

    // Cheap blur: repeatedly halve the resolution with bilinear filtering, then scale back up the same way.
    private void BuildBlur(Texture2D source)
    {
        int w = source.width;
        int h = source.height;

        Texture current = source;
        RenderTexture tempToRelease = null;

        for (int i = 0; i < blurSteps; i++)
        {
            w = Mathf.Max(2, w / 2);
            h = Mathf.Max(2, h / 2);
            RenderTexture next = RenderTexture.GetTemporary(w, h, 0);
            next.filterMode = FilterMode.Bilinear;
            Graphics.Blit(current, next);
            if (tempToRelease != null) RenderTexture.ReleaseTemporary(tempToRelease);
            tempToRelease = next;
            current = next;
        }

        for (int i = 0; i < blurSteps - 1; i++)
        {
            w *= 2;
            h *= 2;
            RenderTexture next = RenderTexture.GetTemporary(w, h, 0);
            next.filterMode = FilterMode.Bilinear;
            Graphics.Blit(current, next);
            if (tempToRelease != null) RenderTexture.ReleaseTemporary(tempToRelease);
            tempToRelease = next;
            current = next;
        }

        if (blurTexture == null || blurTexture.width != w || blurTexture.height != h)
        {
            if (blurTexture != null) blurTexture.Release();
            blurTexture = new RenderTexture(w, h, 0);
            blurTexture.filterMode = FilterMode.Bilinear;
        }

        Graphics.Blit(current, blurTexture);
        if (tempToRelease != null) RenderTexture.ReleaseTemporary(tempToRelease);
    }

    private void UpdateBackdropFade()
    {
        if (blurBackdrop == null || !blurBackdrop.gameObject.activeSelf) return;

        float target = IsPaused ? 1f : 0f;
        backdropAlpha = Mathf.MoveTowards(backdropAlpha, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, blurFadeTime));
        SetBackdropAlpha(backdropAlpha);

        if (!IsPaused && backdropAlpha <= 0f)
            blurBackdrop.gameObject.SetActive(false);
    }

    private void SetBackdropAlpha(float a)
    {
        Color c = blurBackdrop.color;
        c.a = a;
        blurBackdrop.color = c;
    }

    private void OnDestroy()
    {
        if (blurTexture != null) blurTexture.Release();

        if (IsPaused)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            IsPaused = false;
        }
    }
}