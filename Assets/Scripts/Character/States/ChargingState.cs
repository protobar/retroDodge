using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== CHARGING STATE ====================
public class ChargingState : ICharacterState
{
    private float chargeStartTime;
    private ParticleSystem chargeEffect;

    public void EnterState(CharacterBase character)
    {
        chargeStartTime = Time.time;

        // Start charging effects
        GameObject chargeEffectObj = character.transform.Find("ChargeEffect")?.gameObject;
        if (chargeEffectObj != null)
        {
            chargeEffect = chargeEffectObj.GetComponent<ParticleSystem>();
            chargeEffect?.Play();
        }

        AudioManager.Instance?.PlaySound("ChargeStart");
    }

    public void UpdateState(CharacterBase character)
    {
        float chargeTime = Time.time - chargeStartTime;

        // Update charging visual effects based on charge time
        if (chargeEffect != null)
        {
            var main = chargeEffect.main;
            main.startColor = Color.Lerp(Color.white, Color.red, chargeTime / 1.5f);
        }

        // Play charge sound at intervals
        if (chargeTime >= 1.5f && chargeTime % 0.5f < Time.deltaTime)
        {
            AudioManager.Instance?.PlaySound("ChargeReady");
        }
    }

    public void ExitState(CharacterBase character)
    {
        // Stop charging effects
        chargeEffect?.Stop();
        AudioManager.Instance?.PlaySound("ChargeEnd");
    }
}