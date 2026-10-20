using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes an enemy glow red for a moment whenever it takes damage.
/// Added automatically by EnemyHealth, so every enemy with EnemyHealth gets it (no prefab changes needed).
/// Uses MaterialPropertyBlocks, so shared materials are never modified and batching isn't broken.
/// Works with URP/HDRP (_BaseColor) and Built-in Standard (_Color); emission is added when the material has _EmissionColor.
/// </summary>
public class EnemyHitFlash : MonoBehaviour
{
    [Header("Flash")]
    [SerializeField] private Color flashColor = new Color(1f, 0.1f, 0.05f, 1f);
    [SerializeField] private float flashDuration = 0.25f;

    [Header("Look")]
    [Tooltip("How far the surface color is pulled toward the flash color at the peak of the flash (0-1).")]
    [SerializeField, Range(0f, 1f)] private float tintStrength = 0.65f;
    [Tooltip("Emission added at the peak of the flash. Needs Emission enabled on the material to show; the tint works regardless.")]
    [SerializeField] private float emissionIntensity = 3f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private class Slot
    {
        public Renderer renderer;
        public int materialIndex;
        public int colorId;          // -1 if the material has no tintable color
        public Color baseColor;
        public bool hasEmission;
        public Color baseEmission;
    }

    private readonly List<Slot> slots = new List<Slot>();
    private MaterialPropertyBlock block;
    private float flashAmount; // 1 = peak, 0 = none

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        CacheRenderers();
    }

    public void Flash()
    {
        if (flashAmount <= 0f) CacheRenderers(); // pick up renderers added since Awake (e.g. armor visuals)
        flashAmount = 1f;
        Apply();
    }

    private void Update()
    {
        if (flashAmount <= 0f) return;

        flashAmount -= Time.deltaTime / Mathf.Max(0.01f, flashDuration);

        if (flashAmount <= 0f)
        {
            flashAmount = 0f;
            Clear();
            return;
        }

        Apply();
    }

    private void CacheRenderers()
    {
        slots.Clear();

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;

            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) continue;

                Slot slot = new Slot { renderer = r, materialIndex = i, colorId = -1 };

                if (m.HasProperty(BaseColorId)) slot.colorId = BaseColorId;
                else if (m.HasProperty(ColorId)) slot.colorId = ColorId;

                if (slot.colorId != -1) slot.baseColor = m.GetColor(slot.colorId);

                if (m.HasProperty(EmissionColorId))
                {
                    slot.hasEmission = true;
                    slot.baseEmission = m.GetColor(EmissionColorId);
                }

                if (slot.colorId != -1 || slot.hasEmission) slots.Add(slot);
            }
        }
    }

    private void Apply()
    {
        // Ease so the flash pops, then fades smoothly.
        float t = flashAmount * flashAmount;

        foreach (Slot s in slots)
        {
            if (s.renderer == null) continue;

            s.renderer.GetPropertyBlock(block, s.materialIndex);

            if (s.colorId != -1)
            {
                Color tinted = Color.Lerp(s.baseColor, flashColor, tintStrength * t);
                tinted.a = s.baseColor.a;
                block.SetColor(s.colorId, tinted);
            }

            if (s.hasEmission)
                block.SetColor(EmissionColorId, s.baseEmission + flashColor * (emissionIntensity * t));

            s.renderer.SetPropertyBlock(block, s.materialIndex);
        }
    }

    private void Clear()
    {
        foreach (Slot s in slots)
        {
            if (s.renderer != null)
                s.renderer.SetPropertyBlock(null, s.materialIndex);
        }
    }

    private void OnDisable()
    {
        if (flashAmount > 0f)
        {
            flashAmount = 0f;
            Clear();
        }
    }
}