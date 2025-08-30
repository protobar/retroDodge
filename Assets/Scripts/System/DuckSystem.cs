using UnityEngine;
using System.Collections;

/// <summary>
/// Optimized DuckSystem with streamlined duration limits and cooldowns
/// Removed verbose debug features and redundant visual effects
/// </summary>
public class DuckSystem : MonoBehaviour
{
    [Header("Duck Settings")]
    [SerializeField] private float maxDuckDuration = 1.5f;
    [SerializeField] private float duckCooldown = 1.2f;
    [SerializeField] private bool enableDurationLimit = true;

    [Header("Audio")]
    [SerializeField] private AudioClip duckStartSound;
    [SerializeField] private AudioClip duckEndSound;
    [SerializeField] private AudioClip duckExhaustedSound;

    // Core state
    private bool isDucking = false;
    private bool canDuck = true;
    private float currentDuckTime = 0f;
    private float cooldownTimer = 0f;
    private bool isInCooldown = false;
    private bool duckInputHeld = false;

    // Components - cached once
    private PlayerInputHandler inputHandler;
    private PlayerCharacter playerCharacter;
    private AudioSource audioSource;
    private CapsuleCollider characterCollider;

    // Collider cache
    private float originalColliderHeight;
    private Vector3 originalColliderCenter;

    // Events for UI integration
    public System.Action OnDuckStart;
    public System.Action OnDuckEnd;
    public System.Action<float> OnDuckTimeChanged;

    void Awake()
    {
        // Cache components
        inputHandler = GetComponent<PlayerInputHandler>();
        playerCharacter = GetComponent<PlayerCharacter>();
        characterCollider = GetComponent<CapsuleCollider>();
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.volume = 0.7f;

        // Store original collider dimensions
        if (characterCollider != null)
        {
            originalColliderHeight = characterCollider.height;
            originalColliderCenter = characterCollider.center;
        }
    }

    void Update()
    {
        HandleDuckInput();
        UpdateDuckTimer();
        UpdateCooldownTimer();
    }

    void HandleDuckInput()
    {
        if (inputHandler == null) return;

        bool duckInput = inputHandler.GetDuckHeld();
        bool inputStarted = duckInput && !duckInputHeld;
        bool inputEnded = !duckInput && duckInputHeld;

        duckInputHeld = duckInput;

        if (inputStarted) TryStartDuck();
        if (inputEnded || (!duckInput && isDucking)) EndDuck(false);
    }

    void TryStartDuck()
    {
        // Check if can duck
        if (!canDuck || isInCooldown || isDucking) return;

        // Must be grounded
        bool isGrounded = playerCharacter?.IsGrounded() ?? false;
        if (!isGrounded) return;

        StartDuck();
    }

    void StartDuck()
    {
        isDucking = true;
        currentDuckTime = 0f;
        ApplyDuckCollider();
        PlaySound(duckStartSound);
        OnDuckStart?.Invoke();
    }

    void EndDuck(bool wasForced)
    {
        if (!isDucking) return;

        isDucking = false;
        currentDuckTime = 0f;
        RestoreCollider();
        StartCooldown();
        PlaySound(wasForced ? duckExhaustedSound : duckEndSound);
        OnDuckEnd?.Invoke();
    }

    void UpdateDuckTimer()
    {
        if (!isDucking || !enableDurationLimit) return;

        currentDuckTime += Time.deltaTime;
        OnDuckTimeChanged?.Invoke(currentDuckTime / maxDuckDuration);

        if (currentDuckTime >= maxDuckDuration)
        {
            EndDuck(true); // Force end
        }
    }

    void StartCooldown()
    {
        isInCooldown = true;
        cooldownTimer = duckCooldown;
        canDuck = false;
    }

    void UpdateCooldownTimer()
    {
        if (!isInCooldown) return;

        cooldownTimer -= Time.deltaTime;
        if (cooldownTimer <= 0f)
        {
            EndCooldown();
        }
    }

    void EndCooldown()
    {
        isInCooldown = false;
        cooldownTimer = 0f;
        canDuck = true;
    }

    void ApplyDuckCollider()
    {
        if (characterCollider == null) return;

        characterCollider.height = originalColliderHeight * 0.5f;
        characterCollider.center = new Vector3(
            originalColliderCenter.x,
            originalColliderCenter.y - (originalColliderHeight * 0.25f),
            originalColliderCenter.z
        );
    }

    void RestoreCollider()
    {
        if (characterCollider == null) return;
        characterCollider.height = originalColliderHeight;
        characterCollider.center = originalColliderCenter;
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // Public API for PlayerCharacter integration
    public bool IsDucking() => isDucking;
    public bool CanDuck() => canDuck && !isInCooldown;
    public bool IsInCooldown() => isInCooldown;
    public float GetDuckTimeRemaining() => Mathf.Max(0f, maxDuckDuration - currentDuckTime);
    public float GetCooldownTimeRemaining() => Mathf.Max(0f, cooldownTimer);
    public float GetDuckProgress() => isDucking ? (currentDuckTime / maxDuckDuration) : 0f;

    // Configuration methods
    public void SetMaxDuckDuration(float duration) => maxDuckDuration = duration;
    public void SetDuckCooldown(float cooldown) => duckCooldown = cooldown;
    public void SetDurationLimitEnabled(bool enabled)
    {
        enableDurationLimit = enabled;
        if (!enabled && isDucking) currentDuckTime = 0f;
    }

    // Emergency reset
    public void ResetDuckSystem()
    {
        if (isDucking) EndDuck(true);
        isInCooldown = false;
        cooldownTimer = 0f;
        canDuck = true;
        currentDuckTime = 0f;
        RestoreCollider();
    }
}