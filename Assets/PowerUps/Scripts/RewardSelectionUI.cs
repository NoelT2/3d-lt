using System;
using System.Collections.Generic;
using UnityEngine;

public class RewardSelectionUI : MonoBehaviour
{
    public static RewardSelectionUI Instance { get; private set; }

    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RewardCardUI[] cards; // Tepat 3 buah kartu UI di dalam Canvas

    private Action<RewardChoice> onChosen;
    public bool IsOpen { get; private set; }

    private void Awake()
    {
        Instance = this;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void Show(List<RewardChoice> choices, Action<RewardChoice> callback)
    {
        onChosen = callback;
        IsOpen = true;

        if (panelRoot != null) panelRoot.SetActive(true);

        // Kunci Kursor Terbuka: Membuka kunci mouse agar pemain bisa memilih kartu dengan bebas
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f; // Pause jalannya game saat memilih boons/stats

        for (int i = 0; i < cards.Length; i++)
        {
            if (i < choices.Count)
            {
                RewardChoice choice = choices[i];
                cards[i].gameObject.SetActive(true);
                cards[i].Setup(choice, () => Choose(choice));
            }
            else
            {
                cards[i].gameObject.SetActive(false);
            }
        }
    }

    private void Choose(RewardChoice choice)
    {
        IsOpen = false;
        if (panelRoot != null) panelRoot.SetActive(false);

        Time.timeScale = 1f; // Jalankan kembali waktu game normal
        
        // Kunci Kembali Kursor: Sembunyikan mouse kembali ke tengah untuk crosshair FPS
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        onChosen?.Invoke(choice);
        onChosen = null;
    }

    // CATATAN: Fungsi Update() yang berisi variabel tembakan/focus senjata yang bentrok 
    // telah dihapus sepenuhnya dari script ini karena fungsi tersebut adalah milik Weapon/Player Controller!
}
