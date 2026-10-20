using UnityEngine;

public class CameraShake : MonoBehaviour
{
    [Header("Shake Curve")]
    [SerializeField] private float maxAngle = 6f;
    [SerializeField] private float maxOffset = 0.08f;
    [SerializeField] private float frequency = 22f;
    [SerializeField] private float traumaDecay = 1.5f;

    private float trauma;
    private Vector3 restLocalPosition;
    private float seedX, seedY, seedZ;

    private void Awake()
    {
        restLocalPosition = transform.localPosition;

        seedX = Random.Range(0f, 100f);
        seedY = Random.Range(0f, 100f);
        seedZ = Random.Range(0f, 100f);
    }

    /// Adds shake. 0-1 range, clamped. Call repeatedly for continuous rumble,
    /// or once with a big value for an impact punch.
    public void AddTrauma(float amount)
    {
        trauma = Mathf.Clamp01(trauma + amount);
    }

    private void LateUpdate()
    {
        if (trauma <= 0f)
        {
            transform.localPosition = restLocalPosition;
            trauma = 0f;
            return;
        }

        // Squared falloff: shake feels punchy near max, fades out gently.
        float shake = trauma * trauma;
        float t = Time.time * frequency;

        float offsetX = (Mathf.PerlinNoise(seedX, t) * 2f - 1f) * maxOffset * shake;
        float offsetY = (Mathf.PerlinNoise(seedY, t) * 2f - 1f) * maxOffset * shake;

        float angleZ = (Mathf.PerlinNoise(seedZ, t) * 2f - 1f) * maxAngle * shake;
        float angleX = (Mathf.PerlinNoise(seedX, t * 0.7f) * 2f - 1f) * maxAngle * shake * 0.5f;

        transform.localPosition = restLocalPosition + new Vector3(offsetX, offsetY, 0f);
        transform.localRotation *= Quaternion.Euler(angleX, 0f, angleZ);

        trauma = Mathf.Max(0f, trauma - traumaDecay * Time.deltaTime);
    }
}