using UnityEngine;
using UnityEngine.InputSystem;
using System.Text;
using System.Collections.Generic;

public class SlotMachinePointerTest : MonoBehaviour
{
    [SerializeField] private RectTransform pointer;
    [SerializeField] private RectTransform[] slots; // Slot1..S9 in order
    [SerializeField] private PowerUpQueueTester queueTester;

    private const int WindowSize = 9;
    private const int CenterIndex = WindowSize / 2; // 4 = box 5

    private int selectedIndex = CenterIndex;
    private int previousCount = 0;

    private void Start()
    {
        if (queueTester != null) queueTester.OnQueueChanged += HandleQueueChanged;
    }

    private void OnDestroy()
    {
        if (queueTester != null) queueTester.OnQueueChanged -= HandleQueueChanged;
    }

    private void HandleQueueChanged()
    {
        int newCount = queueTester.Queue.Count;
        bool crossingInto9Plus = previousCount <= WindowSize && newCount > WindowSize;

        if (crossingInto9Plus)
        {
            // Force the default S1[ID1]..S9[ID9] layout the instant we cross into the >9 scenario,
            // regardless of whatever was selected while in the <=9 regime.
            selectedIndex = CenterIndex;
        }
        else if (newCount == 0)
        {
            selectedIndex = 0;
        }
        else if (selectedIndex >= newCount)
        {
            selectedIndex = newCount - 1;
        }

        previousCount = newCount;
        Refresh();
    }

    private void Update()
    {
        if (Keyboard.current == null || queueTester == null) return;
        int count = queueTester.Queue.Count;
        if (count == 0) return;

        float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
        // Scroll up (positive) = backward (-1). Scroll down (negative) = forward (+1). Do not invert.
        if (scroll > 0f) Scroll(-1);
        else if (scroll < 0f) Scroll(1);

        for (int i = 0; i < WindowSize; i++)
        {
            if (Keyboard.current[Key.Digit1 + i].wasPressedThisFrame)
            {
                SelectFromDisplayedSlot(i);
                break;
            }
        }
    }

    private void Scroll(int direction)
    {
        int count = queueTester.Queue.Count;
        selectedIndex = ((selectedIndex + direction) % count + count) % count;
        Refresh();
    }

    private void SelectFromDisplayedSlot(int i)
    {
        int count = queueTester.Queue.Count;
        if (i >= count) return;

        int[] window = BuildWindow(selectedIndex, count);
        selectedIndex = window[i];
        Refresh();
    }

    private int[] BuildWindow(int center, int count)
    {
        if (count <= WindowSize)
        {
            int[] identity = new int[count];
            for (int i = 0; i < count; i++) identity[i] = i;
            return identity;
        }

        int[] window = new int[WindowSize];
        int half = WindowSize / 2;
        for (int i = 0; i < WindowSize; i++)
            window[i] = ((center - half + i) % count + count) % count;
        return window;
    }

    private void Refresh()
    {
        int count = queueTester.Queue.Count;
        if (count == 0) return;

        bool sliding = count > WindowSize;

        if (pointer != null)
        {
            Vector2 pos = pointer.anchoredPosition;
            pos.y = sliding ? slots[CenterIndex].anchoredPosition.y : slots[selectedIndex].anchoredPosition.y;
            pointer.anchoredPosition = pos;
        }

        int[] window = BuildWindow(selectedIndex, count);

        var sb = new StringBuilder();
        for (int i = 0; i < window.Length; i++)
            sb.Append($"S{i + 1}[ID{queueTester.Queue[window[i]].id}] ");

        var displayed = new HashSet<int>(window);
        sb.Append("| Queued (hidden): ");
        for (int i = 0; i < count; i++)
            if (!displayed.Contains(i)) sb.Append($"ID{queueTester.Queue[i].id} ");

        Debug.Log(sb.ToString());
    }
}