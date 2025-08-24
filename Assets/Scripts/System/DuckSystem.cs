using UnityEngine;
using System.Collections;

/// <summary>
/// Enhanced Duck System with duration limits and cooldowns
/// Prevents infinite ducking while maintaining responsive controls
/// </summary>
public class DuckSystem : MonoBehaviour
{
    [Header("Duck Duration Settings")]
    [SerializeField] private float maxDuckDuration = 1.5f;
    [SerializeField] private float duckCooldown = 1.2f;
    [SerializeField] private bool enableDurationLimit = true;

    [Header("Visual Feedback")]
    [SerializeField] private bool showDuckWarning = true;
    [SerializeField] private float warningStartTime = 1.0f; // Start warning at 1s
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color exhaustedColor = Color.red;

    [Header("Audio")]
    [SerializeField] private AudioClip duckStartSound;
    [SerializeField] private AudioClip duckEndSound;
    [SerializeField] private AudioClip duckExhaustedSound;
    [SerializeField] private AudioClip cooldownEndSound;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    // Duck state tracking
    private bool isDucking = false;
    private bool canDuck = true;
    private float currentDuckTime = 0f;
    private float cooldownTimer = 0f;
    private bool isInCooldown = false;
    private bool duckInputHeld = false;

    // Visual feedback
    private Renderer playerRenderer;
    private Color originalColor;
    private bool isFlashing = false;

    // Components
    private PlayerInputHandler inputHandler;
    private PlayerCharacter playerCharacter;
    private CharacterController legacyCharacter;
    private AudioSource audioSource;

    // Collider management
    private CapsuleCollider characterCollider;
    private float originalColliderHeight;
    private Vector3 originalColliderCenter;

    // Events
    public System.Action OnDuckStart;
    public System.Action OnDuckEnd;
    public System.Action OnDuckExhausted;
    public System.Action<float> OnDuckTimeChanged; // currentTime / maxTime
    public System.Action OnCooldownStart;
    public System.Action OnCooldownEnd;

    void Awake()
    {
        // Get components
        inputHandler = GetComponent<PlayerInputHandler>();
        playerCharacter = GetComponent<PlayerCharacter>();
        legacyCharacter = GetComponent<CharacterController>();
        characterCollider = GetComponent<CapsuleCollider>();
        audioSource = GetComponent<AudioSource>();
        playerRenderer = GetComponentInChildren<Renderer>();

        // Setup audio source
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = 0.7f;
        }

        // Store original values
        if (characterCollider != null)
        {
            originalColliderHeight = characterCollider.height;
            originalColliderCenter = characterCollider.center;
        }

        if (playerRenderer != null)
        {
            originalColor = playerRenderer.material.color;
        }
    }

    void Update()
    {
        HandleDuckInput();
        UpdateDuckTimer();
        UpdateCooldownTimer();
        UpdateVisualFeedback();

        // Debug controls
        if (debugMode && Input.GetKeyDown(KeyCode.F))
        {
            ForceEndDuck();
        }
    }

    void HandleDuckInput()
    {
        if (inputHandler == null) return;

        bool duckInputPressed = inputHandler.GetDuckHeld();

        // Detect input state changes
        bool inputStarted = duckInputPressed && !duckInputHeld;
        bool inputEnded = !duckInputPressed && duckInputHeld;

        duckInputHeld = duckInputPressed;

        // Handle duck start
        if (inputStarted)
        {
            TryStartDuck();
        }

        // Handle duck end
        if (inputEnded || (!duckInputPressed && isDucking))
        {
            EndDuck(false); // Manual end
        }
    }

    void TryStartDuck()
    {
        // Check if we can duck
        if (!canDuck || isInCooldown || isDucking)
        {
            if (debugMode)
            {
                string reason = !canDuck ? "not allowed" :
                               isInCooldown ? $"in cooldown ({cooldownTimer:F1}s remaining)" :
                               "already ducking";
                Debug.Log($"Duck attempt blocked: {reason}");
            }

            // Play feedback sound for blocked attempts
            if (isInCooldown)
            {
                PlaySound(duckExhaustedSound);
            }

            return;
        }

        // Check if character is grounded (can only duck when grounded)
        bool isGrounded = false;
        if (playerCharacter != null)
        {
            isGrounded = playerCharacter.IsGrounded();
        }
        /*else if (legacyCharacter != null)
        {
            isGrounded = legacyCharacter.IsGrounded();
        }*/

        if (!isGrounded)
        {
            if (debugMode)
            {
                Debug.Log("Duck attempt blocked: not grounded");
            }
            return;
        }

        // Start ducking
        StartDuck();
    }

    void StartDuck()
    {
        isDucking = true;
        currentDuckTime = 0f;

        // Apply duck physics
        ApplyDuckCollider();

        // Update character state
        if (playerCharacter != null)
        {
            // PlayerCharacter will handle its own ducking state
        }
        else if (legacyCharacter != null)
        {
            // Update legacy character state if needed
        }

        // Audio feedback
        PlaySound(duckStartSound);

        // Trigger events
        OnDuckStart?.Invoke();

        if (debugMode)
        {
            Debug.Log($"Duck started - Max duration: {maxDuckDuration}s");
        }
    }

    void EndDuck(bool wasForced)
    {
        if (!isDucking) return;

        isDucking = false;
        currentDuckTime = 0f;

        // Restore collider
        RestoreCollider();

        // Start cooldown
        StartCooldown();

        // Audio feedback
        PlaySound(wasForced ? duckExhaustedSound : duckEndSound);

        // Trigger events
        OnDuckEnd?.Invoke();
        if (wasForced)
        {
            OnDuckExhausted?.Invoke();
        }

        if (debugMode)
        {
            Debug.Log($"Duck ended - {(wasForced ? "FORCED" : "Manual")} - Starting cooldown: {duckCooldown}s");
        }
    }

    void ForceEndDuck()
    {
        if (isDucking)
        {
            EndDuck(true);
        }
    }

    void UpdateDuckTimer()
    {
        if (!isDucking || !enableDurationLimit) return;

        currentDuckTime += Time.deltaTime;

        // Check if max duration reached
        if (currentDuckTime >= maxDuckDuration)
        {
            ForceEndDuck();
            return;
        }

        // Update timer event
        OnDuckTimeChanged?.Invoke(currentDuckTime / maxDuckDuration);
    }

    void StartCooldown()
    {
        isInCooldown = true;
        cooldownTimer = duckCooldown;
        canDuck = false;

        OnCooldownStart?.Invoke();
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

        // Audio feedback
        PlaySound(cooldownEndSound);

        OnCooldownEnd?.Invoke();

        if (debugMode)
        {
            Debug.Log("Duck cooldown ended - Can duck again");
        }
    }

    void ApplyDuckCollider()
    {
        if (characterCollider == null) return;

        // Reduce collider height
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

        // Restore original dimensions
        characterCollider.height = originalColliderHeight;
        characterCollider.center = originalColliderCenter;
    }

    void UpdateVisualFeedback()
    {
        if (!showDuckWarning || playerRenderer == null) return;

        if (isDucking && enableDurationLimit)
        {
            float duckProgress = currentDuckTime / maxDuckDuration;

            if (duckProgress >= (warningStartTime / maxDuckDuration))
            {
                // Start warning flash
                if (!isFlashing)
                {
                    StartCoroutine(DuckWarningFlash());
                }
            }
        }
        else if (isInCooldown)
        {
            // Show cooldown color
            if (!isFlashing)
            {
                StartCoroutine(CooldownFlash());
            }
        }
        else
        {
            // Normal color
            if (playerRenderer.material.color != originalColor)
            {
                playerRenderer.material.color = originalColor;
            }
        }
    }

    IEnumerator DuckWarningFlash()
    {
        isFlashing = true;

        while (isDucking && currentDuckTime >= warningStartTime)
        {
            // Flash between warning and exhausted colors based on time remaining
            float timeRemaining = maxDuckDuration - currentDuckTime;
            float flashSpeed = Mathf.Lerp(0.5f, 0.1f, 1f - (timeRemaining / (maxDuckDuration - warningStartTime)));

            Color targetColor = timeRemaining < 0.3f ? exhaustedColor : warningColor;

            playerRenderer.material.color = targetColor;
            yield return new WaitForSeconds(flashSpeed);

            playerRenderer.material.color = originalColor;
            yield return new WaitForSeconds(flashSpeed);
        }

        playerRenderer.material.color = originalColor;
        isFlashing = false;
    }

    IEnumerator CooldownFlash()
    {
        isFlashing = true;

        Color cooldownColor = new Color(exhaustedColor.r, exhaustedColor.g, exhaustedColor.b, 0.5f);

        while (isInCooldown)
        {
            playerRenderer.material.color = cooldownColor;
            yield return new WaitForSeconds(0.3f);

            playerRenderer.material.color = originalColor;
            yield return new WaitForSeconds(0.3f);
        }

        playerRenderer.material.color = originalColor;
        isFlashing = false;
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // Public API
    public bool IsDucking() => isDucking;
    public bool CanDuck() => canDuck && !isInCooldown;
    public bool IsInCooldown() => isInCooldown;
    public float GetDuckTimeRemaining() => Mathf.Max(0f, maxDuckDuration - currentDuckTime);
    public float GetCooldownTimeRemaining() => Mathf.Max(0f, cooldownTimer);
    public float GetDuckProgress() => isDucking ? (currentDuckTime / maxDuckDuration) : 0f;
    public float GetCooldownProgress() => isInCooldown ? (1f - (cooldownTimer / duckCooldown)) : 1f;

    // Configuration
    public void SetMaxDuckDuration(float duration)
    {
        maxDuckDuration = duration;
    }

    public void SetDuckCooldown(float cooldown)
    {
        duckCooldown = cooldown;
    }

    public void SetDurationLimitEnabled(bool enabled)
    {
        enableDurationLimit = enabled;
        if (!enabled && isDucking)
        {
            // Reset timer if duration limit disabled
            currentDuckTime = 0f;
        }
    }

    // Emergency reset (for admin/debug)
    public void ResetDuckSystem()
    {
        if (isDucking)
        {
            EndDuck(true);
        }

        isInCooldown = false;
        cooldownTimer = 0f;
        canDuck = true;
        currentDuckTime = 0f;

        RestoreCollider();

        if (debugMode)
        {
            Debug.Log("Duck system reset");
        }
    }

    // Debug visualization
    void OnGUI()
    {
        if (!debugMode) return;

        float yOffset = gameObject.name.Contains("2") ? 300f : 200f;

        GUILayout.BeginArea(new Rect(10, yOffset, 300, 120));
        GUILayout.BeginVertical("box");

        GUILayout.Label($"{gameObject.name} Duck System");
        GUILayout.Label($"Ducking: {isDucking}");

        if (isDucking)
        {
            float timeRemaining = GetDuckTimeRemaining();
            GUILayout.Label($"Time Remaining: {timeRemaining:F1}s");

            // Progress bar
            Rect progressRect = GUILayoutUtility.GetRect(250, 20);
            GUI.Box(progressRect, "");

            Rect fillRect = new Rect(progressRect.x, progressRect.y,
                progressRect.width * (currentDuckTime / maxDuckDuration), progressRect.height);

            Color barColor = timeRemaining < 0.3f ? Color.red :
                           timeRemaining < 0.7f ? Color.yellow : Color.green;

            GUI.color = barColor;
            GUI.Box(fillRect, "");
            GUI.color = Color.white;
        }

        if (isInCooldown)
        {
            GUILayout.Label($"Cooldown: {cooldownTimer:F1}s");

            // Cooldown progress bar
            Rect cooldownRect = GUILayoutUtility.GetRect(250, 15);
            GUI.Box(cooldownRect, "");

            Rect cooldownFill = new Rect(cooldownRect.x, cooldownRect.y,
                cooldownRect.width * GetCooldownProgress(), cooldownRect.height);

            GUI.color = Color.cyan;
            GUI.Box(cooldownFill, "");
            GUI.color = Color.white;
        }

        GUILayout.Label($"Can Duck: {CanDuck()}");

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}