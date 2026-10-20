using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Safety net for a CharacterController that ends up embedded in level geometry (giant mesh triangles,
/// a gate's mesh collider, a very fast dive...).
///  - Remembers recent safe positions while you are grounded and free.
///  - Auto: if the capsule stays embedded in static geometry for 'Stuck Seconds', it moves you back to a safe spot.
///  - Manual: press the Unstuck Key any time to do the same.
/// Add to the Capsule (next to Movement). Does nothing while paused, diving or grappling.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerUnstuck : MonoBehaviour
{
    [Header("Manual")]
    [SerializeField] private Key unstuckKey = Key.F9;

    [Header("Auto")]
    [SerializeField] private bool autoUnstick = true;
    [Tooltip("How long the capsule must stay embedded before it is moved.")]
    [SerializeField] private float stuckSeconds = 1f;
    [Tooltip("The capsule is shrunk by this factor for the embed test, so normal wall contact doesn't count.")]
    [SerializeField, Range(0.5f, 1f)] private float embedRadiusFactor = 0.85f;
    [Tooltip("Layers that count as solid level geometry.")]
    [SerializeField] private LayerMask solidMask = ~0;

    [Header("Safe position memory")]
    [SerializeField] private float saveInterval = 0.5f;
    [Tooltip("Rescue goes back to a spot saved at least this long ago (so it isn't already next to the trap).")]
    [SerializeField] private float rescueDelay = 1.5f;
    [SerializeField] private float maxLiftSearch = 40f;

    private struct Sample { public float time; public Vector3 pos; }

    private CharacterController controller;
    private Movement movement;
    private GrappleController grapple;

    private readonly List<Sample> samples = new List<Sample>();
    private readonly Collider[] overlapBuffer = new Collider[16];
    private float nextSave;
    private float embeddedTime;
    private Vector3 spawnPosition;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        movement = GetComponent<Movement>();
        grapple = GetComponent<GrappleController>();
        spawnPosition = transform.position;
    }

    private void Update()
    {
        if (PauseMenuController.InputBlocked || Time.timeScale == 0f) return;

        if (Keyboard.current != null && Keyboard.current[unstuckKey].wasPressedThisFrame)
        {
            Rescue("manual");
            return;
        }

        // Don't judge the player while a dive/dash/grapple is moving them on purpose.
        bool busy = (movement != null && !movement.enabled) || (grapple != null && grapple.IsGrappling);
        bool embedded = IsEmbedded();

        if (embedded)
        {
            if (!busy) embeddedTime += Time.deltaTime;
            if (autoUnstick && embeddedTime >= stuckSeconds) Rescue("auto");
            return;
        }

        embeddedTime = 0f;

        if (!busy && controller.isGrounded && Time.time >= nextSave)
        {
            nextSave = Time.time + saveInterval;
            samples.Add(new Sample { time = Time.time, pos = transform.position });
            if (samples.Count > 12) samples.RemoveAt(0);
        }
    }

    // True when the (slightly shrunk) capsule overlaps static solid geometry.
    private bool IsEmbedded() => IsEmbeddedAt(transform.position);

    private bool IsEmbeddedAt(Vector3 position)
    {
        GetCapsule(position, out Vector3 a, out Vector3 b, out float r);
        int n = Physics.OverlapCapsuleNonAlloc(a, b, r * embedRadiusFactor, overlapBuffer, solidMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < n; i++)
        {
            Collider c = overlapBuffer[i];
            if (c == controller) continue;
            if (c.attachedRigidbody != null) continue;   // enemies, props, bullets: not level geometry
            return true;
        }
        return false;
    }

    private void GetCapsule(Vector3 position, out Vector3 a, out Vector3 b, out float radius)
    {
        radius = controller.radius;
        Vector3 center = position + transform.TransformVector(controller.center);
        float half = Mathf.Max(controller.height * 0.5f - radius, 0f);
        a = center + Vector3.up * half;
        b = center - Vector3.up * half;
    }

    private void Rescue(string reason)
    {
        Vector3 target = ChooseRescuePosition();

        // If even the saved spot is blocked (or there is none), lift straight up until the capsule is free.
        if (IsEmbeddedAt(target))
        {
            bool freed = false;
            for (float lift = 0.5f; lift <= maxLiftSearch; lift += 0.5f)
            {
                if (!IsEmbeddedAt(target + Vector3.up * lift))
                {
                    target += Vector3.up * lift;
                    freed = true;
                    break;
                }
            }
            if (!freed) { Debug.LogWarning("PlayerUnstuck: no free spot found above the player."); return; }
        }

        controller.enabled = false;
        transform.position = target;
        controller.enabled = true;
        Physics.SyncTransforms();

        if (movement != null) movement.CancelMomentum();
        if (grapple != null) grapple.CancelLaunchMomentum();

        embeddedTime = 0f;
        Debug.Log("PlayerUnstuck (" + reason + "): moved to " + target);
    }

    private Vector3 ChooseRescuePosition()
    {
        for (int i = samples.Count - 1; i >= 0; i--)
            if (Time.time - samples[i].time >= rescueDelay)
                return samples[i].pos + Vector3.up * 0.1f;

        if (samples.Count > 0) return samples[0].pos + Vector3.up * 0.1f;
        return spawnPosition;
    }
}
