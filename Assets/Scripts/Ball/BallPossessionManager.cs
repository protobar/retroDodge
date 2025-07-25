using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;

// ==================== BALL POSSESSION MANAGER ====================
public class BallPossessionManager : MonoBehaviour
{
    [Header("Possession Settings")]
    public float maxHoldTime = 5f;
    public float warningStartTime = 3f;
    public float damagePerSecond = 2f;

    private float currentHoldTime = 0f;
    private bool isHoldingBall = false;
    private CharacterBase ballHolder;

    // UI References
    public GameObject holdTimerUI;
    public UnityEngine.UI.Image timerFillImage;
    public Color normalColor = Color.green;
    public Color warningColor = Color.yellow;
    public Color dangerColor = Color.red;

    // Effects
    public ParticleSystem warningParticles;
    public ParticleSystem dangerParticles;

    void Update()
    {
        if (isHoldingBall && ballHolder != null)
        {
            currentHoldTime += Time.deltaTime;
            UpdateHoldTimerUI();

            // Warning phase
            if (currentHoldTime >= warningStartTime && currentHoldTime < maxHoldTime)
            {
                ApplyWarningEffects();
            }

            // Damage phase
            if (currentHoldTime >= maxHoldTime)
            {
                ApplyHoldPenalty();
            }
        }
    }

    void ApplyWarningEffects()
    {
        if (warningParticles != null && !warningParticles.isPlaying)
        {
            warningParticles.Play();
            AudioManager.Instance?.PlaySound("BallWarning");
        }

        if (timerFillImage != null)
        {
            timerFillImage.color = Color.Lerp(warningColor, dangerColor,
                (currentHoldTime - warningStartTime) / (maxHoldTime - warningStartTime));
        }
    }

    void ApplyHoldPenalty()
    {
        float damage = damagePerSecond * Time.deltaTime;
        ballHolder.TakeDamage(damage, DamageType.HoldPenalty);

        if (dangerParticles != null && !dangerParticles.isPlaying)
        {
            dangerParticles.Play();
            if (warningParticles != null) warningParticles.Stop();
        }

        // Visual feedback
        BallEffects.Instance?.PlayCorruptionEffect();
        CameraShake.Instance?.Shake(0.1f, 0.2f);
    }

    public void StartPossessionTimer(CharacterBase holder)
    {
        ballHolder = holder;
        isHoldingBall = true;
        currentHoldTime = 0f;

        if (holdTimerUI != null) holdTimerUI.SetActive(true);
    }

    public void StopPossessionTimer()
    {
        isHoldingBall = false;
        currentHoldTime = 0f;

        if (holdTimerUI != null) holdTimerUI.SetActive(false);
        if (warningParticles != null) warningParticles.Stop();
        if (dangerParticles != null) dangerParticles.Stop();
    }

    void UpdateHoldTimerUI()
    {
        if (timerFillImage != null)
        {
            float fillAmount = currentHoldTime / maxHoldTime;
            timerFillImage.fillAmount = fillAmount;

            if (currentHoldTime < warningStartTime)
            {
                timerFillImage.color = normalColor;
            }
        }

        // Show countdown in final 2 seconds
        if (currentHoldTime >= 3f && holdTimerUI != null)
        {
            float timeLeft = maxHoldTime - currentHoldTime;
            if (timeLeft <= 2f)
            {
                UnityEngine.UI.Text countdownText = holdTimerUI.GetComponentInChildren<UnityEngine.UI.Text>();
                if (countdownText != null)
                {
                    countdownText.text = timeLeft.ToString("F1");
                }
            }
        }
    }
}