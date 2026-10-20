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
    [SerializeField] private float minSpeed = 12f;      // no wind below this (walking is 6)
    [SerializeField] private float maxSpeed = 100f;     // full intensity at this speed (super dash max is 110)

    [Header("Intensity")]
    [SerializeField] private float maxEmissionRate = 120f;
    [SerializeField] private float spawnDistance = 25f; // how far ahead the particles appear
    [SerializeField] private float particleSpeedMultiplier = 1.2f; // particle speed relative to player speed
    [SerializeField] private float minParticleSpeed = 20f;
    [SerializeField] private float passBehind = 10f;    // how far past the camera they travel before dying

    private ParticleSystem ps;

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
        float intensity = Mathf.InverseLerp(minSpeed, maxSpeed, speed);

        var emission = ps.emission;
        emission.rateOverTime = maxEmissionRate * intensity;

        if (intensity <= 0f) return;

        Vector3 dir = momentum.MomentumVelocity.normalized;

        // Sit ahead of the player and shoot particles back toward them.
        transform.position = playerCamera.position + dir * spawnDistance;
        transform.rotation = Quaternion.LookRotation(-dir);

        float particleSpeed = Mathf.Max(minParticleSpeed, speed * particleSpeedMultiplier);
        var main = ps.main;
        main.startSpeed = particleSpeed;
        main.startLifetime = (spawnDistance + passBehind) / particleSpeed; // lives just long enough to pass you
    }
}
