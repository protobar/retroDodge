using UnityEngine;
using Photon.Pun;
using System.Collections;

// ==================== SCREEN EFFECTS ====================
public class ScreenEffects : MonoBehaviour
{
    private static ScreenEffects instance;
    public static ScreenEffects Instance => instance;

    [Header("Screen Effect Settings")]
    public UnityEngine.UI.Image flashOverlay;

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
    }

    public void FlashScreen(Color color, float duration)
    {
        if (flashOverlay != null)
        {
            StartCoroutine(FlashCoroutine(color, duration));
        }
    }

    System.Collections.IEnumerator FlashCoroutine(Color color, float duration)
    {
        flashOverlay.color = color;
        flashOverlay.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(color.a, 0f, elapsed / duration);
            flashOverlay.color = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }

        flashOverlay.gameObject.SetActive(false);
    }
}