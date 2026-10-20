using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class QuillController : MonoBehaviour
{
    [Header("References")]
    public Transform bone;              // QuillBone
    public Transform glassSphere;       // GlassSphere
    public Renderer quillRenderer;      // Quill
    public Camera cam;                  // leave empty to use Main Camera

    [Header("Aiming")]
    public Vector3 localTipAxis = Vector3.up; // which bone axis points along the quill
    public float aimSpeed = 10f;
    public float planeDistance = 1f;    // how far in front of the sphere the cursor plane sits

    [Header("Spin")]
    public float sphereSpinSpeed = 30f; // degrees per second around Y
    public float quillRollSpeed = 90f;  // degrees per second around the quill's own axis

    [Header("Glow")]
    [ColorUsage(false, true)]
    public Color glowColor = new Color(0.3f, 0.8f, 1f) * 4f;
    public float glowFadeSpeed = 8f;

    float roll;
    float glow;
    Material quillMat;

    void Start()
    {
        if (cam == null) cam = Camera.main;
        quillMat = quillRenderer.material;      // makes an instance
        quillMat.EnableKeyword("_EMISSION");
    }

    void Update()
    {
        // ---------- Spin ----------
        if (glassSphere != null)
            glassSphere.Rotate(0f, sphereSpinSpeed * Time.deltaTime, 0f, Space.World);
        roll += quillRollSpeed * Time.deltaTime;

        // ---------- Aim ----------
        Vector2 mouse = GetMousePosition();
        Ray ray = cam.ScreenPointToRay(mouse);

        // Invisible plane in front of the sphere, facing the camera
        Vector3 planePoint = bone.position - cam.transform.forward * planeDistance;
        Plane plane = new Plane(-cam.transform.forward, planePoint);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 target = ray.GetPoint(enter);
            Vector3 dir = (target - bone.position).normalized;

            Vector3 currentTip = bone.TransformDirection(localTipAxis);
            Quaternion aimed = Quaternion.FromToRotation(currentTip, dir) * bone.rotation;
            aimed = Quaternion.AngleAxis(roll, dir) * aimed;   // roll around the aim direction

            bone.rotation = Quaternion.Slerp(bone.rotation, aimed, aimSpeed * Time.deltaTime);
        }

        // ---------- Glow ----------
        float targetGlow = IsMouseHeld() ? 1f : 0f;
        glow = Mathf.MoveTowards(glow, targetGlow, glowFadeSpeed * Time.deltaTime);
        quillMat.SetColor("_EmissionColor", glowColor * glow);
    }

    Vector2 GetMousePosition()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    bool IsMouseHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }
}