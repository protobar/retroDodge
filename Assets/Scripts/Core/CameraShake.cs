using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== CAMERA SHAKE ====================
public class CameraShake : MonoBehaviour
{
    private static CameraShake instance;
    public static CameraShake Instance => instance;

    [Header("Shake Settings")]
    public float traumaDecay = 1f;
    public float maxShake = 1f;
    public AnimationCurve shakeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private float trauma = 0f;
    private Vector3 originalPosition;
    private Camera cam;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        cam = GetComponent<Camera>();
        originalPosition = transform.localPosition;
    }

    void Update()
    {
        if (trauma > 0)
        {
            ApplyShake();
            trauma = Mathf.Max(0, trauma - traumaDecay * Time.deltaTime);
        }
        else
        {
            transform.localPosition = originalPosition;
        }
    }

    void ApplyShake()
    {
        float shake = trauma * trauma * maxShake;
        float curveValue = shakeCurve.Evaluate(trauma);

        Vector3 shakeOffset = new Vector3(
            Random.Range(-shake, shake) * curveValue,
            Random.Range(-shake, shake) * curveValue,
            0
        );

        transform.localPosition = originalPosition + shakeOffset;
    }

    public void Shake(float intensity, float duration)
    {
        trauma = Mathf.Clamp01(trauma + intensity);

        if (duration > 0)
        {
            StartCoroutine(ShakeForDuration(intensity, duration));
        }
    }

    IEnumerator ShakeForDuration(float intensity, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float currentIntensity = Mathf.Lerp(intensity, 0, elapsed / duration);
            trauma = Mathf.Max(trauma, currentIntensity);
            yield return null;
        }
    }
}