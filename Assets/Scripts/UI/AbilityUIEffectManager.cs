using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Manages trick and treat UI effects shown on screen
/// Trick effects show on opponent's screen
/// Treat effects show on own screen
/// Works in both online (PUN2) and offline modes
/// </summary>
public class AbilityUIEffectManager : MonoBehaviour
{
    [Header("UI Effect Images")]
    [SerializeField] private Image trickEffectImage;
    [SerializeField] private Image treatEffectImage;

    [Header("Display Settings")]
    [SerializeField] private float effectDuration = 3f;
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float fadeOutDuration = 0.5f;

    [Header("Position Settings")]
    [SerializeField] private bool centerScreen = true;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    private Coroutine trickCoroutine;
    private Coroutine treatCoroutine;

    void Awake()
    {
        // Hide effects at start
        if (trickEffectImage != null)
        {
            trickEffectImage.gameObject.SetActive(false);
            trickEffectImage.color = new Color(1, 1, 1, 0);
        }

        if (treatEffectImage != null)
        {
            treatEffectImage.gameObject.SetActive(false);
            treatEffectImage.color = new Color(1, 1, 1, 0);
        }
    }

    /// <summary>
    /// Show trick effect (called when opponent uses trick on you)
    /// </summary>
    public void ShowTrickEffect(Sprite effectSprite, float duration = -1f)
    {
        if (trickEffectImage == null)
        {
            Debug.LogWarning("[ABILITY UI] Trick effect image is not assigned!");
            return;
        }

        if (effectSprite == null)
        {
            Debug.LogWarning("[ABILITY UI] Trick effect sprite is null!");
            return;
        }

        if (debugMode)
        {
            Debug.Log($"[ABILITY UI] Showing trick effect: {effectSprite.name}");
        }

        // Stop existing coroutine if any
        if (trickCoroutine != null)
        {
            StopCoroutine(trickCoroutine);
        }

        // Show the effect
        float displayDuration = duration > 0 ? duration : effectDuration;
        trickCoroutine = StartCoroutine(ShowEffectCoroutine(trickEffectImage, effectSprite, displayDuration));
    }

    /// <summary>
    /// Show treat effect (called when you use treat on yourself)
    /// </summary>
    public void ShowTreatEffect(Sprite effectSprite, float duration = -1f)
    {
        if (treatEffectImage == null)
        {
            Debug.LogWarning("[ABILITY UI] Treat effect image is not assigned!");
            return;
        }

        if (effectSprite == null)
        {
            Debug.LogWarning("[ABILITY UI] Treat effect sprite is null!");
            return;
        }

        if (debugMode)
        {
            Debug.Log($"[ABILITY UI] Showing treat effect: {effectSprite.name}");
        }

        // Stop existing coroutine if any
        if (treatCoroutine != null)
        {
            StopCoroutine(treatCoroutine);
        }

        // Show the effect
        float displayDuration = duration > 0 ? duration : effectDuration;
        treatCoroutine = StartCoroutine(ShowEffectCoroutine(treatEffectImage, effectSprite, displayDuration));
    }

    /// <summary>
    /// Coroutine to show effect with fade in/out
    /// </summary>
    private IEnumerator ShowEffectCoroutine(Image effectImage, Sprite effectSprite, float duration)
    {
        // Set sprite
        effectImage.sprite = effectSprite;
        effectImage.gameObject.SetActive(true);

        // Fade in
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            effectImage.color = new Color(1, 1, 1, alpha);
            yield return null;
        }

        effectImage.color = new Color(1, 1, 1, 1);

        // Hold for duration
        yield return new WaitForSeconds(duration);

        // Fade out
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / fadeOutDuration);
            effectImage.color = new Color(1, 1, 1, alpha);
            yield return null;
        }

        effectImage.color = new Color(1, 1, 1, 0);
        effectImage.gameObject.SetActive(false);
    }

    /// <summary>
    /// Manually hide trick effect
    /// </summary>
    public void HideTrickEffect()
    {
        if (trickCoroutine != null)
        {
            StopCoroutine(trickCoroutine);
            trickCoroutine = null;
        }

        if (trickEffectImage != null)
        {
            trickEffectImage.gameObject.SetActive(false);
            trickEffectImage.color = new Color(1, 1, 1, 0);
        }
    }

    /// <summary>
    /// Manually hide treat effect
    /// </summary>
    public void HideTreatEffect()
    {
        if (treatCoroutine != null)
        {
            StopCoroutine(treatCoroutine);
            treatCoroutine = null;
        }

        if (treatEffectImage != null)
        {
            treatEffectImage.gameObject.SetActive(false);
            treatEffectImage.color = new Color(1, 1, 1, 0);
        }
    }

    /// <summary>
    /// Hide all effects
    /// </summary>
    public void HideAllEffects()
    {
        HideTrickEffect();
        HideTreatEffect();
    }
}


