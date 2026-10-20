using UnityEngine;
using TMPro;

public class DamageNumberPopup : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private float lifetime = 0.9f;
    [SerializeField] private float floatSpeed = 1.5f;
    [SerializeField] private float fadeStartRatio = 0.5f;

    private float timer;
    private Color startColor;
    private Transform cameraTransform;

    public void Init(float damage)
    {
        if (text == null) text = GetComponentInChildren<TMP_Text>();
        text.text = Mathf.RoundToInt(damage).ToString();
        startColor = text.color;
        cameraTransform = Camera.main != null ? Camera.main.transform : null;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;

        if (cameraTransform != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - cameraTransform.position);
        }

        float lifeRatio = timer / lifetime;
        if (lifeRatio > fadeStartRatio)
        {
            Color c = startColor;
            c.a = Mathf.Lerp(startColor.a, 0f, Mathf.InverseLerp(fadeStartRatio, 1f, lifeRatio));
            text.color = c;
        }

        if (timer >= lifetime) Destroy(gameObject);
    }
}