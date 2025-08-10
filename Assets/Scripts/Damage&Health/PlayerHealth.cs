using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Manages player health, damage, and death states
/// Network-ready for PUN2 integration
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

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    // State management
    private bool isDead = false;
    private bool isInvulnerable = false;
    private float lastDamageTime = 0f;
    private CharacterController characterController;
    private AudioSource audioSource;

    // Damage tracking
    private int totalDamageTaken = 0;
    private int totalDamageDealt = 0;
    private CharacterController lastAttacker;

    // Events
    public System.Action<int, int> OnHealthChanged; // currentHealth, maxHealth
    public System.Action<CharacterController> OnPlayerDeath; // killed player
    public System.Action<CharacterController> OnPlayerRespawn; // respawned player
    public System.Action<int, CharacterController> OnDamageTaken; // damage, attacker

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = 0.7f;
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

        // Debug key to test damage
        if (debugMode && Input.GetKeyDown(KeyCode.Minus))
        {
            TakeDamage(10, null);
        }

        if (debugMode && Input.GetKeyDown(KeyCode.Plus))
        {
            Heal(10);
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
        Renderer playerRenderer = GetComponentInChildren<Renderer>();

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

    public void TakeDamage(int damage, CharacterController attacker)
    {
        if (isDead || isInvulnerable) return;

        // Apply damage
        int actualDamage = Mathf.Min(damage, currentHealth);
        currentHealth -= actualDamage;
        totalDamageTaken += actualDamage;
        lastDamageTime = Time.time;
        lastAttacker = attacker;

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

        // Re-enable player
        if (characterController != null)
        {
            characterController.enabled = true;
        }

        // Reset position to spawn point
        // TODO: Implement proper spawn point system
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
                Color targetColor = healthPercentage <= lowHealthThreshold ? lowHealthColor : healthyColor;
                healthBarFill.color = Color.Lerp(lowHealthColor, healthyColor, healthPercentage);
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

    // Public getters
    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;
    public float GetHealthPercentage() => (float)currentHealth / maxHealth;
    public bool IsDead() => isDead;
    public bool IsInvulnerable() => isInvulnerable;
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

    // Debug methods
    void OnGUI()
    {
        if (!debugMode) return;

        float yOffset = gameObject.name.Contains("2") ? 100f : 50f;

        GUILayout.BeginArea(new Rect(10, yOffset, 200, 100));
        GUILayout.BeginVertical("box");

        GUILayout.Label($"{gameObject.name} Health");
        GUILayout.Label($"HP: {currentHealth}/{maxHealth}");
        GUILayout.Label($"Status: {(isDead ? "Dead" : "Alive")}");

        if (isInvulnerable)
        {
            GUILayout.Label("INVULNERABLE", GUI.skin.box);
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}