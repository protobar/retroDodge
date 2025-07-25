using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== ULTIMATE MANAGER ====================
public class UltimateManager : MonoBehaviourPun
{
    [Header("Ultimate Settings")]
    public float maxCharge = 100f;
    public float chargeDecayRate = 0f; // No decay by default

    private float currentCharge = 0f;
    private CharacterBase character;
    private bool ultimateReady = false;

    // Events
    public System.Action<float> OnChargeChanged;
    public System.Action OnUltimateReady;
    public System.Action OnUltimateUsed;

    void Awake()
    {
        character = GetComponent<CharacterBase>();
    }

    void Update()
    {
        // Charge decay (if enabled)
        if (chargeDecayRate > 0f && currentCharge > 0f)
        {
            currentCharge -= chargeDecayRate * Time.deltaTime;
            currentCharge = Mathf.Max(0f, currentCharge);
            UpdateUI();
        }
    }

    public void AddCharge(float amount)
    {
        float oldCharge = currentCharge;
        currentCharge += amount * maxCharge; // Convert percentage to actual charge
        currentCharge = Mathf.Clamp(currentCharge, 0f, maxCharge);

        // Check if ultimate became ready
        if (oldCharge < maxCharge && currentCharge >= maxCharge)
        {
            ultimateReady = true;
            OnUltimateReady?.Invoke();

            // Visual feedback
            ShowUltimateReadyEffect();
            AudioManager.Instance?.PlaySound("UltimateReady");
        }

        UpdateUI();

        // Network sync
        if (photonView.IsMine)
        {
            photonView.RPC("SyncUltimateCharge", RpcTarget.Others, currentCharge);
        }
    }

    public bool CanUseUltimate()
    {
        return ultimateReady && currentCharge >= maxCharge;
    }

    public void UseUltimate()
    {
        if (!CanUseUltimate()) return;

        currentCharge = 0f;
        ultimateReady = false;

        OnUltimateUsed?.Invoke();
        UpdateUI();

        // Network sync
        if (photonView.IsMine)
        {
            photonView.RPC("SyncUltimateCharge", RpcTarget.Others, currentCharge);
        }
    }

    public void ResetCharge()
    {
        currentCharge = 0f;
        ultimateReady = false;
        UpdateUI();
    }

    public float GetChargePercentage()
    {
        return currentCharge / maxCharge;
    }

    void UpdateUI()
    {
        OnChargeChanged?.Invoke(GetChargePercentage());

        // Update HUD
        if (HUDController.Instance != null)
        {
            HUDController.Instance.UpdateUltimateMeter(
                photonView.Owner.ActorNumber,
                GetChargePercentage()
            );
        }
    }

    void ShowUltimateReadyEffect()
    {
        // Screen flash effect
        ScreenEffects.Instance?.FlashScreen(Color.blue, 0.2f);

        // Character glow effect
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            StartCoroutine(UltimateReadyGlow(renderer));
        }
    }

    System.Collections.IEnumerator UltimateReadyGlow(Renderer renderer)
    {
        Material material = renderer.material;
        Color originalColor = material.color;

        for (int i = 0; i < 3; i++)
        {
            material.color = Color.cyan;
            yield return new WaitForSeconds(0.1f);
            material.color = originalColor;
            yield return new WaitForSeconds(0.1f);
        }
    }

    [PunRPC]
    void SyncUltimateCharge(float charge)
    {
        currentCharge = charge;
        ultimateReady = charge >= maxCharge;
        UpdateUI();
    }
}