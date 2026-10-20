using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ActivePowerUpUIController : MonoBehaviour
{
    [Header("UI Component Bindings")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI hotkeyText;

    /// <summary>
    /// Fungsi yang dipanggil oleh ActivePowerUpUIController untuk memperbarui tampilan slot.
    /// </summary>
    public void UpdateVisuals(Sprite icon, string powerUpName, string bindKey)
    {
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            // Jika icon kosong, sembunyikan gambar agar tidak putih polos, jika ada tampilkan
            iconImage.enabled = (icon != null); 
        }

        if (nameText != null)
        {
            nameText.text = powerUpName;
        }

        if (hotkeyText != null)
        {
            hotkeyText.text = bindKey;
        }
    }
}
