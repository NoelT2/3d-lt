using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PowerUpQueueTester : MonoBehaviour
{
    public class PlaceholderEntry { public int id; }

    private readonly List<PlaceholderEntry> queue = new List<PlaceholderEntry>();
    private int nextId = 1;

    public IReadOnlyList<PlaceholderEntry> Queue => queue;

    public delegate void QueueChanged();
    public event QueueChanged OnQueueChanged;

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
            AddPlaceholder();
    }

    public void AddPlaceholder()
    {
        queue.Add(new PlaceholderEntry { id = nextId });
        Debug.Log($"Added placeholder ID{nextId}. Queue count = {queue.Count}");
        nextId++;
        OnQueueChanged?.Invoke();
    }
}