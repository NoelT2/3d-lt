using UnityEngine;

/// <summary>
/// Drives a Particle System as a wind / speed-line effect.
/// Particles spawn in front of the player (in the direction of momentum) and rush
/// back past the camera. Reads speed and direction from MomentumMarker.
/// Put this on the Particle System object.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class WindEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MomentumMarker momentum;   // the one on the Player
    [SerializeField] private Transform playerCamera;    // leave empty to use Camera.main

    [Header("When it shows")]
    [SerializeField] private float minSpeed = 40f;      // no wind below this (normal movement tops out near 50)
    [SerializeField] private float maxSpeed = 100f;     // full intensity at this speed (super dash max is 110)
    [SerializeField] private float intensityCurve = 0.6f; // below 1 = more wind at mid speeds

    [Header("Intensity")]
    [SerializeField] private int maxVisibleParticles = 25; // most particles on screen at once
    [SerializeField] private float spawnDistance = 25f; // how far ahead the particles appear
    [SerializeField] private float particleSpeedMultiplier = 1.2f; // particle speed relative to player speed
    [SerializeField] private float minParticleSpeed = 20f;
    [SerializeField] private float passBehind = 10f;    // how far past the camera they travel before dying

    [Header("Fade")]
    [SerializeField] private float fadeInRate = 12f;    // how fast the wind appears
    [SerializeField] private float fadeOutRate = 2.5f;  // how slowly it fades when you slow down

    private ParticleSystem ps;
    private float smoothIntensity;

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // old particles keep going when you turn
        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;
    }

    private void LateUpdate()
    {
        if (momentum == null || playerCamera == null) return;

        float speed = momentum.Speed;

        // Target intensity from speed, then smooth it (fast rise, slow fall).
        float target = Mathf.InverseLerp(minSpeed, maxSpeed, speed);
        target = Mathf.Pow(target, intensityCurve);

        float rate = target > smoothIntensity ? fadeInRate : fadeOutRate;
        smoothIntensity = Mathf.MoveTowards(smoothIntensity, target, rate * Time.deltaTime);

        var emission = ps.emission;

        if (smoothIntensity <= 0.001f || speed < 0.1f)
        {
            emission.rateOverTime = 0f;
            return;
        }

        float particleSpeed = Mathf.Max(minParticleSpeed, speed * particleSpeedMultiplier);
        float lifetime = (spawnDistance + passBehind) / particleSpeed; // lives just long enough to pass you

        emission.rateOverTime = (maxVisibleParticles * smoothIntensity) / lifetime;

        var main = ps.main;
        main.maxParticles = maxVisibleParticles + 5;   // hard safety cap
        main.startSpeed = particleSpeed;
        main.startLifetime = lifetime;

        // Sit ahead of the player and shoot particles back toward them.
        Vector3 dir = momentum.MomentumVelocity.normalized;
        transform.position = playerCamera.position + dir * spawnDistance;
        transform.rotation = Quaternion.LookRotation(-dir);
    }
}