using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Manages player health, damage, and death states
/// Network-ready for PUN2 integration with Ultimate support
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth = 100;
    [SerializeField] private bool invulnerableOnSpawn = true;
    [SerializeField] private float invulnerabilityDuration = 1f;

    [Header("UI References")]
    [SerializeField] private Slider healthBarSlider;
    [SerializeField] private Text healthText;
    [SerializeField] private Image healthBarFill;
    [SerializeField] private Color healthyColor = Color.green;
    [SerializeField] private Color lowHealthColor = Color.red;
    [SerializeField] private Color invulnerableColor = Color.yellow;
    [SerializeField] private float lowHealthThreshold = 0.3f;

    [Header("Visual Effects")]
    [SerializeField] private GameObject deathEffectPrefab;
    [SerializeField] private float respawnDelay = 3f;
    [SerializeField] private bool enableHealthRegeneration = false;
    [SerializeField] private float regenRate = 5f; // HP per second
    [SerializeField] private float regenDelay = 5f; // Delay after taking damage

    [Header("Audio")]
    [SerializeField] private AudioClip hurtSound;
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private AudioClip healSound;
    [SerializeField] private AudioClip invulnerabilitySound;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    // State management
    private bool isDead = false;
    private bool isInvulnerable = false;
    private bool hasTemporaryInvulnerability = false;
    private float lastDamageTime = 0f;
    private CharacterController characterController;
    private PlayerCharacter playerCharacter;
    private AudioSource audioSource;

    // Damage tracking
    private int totalDamageTaken = 0;
    private int totalDamageDealt = 0;
    private CharacterController lastAttacker;

    // Visual components
    private Renderer playerRenderer;
    private Color originalRendererColor;

    // Events
    public System.Action<int, int> OnHealthChanged; // currentHealth, maxHealth
    public System.Action<CharacterController> OnPlayerDeath; // killed player
    public System.Action<CharacterController> OnPlayerRespawn; // respawned player
    public System.Action<int, CharacterController> OnDamageTaken; // damage, attacker

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerCharacter = GetComponent<PlayerCharacter>();
        audioSource = GetComponent<AudioSource>();
        playerRenderer = GetComponentInChildren<Renderer>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = 0.7f;
        }

        // Store original renderer color
        if (playerRenderer != null)
        {
            originalRendererColor = playerRenderer.material.color;
        }

        // Initialize health
        currentHealth = maxHealth;

        // Auto-find UI elements if not assigned
        if (healthBarSlider == null)
        {
            healthBarSlider = FindHealthBarInUI();
        }
    }

    void Start()
    {
        UpdateHealthUI();

        if (invulnerableOnSpawn)
        {
            StartCoroutine(SpawnInvulnerability());
        }

        if (debugMode)
        {
            Debug.Log($"PlayerHealth initialized for {gameObject.name} - Health: {currentHealth}/{maxHealth}");
        }
    }

    void Update()
    {
        // Handle health regeneration
        if (enableHealthRegeneration && !isDead && currentHealth < maxHealth)
        {
            if (Time.time - lastDamageTime > regenDelay)
            {
                RegenerateHealth();
            }
        }

        // Debug controls
        if (debugMode)
        {
            if (Input.GetKeyDown(KeyCode.Minus))
            {
                TakeDamage(10, null);
            }

            if (Input.GetKeyDown(KeyCode.Plus))
            {
                Heal(10);
            }

            if (Input.GetKeyDown(KeyCode.I))
            {
                SetTemporaryInvulnerability(3f);
            }
        }
    }

    Slider FindHealthBarInUI()
    {
        // Try to find health bar by name convention
        string[] possibleNames = {
            $"{gameObject.name}_HealthBar",
            $"Player{GetPlayerNumber()}_HealthBar",
            "HealthBar"
        };

        foreach (string name in possibleNames)
        {
            GameObject healthBarGO = GameObject.Find(name);
            if (healthBarGO != null)
            {
                return healthBarGO.GetComponent<Slider>();
            }
        }

        return null;
    }

    int GetPlayerNumber()
    {
        // Simple player number detection
        if (gameObject.name.Contains("1")) return 1;
        if (gameObject.name.Contains("2")) return 2;
        return 0;
    }

    IEnumerator SpawnInvulnerability()
    {
        isInvulnerable = true;

        if (debugMode)
        {
            Debug.Log($"{gameObject.name} is invulnerable for {invulnerabilityDuration} seconds");
        }

        // Visual feedback for invulnerability (flashing)
        float flashInterval = 0.1f;
        float elapsed = 0f;

        while (elapsed < invulnerabilityDuration)
        {
            if (playerRenderer != null)
            {
                playerRenderer.enabled = !playerRenderer.enabled;
            }

            yield return new WaitForSeconds(flashInterval);
            elapsed += flashInterval;
        }

        // Ensure renderer is visible at the end
        if (playerRenderer != null)
        {
            playerRenderer.enabled = true;
        }

        isInvulnerable = false;

        if (debugMode)
        {
            Debug.Log($"{gameObject.name} invulnerability ended");
        }
    }

    /// <summary>
    /// Set temporary invulnerability for ultimate abilities
    /// </summary>
    public void SetTemporaryInvulnerability(float duration)
    {
        if (debugMode)
        {
            Debug.Log($"{gameObject.name} gained temporary invulnerability for {duration} seconds!");
        }

        PlaySound(invulnerabilitySound);
        StartCoroutine(TemporaryInvulnerabilityCoroutine(duration));
    }

    IEnumerator TemporaryInvulnerabilityCoroutine(float duration)
    {
        bool wasInvulnerable = isInvulnerable;
        isInvulnerable = true;
        hasTemporaryInvulnerability = true;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            // Pulsing effect to show invulnerability
            if (playerRenderer != null)
            {
                float pulse = Mathf.Sin(elapsed * 10f) * 0.3f + 0.7f;
                playerRenderer.material.color = Color.Lerp(originalRendererColor, invulnerableColor, pulse);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Restore original state and color
        isInvulnerable = wasInvulnerable;
        hasTemporaryInvulnerability = false;

        if (playerRenderer != null)
        {
            playerRenderer.material.color = originalRendererColor;
        }

        if (debugMode)
        {
            Debug.Log($"{gameObject.name} temporary invulnerability ended");
        }
    }

    public void TakeDamage(int damage, CharacterController attacker)
    {
        if (isDead || isInvulnerable) return;

        // Apply damage
        int actualDamage = Mathf.Min(damage, currentHealth);
        currentHealth -= actualDamage;
        totalDamageTaken += actualDamage;
        lastDamageTime = Time.time;
        lastAttacker = attacker;

        // Add ultimate charge for taking damage
        if (playerCharacter != null)
        {
            playerCharacter.OnDamageTaken(actualDamage);
        }

        // Play hurt sound
        PlaySound(hurtSound);

        // Trigger events
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDamageTaken?.Invoke(actualDamage, attacker);

        // Update UI
        UpdateHealthUI();

        if (debugMode)
        {
            string attackerName = attacker ? attacker.name : "Unknown";
            Debug.Log($"{gameObject.name} took {actualDamage} damage from {attackerName} - Health: {currentHealth}/{maxHealth}");
        }

        // Check for death
        if (currentHealth <= 0)
        {
            Die(attacker);
        }
        else
        {
            // Brief damage reaction
            StartCoroutine(DamageReaction());
        }
    }

    public void Heal(int healAmount)
    {
        if (isDead) return;

        int actualHeal = Mathf.Min(healAmount, maxHealth - currentHealth);
        currentHealth += actualHeal;

        // Play heal sound
        PlaySound(healSound);

        // Trigger events
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // Update UI
        UpdateHealthUI();

        if (debugMode)
        {
            Debug.Log($"{gameObject.name} healed {actualHeal} HP - Health: {currentHealth}/{maxHealth}");
        }
    }

    public void SetHealth(int newHealth)
    {
        currentHealth = Mathf.Clamp(newHealth, 0, maxHealth);
        UpdateHealthUI();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0 && !isDead)
        {
            Die(null);
        }
    }

    void Die(CharacterController killer)
    {
        if (isDead) return;

        isDead = true;
        currentHealth = 0;

        // Stop any temporary invulnerability
        if (hasTemporaryInvulnerability)
        {
            StopCoroutine(nameof(TemporaryInvulnerabilityCoroutine));
            hasTemporaryInvulnerability = false;
            isInvulnerable = false;
            if (playerRenderer != null)
            {
                playerRenderer.material.color = originalRendererColor;
            }
        }

        // Play death sound
        PlaySound(deathSound);

        // Create death effect
        if (deathEffectPrefab != null)
        {
            GameObject effect = Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, 3f);
        }

        // Trigger events
        OnPlayerDeath?.Invoke(characterController);

        // Update UI
        UpdateHealthUI();

        // Disable player controls
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        // Record stats
        if (killer != null)
        {
            PlayerHealth killerHealth = killer.GetComponent<PlayerHealth>();
            if (killerHealth != null)
            {
                killerHealth.totalDamageDealt += maxHealth; // Credit full health as damage dealt
            }
        }

        if (debugMode)
        {
            string killerName = killer ? killer.name : "Unknown";
            Debug.Log($"{gameObject.name} died! Killed by: {killerName}");
        }

        // Start respawn timer
        StartCoroutine(RespawnTimer());
    }

    IEnumerator RespawnTimer()
    {
        yield return new WaitForSeconds(respawnDelay);
        Respawn();
    }

    void Respawn()
    {
        if (!isDead) return;

        // Reset health
        currentHealth = maxHealth;
        isDead = false;
        totalDamageTaken = 0; // Reset damage taken for this life

        // Reset renderer color
        if (playerRenderer != null)
        {
            playerRenderer.material.color = originalRendererColor;
        }

        // Re-enable player
        if (characterController != null)
        {
            characterController.enabled = true;
        }

        // Reset position to spawn point
        Vector3 spawnPosition = GetSpawnPosition();
        transform.position = spawnPosition;

        // Start spawn invulnerability
        if (invulnerableOnSpawn)
        {
            StartCoroutine(SpawnInvulnerability());
        }

        // Trigger events
        OnPlayerRespawn?.Invoke(characterController);

        // Update UI
        UpdateHealthUI();

        if (debugMode)
        {
            Debug.Log($"{gameObject.name} respawned at {spawnPosition}");
        }
    }

    Vector3 GetSpawnPosition()
    {
        // Simple spawn position logic - you can enhance this
        if (gameObject.name.Contains("1"))
        {
            return new Vector3(-5f, 0f, 0f); // Player 1 spawn
        }
        else if (gameObject.name.Contains("2"))
        {
            return new Vector3(5f, 0f, 0f); // Player 2 spawn
        }

        return Vector3.zero; // Default spawn
    }

    void RegenerateHealth()
    {
        float regenAmount = regenRate * Time.deltaTime;
        int regenHP = Mathf.RoundToInt(regenAmount);

        if (regenHP > 0)
        {
            Heal(regenHP);
        }
    }

    IEnumerator DamageReaction()
    {
        // Brief pause/reaction to being hit
        // TODO: Add animation or visual effect
        yield return new WaitForSeconds(0.1f);
    }

    void UpdateHealthUI()
    {
        if (healthBarSlider != null)
        {
            float healthPercentage = (float)currentHealth / maxHealth;
            healthBarSlider.value = healthPercentage;

            // Update health bar color
            if (healthBarFill != null)
            {
                Color targetColor;
                if (hasTemporaryInvulnerability)
                {
                    targetColor = invulnerableColor;
                }
                else
                {
                    targetColor = healthPercentage <= lowHealthThreshold ? lowHealthColor : healthyColor;
                }
                healthBarFill.color = Color.Lerp(lowHealthColor, targetColor, healthPercentage);
            }
        }

        if (healthText != null)
        {
            healthText.text = $"{currentHealth}/{maxHealth}";
        }
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    /// <summary>
    /// Called when player successfully catches a ball - for ultimate charge
    /// </summary>
    public void OnSuccessfulCatch()
    {
        if (playerCharacter != null)
        {
            playerCharacter.OnSuccessfulCatch();
        }
    }

    /// <summary>
    /// Called when player successfully dodges - for ultimate charge
    /// </summary>
    public void OnSuccessfulDodge()
    {
        if (playerCharacter != null)
        {
            playerCharacter.OnSuccessfulDodge();
        }
    }

    // Public getters
    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;
    public float GetHealthPercentage() => (float)currentHealth / maxHealth;
    public bool IsDead() => isDead;
    public bool IsInvulnerable() => isInvulnerable;
    public bool HasTemporaryInvulnerability() => hasTemporaryInvulnerability;
    public int GetTotalDamageTaken() => totalDamageTaken;
    public int GetTotalDamageDealt() => totalDamageDealt;
    public CharacterController GetLastAttacker() => lastAttacker;

    // Network-ready methods (for future PUN2 integration)
    public void SetMaxHealth(int newMaxHealth)
    {
        maxHealth = newMaxHealth;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        UpdateHealthUI();
    }

    public void ForceRespawn()
    {
        if (isDead)
        {
            StopAllCoroutines();
            Respawn();
        }
    }

    /// <summary>
    /// Force remove all invulnerability effects (for admin/cheat prevention)
    /// </summary>
    public void RemoveAllInvulnerability()
    {
        StopCoroutine(nameof(TemporaryInvulnerabilityCoroutine));
        isInvulnerable = false;
        hasTemporaryInvulnerability = false;

        if (playerRenderer != null)
        {
            playerRenderer.material.color = originalRendererColor;
        }

        if (debugMode)
        {
            Debug.Log($"{gameObject.name} all invulnerability removed");
        }
    }

    // Debug methods
    void OnGUI()
    {
        if (!debugMode) return;

        float yOffset = gameObject.name.Contains("2") ? 150f : 50f;

        GUILayout.BeginArea(new Rect(10, yOffset, 250, 120));
        GUILayout.BeginVertical("box");

        GUILayout.Label($"{gameObject.name} Health");
        GUILayout.Label($"HP: {currentHealth}/{maxHealth}");
        GUILayout.Label($"Status: {(isDead ? "Dead" : "Alive")}");

        if (isInvulnerable)
        {
            string invulnType = hasTemporaryInvulnerability ? "TEMP INVULN" : "INVULNERABLE";
            GUILayout.Label(invulnType, GUI.skin.box);
        }

        GUILayout.Label($"Damage Taken: {totalDamageTaken}");
        GUILayout.Label($"Damage Dealt: {totalDamageDealt}");

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}