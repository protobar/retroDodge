using UnityEngine;

public class ChargedThrowSystem : MonoBehaviour
{
    [Header("Charge Settings")]
    [SerializeField] private float maxChargeTime = 2f;
    [SerializeField] private float minPowerMultiplier = 1f;
    [SerializeField] private float maxPowerMultiplier = 2.5f;
    [SerializeField] private AnimationCurve chargeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Visual Feedback")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color chargedColor = Color.red;
    [SerializeField] private float pulseSpeed = 5f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip chargeSound;
    [SerializeField] private AudioClip releaseSound;

    // Charging state
    private bool isCharging = false;
    private float chargeStartTime = 0f;
    private float currentChargeTime = 0f;
    private float chargePower = 1f;

    // References
    private CharacterController character;
    private PlayerInputHandler inputHandler;
    private BallController heldBall;
    private Renderer ballRenderer;

    // Visual effects
    private Material ballMaterial;
    private Color originalBallColor;

    void Awake()
    {
        character = GetComponent<CharacterController>();
        inputHandler = GetComponent<PlayerInputHandler>();

        if (inputHandler == null)
        {
            Debug.LogError($"{gameObject.name} - PlayerInputHandler component is missing for ChargedThrowSystem!");
        }

        // Setup audio
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = 0.5f;
        }
    }

    void Update()
    {
        HandleChargedThrow();
        UpdateChargingVisuals();
    }

    void HandleChargedThrow()
    {
        if (!character.HasBall() || inputHandler == null)
        {
            StopCharging();
            return;
        }

        // Start charging when throw button is pressed
        if (inputHandler.GetThrowPressed() && !isCharging)
        {
            StartCharging();
        }

        // Continue charging while button is held
        if (inputHandler.GetThrowHeld() && isCharging)
        {
            UpdateCharging();
        }

        // Release charged throw when button is released
        if (!inputHandler.GetThrowHeld() && isCharging)
        {
            ExecuteChargedThrow();
        }
    }

    void StartCharging()
    {
        isCharging = true;
        chargeStartTime = Time.time;
        currentChargeTime = 0f;

        // Get the held ball for visual effects
        if (BallManager.Instance != null)
        {
            heldBall = BallManager.Instance.GetCurrentBall();
            if (heldBall != null)
            {
                ballRenderer = heldBall.GetComponent<Renderer>();
                if (ballRenderer != null && ballRenderer.material != null)
                {
                    ballMaterial = ballRenderer.material;
                    originalBallColor = ballMaterial.color;
                }
            }
        }

        // Play charge sound
        if (audioSource != null && chargeSound != null)
        {
            audioSource.clip = chargeSound;
            audioSource.loop = true;
            audioSource.Play();
        }

        Debug.Log("Started charging throw!");
    }

    void UpdateCharging()
    {
        currentChargeTime = Time.time - chargeStartTime;

        // Clamp to max charge time
        currentChargeTime = Mathf.Clamp(currentChargeTime, 0f, maxChargeTime);

        // Calculate charge power using curve
        float normalizedCharge = currentChargeTime / maxChargeTime;
        chargePower = Mathf.Lerp(minPowerMultiplier, maxPowerMultiplier, chargeCurve.Evaluate(normalizedCharge));

        // Update audio pitch based on charge
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.pitch = 1f + (normalizedCharge * 0.5f);
        }
    }

    void UpdateChargingVisuals()
    {
        if (!isCharging || ballMaterial == null) return;

        // Calculate charge level
        float normalizedCharge = currentChargeTime / maxChargeTime;

        // Color interpolation
        Color chargeColor = Color.Lerp(originalBallColor, chargedColor, normalizedCharge);

        // Add pulsing effect
        float pulse = Mathf.Sin(Time.time * pulseSpeed * (1f + normalizedCharge)) * 0.5f + 0.5f;
        Color finalColor = Color.Lerp(chargeColor, Color.white, pulse * normalizedCharge * 0.3f);

        ballMaterial.color = finalColor;

        // Scale effect for max charge
        if (heldBall != null && normalizedCharge >= 0.9f)
        {
            float scaleBonus = 1f + (pulse * 0.1f);
            heldBall.transform.localScale = Vector3.one * scaleBonus;
        }
    }

    void ExecuteChargedThrow()
    {
        if (!isCharging || BallManager.Instance == null)
        {
            StopCharging();
            return;
        }

        // FIXED: Use the new simplified method that lets BallController determine direction
        BallManager.Instance.RequestBallThrowSimple(character, chargePower);

        // Play release sound
        if (audioSource != null && releaseSound != null)
        {
            audioSource.Stop();
            audioSource.clip = releaseSound;
            audioSource.loop = false;
            audioSource.pitch = 1f + (currentChargeTime / maxChargeTime * 0.5f);
            audioSource.Play();
        }

        Debug.Log($"Executed charged throw! Power: {chargePower:F2}x");

        StopCharging();
    }

    void StopCharging()
    {
        if (!isCharging) return;

        isCharging = false;
        currentChargeTime = 0f;
        chargePower = 1f;

        // Stop audio
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.pitch = 1f;
        }

        // Reset ball visuals
        if (ballMaterial != null)
        {
            ballMaterial.color = originalBallColor;
        }

        if (heldBall != null)
        {
            heldBall.transform.localScale = Vector3.one;
        }

        // Clear references
        heldBall = null;
        ballRenderer = null;
        ballMaterial = null;
    }

    // Public getters
    public bool IsCharging() => isCharging;
    public float GetChargePower() => chargePower;
    public float GetChargeProgress() => currentChargeTime / maxChargeTime;

    // Called when ball is lost (picked up by opponent, etc.)
    public void OnBallLost()
    {
        StopCharging();
    }

    void OnDisable()
    {
        StopCharging();
    }
}