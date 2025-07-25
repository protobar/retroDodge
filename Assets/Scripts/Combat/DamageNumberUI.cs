using UnityEngine;
using Photon.Pun;
using System.Collections;

// ==================== DAMAGE NUMBER UI ====================
public class DamageNumberUI : MonoBehaviour
{
    private static DamageNumberUI instance;
    public static DamageNumberUI Instance => instance;

    [Header("Damage Number Settings")]
    public GameObject damageNumberPrefab;
    public Transform damageNumberParent;

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

    public void ShowDamageNumber(Vector3 worldPosition, float damage, Color color)
    {
        if (damageNumberPrefab == null) return;

        // Convert world position to screen position
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition);

        // Create damage number
        GameObject damageObj = Instantiate(damageNumberPrefab, damageNumberParent);
        damageObj.transform.position = screenPos;

        // Set damage text and color
        UnityEngine.UI.Text damageText = damageObj.GetComponent<UnityEngine.UI.Text>();
        if (damageText != null)
        {
            damageText.text = Mathf.RoundToInt(damage).ToString();
            damageText.color = color;
        }

        // Animate damage number
        StartCoroutine(AnimateDamageNumber(damageObj));
    }

    System.Collections.IEnumerator AnimateDamageNumber(GameObject damageObj)
    {
        Vector3 startPos = damageObj.transform.position;
        Vector3 endPos = startPos + Vector3.up * 100f;

        float duration = 1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Move upward
            damageObj.transform.position = Vector3.Lerp(startPos, endPos, t);

            // Fade out
            UnityEngine.UI.Text text = damageObj.GetComponent<UnityEngine.UI.Text>();
            if (text != null)
            {
                Color color = text.color;
                color.a = Mathf.Lerp(1f, 0f, t);
                text.color = color;
            }

            yield return null;
        }

        Destroy(damageObj);
    }
}