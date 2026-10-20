using System.Collections.Generic;
using UnityEngine;

public class RunPowerUpInventory
{
    public class Entry
    {
        public PowerUpData data;
        public PowerUp instance; // the live component doing the effect
        public int stackCount;
    }

    private readonly List<Entry> actives = new List<Entry>();
    private int selectedIndex;

    public IReadOnlyList<Entry> Actives => actives;
    public int SelectedIndex => selectedIndex;
    public Entry Selected => actives.Count > 0 ? actives[selectedIndex] : null;

    public delegate void InventoryChanged();
    public event InventoryChanged OnChanged;

    public void Add(PowerUpData data, PowerUp instance)
    {
        Entry existing = actives.Find(e => e.data == data);

        if (existing != null)
        {
            existing.stackCount++;
            existing.instance.OnStackAdded(existing.stackCount);
        }
        else
        {
            actives.Add(new Entry { data = data, instance = instance, stackCount = 1 });
            if (actives.Count == 1) selectedIndex = 0;
        }

        OnChanged?.Invoke();
    }

    // Consumes one stack of the currently selected active. Returns true if it fired.
    public bool UseSelected()
    {
        Entry entry = Selected;
        if (entry == null) return false;

        bool fired = entry.instance.Use();
        if (!fired) return false;

        entry.stackCount--;

        if (entry.stackCount <= 0)
        {
            entry.instance.OnRemoved();
            actives.Remove(entry);
            if (selectedIndex >= actives.Count) selectedIndex = Mathf0(actives.Count - 1);
        }

        OnChanged?.Invoke();
        return true;
    }

    // Fires whichever active matches the given auto-trigger type, if it's currently equipped.
    // Does NOT require it to be the selected one — movement-type actives fire on their own input.
    public bool UseByTrigger(PowerUpTrigger triggerType)
    {
        Entry entry = actives.Find(e => e.instance.Trigger == triggerType);
        if (entry == null) return false;

        bool fired = entry.instance.Use();
        if (!fired) return false;

        entry.stackCount--;
        if (entry.stackCount <= 0)
        {
            entry.instance.OnRemoved();
            actives.Remove(entry);
            if (selectedIndex >= actives.Count) selectedIndex = Mathf0(actives.Count - 1);
        }

        OnChanged?.Invoke();
        return true;
    }

    public void Scroll(int direction) // +1 or -1
    {
        if (actives.Count == 0) return;
        selectedIndex = ((selectedIndex + direction) % actives.Count + actives.Count) % actives.Count;
        OnChanged?.Invoke();
    }

    // Jumps to a specific active by its window position (1-9 quick-swap key).
    // windowIndex is 0-based relative to GetWindow's output.
    public void SelectFromWindow(int windowIndex, int windowSize)
    {
        List<Entry> window = GetWindow(windowSize);
        if (windowIndex < 0 || windowIndex >= window.Count) return;

        selectedIndex = actives.IndexOf(window[windowIndex]);
        OnChanged?.Invoke();
    }

    // Returns up to windowSize entries centered on selectedIndex, wrapping around.
    // This is what both the slot-machine column and the card back-stack should render from.
    public List<Entry> GetWindow(int windowSize)
    {
        List<Entry> result = new List<Entry>();
        if (actives.Count == 0) return result;

        int count = Mathf0(actives.Count < windowSize ? actives.Count : windowSize);
        int half = count / 2;

        for (int i = 0; i < count; i++)
        {
            int idx = ((selectedIndex - half + i) % actives.Count + actives.Count) % actives.Count;
            result.Add(actives[idx]);
        }

        return result;
    }

    private int Mathf0(int v) => v < 0 ? 0 : v;

    public void ClearAll()
    {
        foreach (Entry entry in actives)
            Object.Destroy(entry.instance.gameObject); // was: Destroy(...)

        actives.Clear();
        selectedIndex = 0;
        OnChanged?.Invoke();
    }
}