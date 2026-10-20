using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Intro: blinking "Press any key" (camera at point A) -> sphere falls -> splash into water
// -> camera glides from point A down to point B (its position in the scene) -> menu buttons fade in.
// Needs QuillController and MenuController (from the earlier steps) in the project.
// Everything is moved at runtime only, so your scene positions stay as the final menu layout.
public class IntroSequence : MonoBehaviour
{
    enum State { WaitingForKey, Falling, Settling, Done }

    [Header("References")]
    public Transform sphereRoot;               // QuillMenu
    public Camera cam;                         // leave empty to use Main Camera
    public Transform cameraPointA;             // optional marker: where the camera starts (point B = the camera's current spot)
    public QuillController quillController;    // enabled once the intro is finished
    public MenuController menu;                // its ShowButtons() is called at the end
    public CanvasGroup pressKeyGroup;          // "Press any key" text group
    public Transform water;                    // water plane (optional)
    public ParticleSystem splashEffect;        // optional, one-shot burst
    public ParticleSystem bubbleEffect;        // optional, looping bubbles (follows the sphere)

    [Header("Camera A -> B")]
    public float defaultStartHeight = 10f;     // used only if no Camera Point A is assigned
    public float cameraSmoothTime = 0.6f;      // higher = camera lags more behind the falling sphere

    [Header("Fall")]
    public float waterHeight = 4f;             // water surface height above the sphere's final position
    public float gravity = 9f;
    public float maxFallSpeed = 14f;
    public float settleSmoothTime = 1.4f;      // how long it takes to drift to its final spot
    public Vector3 tumbleSpeed = new Vector3(25f, 40f, 10f); // degrees per second while falling

    [Header("Press Any Key Text")]
    public float blinkSpeed = 0.6f;            // full fade in + fade out cycles per second
    [Range(0f, 1f)] public float minAlpha = 0f;   // alpha at the faintest point of the fade
    [Range(0f, 1f)] public float maxAlpha = 1f;   // alpha at the brightest point of the fade

    [Header("Underwater Look")]
    public float fogBlendTime = 1.5f;          // fog thickens after the splash

    [Header("Testing")]
    public bool skipIntro = false;             // tick to jump straight to the menu

    State state = State.WaitingForKey;
    Vector3 restPos;
    Vector3 camAPos, camBPos;
    Quaternion camARot, camBRot;
    float startY;
    float waterY;
    float vel;
    float camProgress, camVel;
    float fogT;
    float underFog, aboveFog;

    void Awake()
    {
        if (cam == null) cam = Camera.main;

        restPos = sphereRoot.position;
        camBPos = cam.transform.position;
        camBRot = cam.transform.rotation;

        waterY = restPos.y + waterHeight;
        if (water != null)
        {
            Vector3 p = water.position;
            water.position = new Vector3(p.x, waterY, p.z);
        }

        if (skipIntro)
        {
            state = State.Done;
            if (pressKeyGroup != null) pressKeyGroup.alpha = 0f;
            return;
        }

        if (menu != null) menu.fadeInOnStart = false;
        if (quillController != null) quillController.enabled = false;

        // Point A: the marker if given, otherwise straight above point B
        if (cameraPointA != null)
        {
            camAPos = cameraPointA.position;
            camARot = cameraPointA.rotation;
        }
        else
        {
            camAPos = camBPos + Vector3.up * defaultStartHeight;
            camARot = camBRot;
        }

        // The sphere starts as far above its final spot as camera A is above camera B,
        // so it is framed exactly the same way at the start as at the end.
        float startOffset = camAPos.y - camBPos.y;
        float minOffset = waterHeight + 0.5f;
        if (startOffset < minOffset)
        {
            Debug.LogWarning("IntroSequence: Camera Point A is too low (it must be higher than the water). Using a higher start.");
            startOffset = minOffset;
        }
        startY = restPos.y + startOffset;

        underFog = RenderSettings.fogDensity;
        aboveFog = underFog * 0.2f;
        RenderSettings.fogDensity = aboveFog;

        sphereRoot.position = new Vector3(restPos.x, startY, restPos.z);
        cam.transform.SetPositionAndRotation(camAPos, camARot);

        if (splashEffect != null) splashEffect.Stop();
        if (bubbleEffect != null) bubbleEffect.Stop();
    }

    void Update()
    {
        if (state == State.Done) return;

        float dt = Time.deltaTime;

        switch (state)
        {
            case State.WaitingForKey:
                Tumble(dt);
                BlinkPrompt();
                if (AnyKeyPressed()) state = State.Falling;
                break;

            case State.Falling:
                Tumble(dt);
                FadePrompt(dt);

                vel = Mathf.Max(vel - gravity * dt, -maxFallSpeed);
                Vector3 p = sphereRoot.position;
                p.y += vel * dt;
                sphereRoot.position = p;

                if (p.y <= waterY)
                {
                    Splash();
                    state = State.Settling;
                }
                break;

            case State.Settling:
                // Slow down and drift to the final spot, carrying the fall speed into the water
                Vector3 s = sphereRoot.position;
                s.y = Mathf.SmoothDamp(s.y, restPos.y, ref vel, settleSmoothTime);
                sphereRoot.position = s;

                // Level out the tumble
                sphereRoot.rotation = Quaternion.Slerp(
                    sphereRoot.rotation, Quaternion.identity, 1f - Mathf.Exp(-2.5f * dt));

                // Thicker fog underwater
                fogT = Mathf.Min(1f, fogT + dt / Mathf.Max(0.01f, fogBlendTime));
                RenderSettings.fogDensity = Mathf.Lerp(aboveFog, underFog, fogT);

                if (Mathf.Abs(s.y - restPos.y) < 0.01f &&
                    Mathf.Abs(vel) < 0.02f &&
                    fogT >= 1f &&
                    camProgress > 0.995f &&
                    Quaternion.Angle(sphereRoot.rotation, Quaternion.identity) < 0.5f)
                {
                    FinishIntro();
                    return;
                }
                break;
        }

        UpdateCamera();
    }

    void LateUpdate()
    {
        // Keep the bubbles centred on the sphere (they simulate in world space)
        if (bubbleEffect != null && state != State.WaitingForKey)
            bubbleEffect.transform.position = sphereRoot.position;
    }

    // ---------- Steps ----------

    void Tumble(float dt)
    {
        sphereRoot.Rotate(tumbleSpeed * dt, Space.World);
    }

    void Splash()
    {
        if (splashEffect != null)
        {
            splashEffect.transform.position = new Vector3(sphereRoot.position.x, waterY, sphereRoot.position.z);
            splashEffect.Play();
        }
        if (bubbleEffect != null)
        {
            bubbleEffect.transform.position = sphereRoot.position;
            bubbleEffect.Play();
        }
    }

    // Camera glides from A to B, following how far the sphere has fallen (a little behind it)
    void UpdateCamera()
    {
        float target = Mathf.InverseLerp(startY, restPos.y, sphereRoot.position.y);
        camProgress = Mathf.SmoothDamp(camProgress, target, ref camVel, Mathf.Max(0.01f, cameraSmoothTime));
        cam.transform.SetPositionAndRotation(
            Vector3.Lerp(camAPos, camBPos, camProgress),
            Quaternion.Slerp(camARot, camBRot, camProgress));
    }

    void FinishIntro()
    {
        state = State.Done;

        sphereRoot.position = restPos;
        sphereRoot.rotation = Quaternion.identity;
        cam.transform.SetPositionAndRotation(camBPos, camBRot);

        if (quillController != null) quillController.enabled = true;
        if (menu != null) menu.ShowButtons();
    }

    // ---------- "Press any key" text ----------

    void BlinkPrompt()
    {
        if (pressKeyGroup == null) return;

        // Smooth cosine pulse: starts fully faded out, rises to max, falls back to min
        float wave = 0.5f - 0.5f * Mathf.Cos(Time.time * blinkSpeed * Mathf.PI * 2f);
        pressKeyGroup.alpha = Mathf.Lerp(minAlpha, maxAlpha, wave);
    }

    void FadePrompt(float dt)
    {
        if (pressKeyGroup == null) return;
        pressKeyGroup.alpha = Mathf.MoveTowards(pressKeyGroup.alpha, 0f, dt * 2f);
    }

    bool AnyKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        bool key = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        bool mouse = Mouse.current != null &&
                     (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame);
        return key || mouse;
#else
        return Input.anyKeyDown;
#endif
    }
}