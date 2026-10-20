using UnityEngine;
using UnityEngine.UI;

public class ChargeCrosshairUI : MonoBehaviour
{
    [SerializeField] private WeaponController weapon;
    [SerializeField] private Image pelletTemplate;
    [SerializeField] private Image centerDot;

    [Header("Look")]
    [SerializeField] private int pelletCount = 8;
    [SerializeField] private float startRadius = 90f;
    [SerializeField] private float spinSpeed = 120f;
    [SerializeField] private float flashSpeed = 20f;
    [SerializeField] private Color chargingColor = new Color(1f, 0.75f, 0.2f, 1f);
    [SerializeField] private Color fullColor = Color.white;

    private RectTransform[] pellets;
    private Image[] pelletImages;
    private bool visible;

    private void Awake()
    {
        pellets = new RectTransform[pelletCount];
        pelletImages = new Image[pelletCount];

        for (int i = 0; i < pelletCount; i++)
        {
            Image img = Instantiate(pelletTemplate, transform);
            img.gameObject.SetActive(true);
            pelletImages[i] = img;
            pellets[i] = img.rectTransform;
        }

        pelletTemplate.gameObject.SetActive(false);
        SetVisible(false, true);
    }

    private void Update()
    {
        float c = (weapon != null && weapon.IsCharging) ? weapon.Charge01 : 0f;

        if (c <= 0.001f)
        {
            SetVisible(false, false);
            return;
        }

        SetVisible(true, false);

        bool full = c >= 0.999f;

        // Pellets slide inward and spin, then disappear when merged
        float radius = Mathf.Lerp(startRadius, 0f, Mathf.SmoothStep(0f, 1f, c));
        float spin = Time.time * spinSpeed * (1f - c);

        for (int i = 0; i < pelletCount; i++)
        {
            float angle = (i * 360f / pelletCount + spin) * Mathf.Deg2Rad;
            pellets[i].anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            Color col = chargingColor;
            col.a = Mathf.Lerp(0.35f, 1f, c);
            pelletImages[i].color = col;
            pelletImages[i].enabled = !full;
        }

        // Center dot grows with charge, then flashes when full
        if (full)
        {
            float pulse = Mathf.Sin(Time.time * flashSpeed) * 0.5f + 0.5f;
            centerDot.color = Color.Lerp(chargingColor, fullColor, pulse);
            centerDot.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.1f, 1.5f, pulse);
        }
        else
        {
            Color col = chargingColor;
            col.a = Mathf.Lerp(0.2f, 1f, c);
            centerDot.color = col;
            centerDot.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, c);
        }
    }

    private void SetVisible(bool show, bool force)
    {
        if (!force && show == visible) return;
        visible = show;

        centerDot.enabled = show;
        foreach (Image img in pelletImages)
            img.enabled = show;
    }
}