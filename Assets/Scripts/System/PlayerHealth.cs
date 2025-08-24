using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// PUN2 Multiplayer Player Health System
/// Authoritative health management with network synchronization
/// CLEANED UP: Removed all CharacterController dependencies
/// </summary>
public class PlayerHealth : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth = 100;
    [SerializeField] private bool invulnerableOnSpawn = true;
    [SerializeField] private float invulnerabilityDuration = 1f;

    [Header("Visual Effects")]
    [SerializeField] private GameObject deathEffectPrefab;
    [SerializeField] private bool enableHealthRegeneration = false;
    [SerializeField] private float regenRate = 5f;
    [SerializeField] private float regenDelay = 5f;

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
    private PlayerCharacter playerCharacter;
    private AudioSource audioSource;

    // Network synchronization
    private int networkHealth;
    private bool networkIsDead;
    private bool networkIsInvulnerable;

    // Damage tracking
    private int totalDamageTaken = 0;
    private PlayerCharacter lastAttacker; // CHANGED: From CharacterController to PlayerCharacter

    // Visual components
    private Renderer playerRenderer;
    private Color originalRendererColor;

    // Events - UPDATED: Changed CharacterController to PlayerCharacter
    public System.Action<int, int> OnHealthChanged;
    public System.Action<PlayerCharacter> OnPlayerDeath;
    public System.Action<PlayerCharacter> OnPlayerRespawn;
    public System.Action<int, PlayerCharacter> OnDamageTaken;

    void Awake()
    {
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
        networkHealth = currentHealth;
    }

    void Start()
    {
        if (invulnerableOnSpawn)
        {
            StartCoroutine(SpawnInvulnerability());
        }

        // Trigger initial health event
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    void Update()
    {
        // Only local player processes health regeneration
        if (!photonView.IsMine) return;

        // Handle health regeneration
        if (enableHealthRegeneration && !isDead && currentHealth < maxHealth)
        {
            if (Time.time - lastDamageTime > regenDelay)
            {
                RegenerateHealth();
            }
        }
    }

    IEnumerator SpawnInvulnerability()
    {
        isInvulnerable = true;

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
    }

    /// <summary>
    /// Network-ready damage method - only owner can take damage
    /// UPDATED: Changed attacker parameter from CharacterController to PlayerCharacter
    /// </summary>
    public void TakeDamage(int damage, PlayerCharacter attacker)
    {
        // Only the owner of this player can process damage
        if (!photonView.IsMine || isDead || isInvulnerable) return;

        // Process damage
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

        // Sync damage across network
        photonView.RPC("OnDamageReceived", RpcTarget.Others, currentHealth, actualDamage);

        // Play hurt sound
        PlaySound(hurtSound);

        // Trigger events
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDamageTaken?.Invoke(actualDamage, attacker);

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

    [PunRPC]
    void OnDamageReceived(int newHealth, int damageAmount)
    {
        // Update health for remote players (visual only)
        currentHealth = newHealth;

        // Play hurt sound
        PlaySound(hurtSound);

        // Trigger UI update
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // Visual damage reaction
        StartCoroutine(DamageReaction());
    }

    /// <summary>
    /// Ball damage RPC - UPDATED: Changed attacker handling to PlayerCharacter
    /// </summary>
    [PunRPC]
    void TakeDamageFromBall(int damage, int attackerViewID)
    {
        if (debugMode)
        {
            Debug.Log($"TakeDamageFromBall RPC received: {damage} damage");
            Debug.Log($"  - My PhotonView.IsMine: {photonView.IsMine}");
            Debug.Log($"  - My Owner: {photonView.Owner?.NickName ?? "NULL"}");
            Debug.Log($"  - Local Player: {PhotonNetwork.LocalPlayer?.NickName ?? "NULL"}");
            Debug.Log($"  - Dead: {isDead}, Invulnerable: {isInvulnerable}");
        }

        // Only process damage on the owner of this player
        if (!photonView.IsMine)
        {
            if (debugMode)
            {
                Debug.Log($"TakeDamageFromBall: Not my player ({photonView.Owner?.NickName}), ignoring damage");
            }
            return;
        }

        if (isDead || isInvulnerable)
        {
            if (debugMode)
            {
                Debug.Log($"TakeDamageFromBall: Player is dead ({isDead}) or invulnerable ({isInvulnerable}), ignoring damage");
            }
            return;
        }

        // Find the attacker PlayerCharacter - UPDATED: No more CharacterController lookup
        PlayerCharacter attacker = null;
        if (attackerViewID != -1)
        {
            PhotonView attackerView = PhotonView.Find(attackerViewID);
            if (attackerView != null)
            {
                attacker = attackerView.GetComponent<PlayerCharacter>();
            }
        }

        if (debugMode)
        {
            Debug.Log($"TakeDamageFromBall: Processing {damage} damage on {photonView.Owner?.NickName}");
            Debug.Log($"  - Current Health: {currentHealth}");
            Debug.Log($"  - Attacker ViewID: {attackerViewID}");
        }

        // Process damage using existing TakeDamage method
        TakeDamage(damage, attacker);

        if (debugMode)
        {
            Debug.Log($"TakeDamageFromBall: Health after damage: {currentHealth}");
        }
    }

    /// <summary>
    /// Network-ready healing method
    /// </summary>
    public void Heal(int healAmount)
    {
        if (!photonView.IsMine || isDead) return;

        int actualHeal = Mathf.Min(healAmount, maxHealth - currentHealth);
        currentHealth += actualHeal;

        // Sync healing across network
        photonView.RPC("OnHealReceived", RpcTarget.Others, currentHealth, actualHeal);

        // Play heal sound
        PlaySound(healSound);

        // Trigger events
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    [PunRPC]
    void OnHealReceived(int newHealth, int healAmount)
    {
        // Update health for remote players
        currentHealth = newHealth;

        // Play heal sound
        PlaySound(healSound);

        // Trigger UI update
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>
    /// Network-ready set health method - Master Client authority
    /// </summary>
    public void SetHealth(int newHealth)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            currentHealth = Mathf.Clamp(newHealth, 0, maxHealth);
            photonView.RPC("SyncHealth", RpcTarget.Others, currentHealth);

            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0 && !isDead)
            {
                Die(null);
            }
        }
    }

    [PunRPC]
    void SyncHealth(int newHealth)
    {
        currentHealth = newHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>
    /// Death handling - UPDATED: Changed killer parameter to PlayerCharacter
    /// </summary>
    void Die(PlayerCharacter killer)
    {
        if (!photonView.IsMine || isDead) return;

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

        // Sync death across network - UPDATED: Use PlayerCharacter ViewID instead of ActorNumber
        int killerViewID = -1;
        if (killer != null)
        {
            PhotonView killerView = killer.GetComponent<PhotonView>();
            if (killerView != null)
            {
                killerViewID = killerView.ViewID;
            }
        }

        photonView.RPC("OnPlayerDied", RpcTarget.Others, killerViewID);

        // Local death processing
        ProcessDeath(killer);
    }

    /// <summary>
    /// Network death sync - UPDATED: Changed to use ViewID instead of ActorNumber
    /// </summary>
    [PunRPC]
    void OnPlayerDied(int killerViewID)
    {
        // Remote player died - update visual state
        isDead = true;
        currentHealth = 0;

        // Find killer by ViewID - UPDATED: Simplified lookup
        PlayerCharacter killer = null;
        if (killerViewID != -1)
        {
            PhotonView killerView = PhotonView.Find(killerViewID);
            if (killerView != null)
            {
                killer = killerView.GetComponent<PlayerCharacter>();
            }
        }

        ProcessDeath(killer);
    }

    /// <summary>
    /// Process death effects - UPDATED: Changed killer parameter to PlayerCharacter
    /// </summary>
    void ProcessDeath(PlayerCharacter killer)
    {
        // Play death sound
        PlaySound(deathSound);

        // Create death effect
        if (deathEffectPrefab != null)
        {
            GameObject effect = Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, 3f);
        }

        // Trigger events - UPDATED: Pass PlayerCharacter instead of CharacterController
        OnPlayerDeath?.Invoke(playerCharacter);

        // Update UI
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // Disable player controls - UPDATED: Disable PlayerCharacter instead of CharacterController
        if (playerCharacter != null)
        {
            // You might want to add a method to PlayerCharacter to disable controls
            // For now, this is just a placeholder - adjust based on your PlayerCharacter implementation
            playerCharacter.enabled = false;
        }
    }

    /// <summary>
    /// Set temporary invulnerability for ultimate abilities
    /// </summary>
    public void SetTemporaryInvulnerability(float duration)
    {
        if (!photonView.IsMine) return;

        // Sync invulnerability across network
        photonView.RPC("StartTemporaryInvulnerability", RpcTarget.All, duration);
    }

    [PunRPC]
    void StartTemporaryInvulnerability(float duration)
    {
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
                Color invulnerableColor = Color.yellow;
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
        // Brief visual reaction to being hit
        if (playerRenderer != null)
        {
            Color originalColor = playerRenderer.material.color;
            playerRenderer.material.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            playerRenderer.material.color = originalColor;
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

    // IPunObservable for continuous health synchronization
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Send health data from owner
            stream.SendNext(currentHealth);
            stream.SendNext(isDead);
            stream.SendNext(isInvulnerable);
        }
        else
        {
            // Receive health data on non-owners
            networkHealth = (int)stream.ReceiveNext();
            networkIsDead = (bool)stream.ReceiveNext();
            networkIsInvulnerable = (bool)stream.ReceiveNext();

            // Smooth health updates for UI
            if (Mathf.Abs(networkHealth - currentHealth) > 1)
            {
                currentHealth = networkHealth;
                OnHealthChanged?.Invoke(currentHealth, maxHealth);
            }

            if (networkIsDead != isDead)
            {
                isDead = networkIsDead;
                if (isDead)
                {
                    ProcessDeath(null);
                }
            }

            isInvulnerable = networkIsInvulnerable;
        }
    }

    // Public getters - UPDATED: Changed return types to PlayerCharacter
    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;
    public float GetHealthPercentage() => (float)currentHealth / maxHealth;
    public bool IsDead() => isDead;
    public bool IsInvulnerable() => isInvulnerable;
    public bool HasTemporaryInvulnerability() => hasTemporaryInvulnerability;
    public int GetTotalDamageTaken() => totalDamageTaken;
    public PlayerCharacter GetLastAttacker() => lastAttacker; // CHANGED: Return PlayerCharacter

    // Network-ready utility methods
    public void SetMaxHealth(int newMaxHealth)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            maxHealth = newMaxHealth;
            currentHealth = Mathf.Min(currentHealth, maxHealth);
            photonView.RPC("SyncMaxHealth", RpcTarget.Others, maxHealth, currentHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }

    [PunRPC]
    void SyncMaxHealth(int newMaxHealth, int newCurrentHealth)
    {
        maxHealth = newMaxHealth;
        currentHealth = newCurrentHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>
    /// Force remove all invulnerability effects
    /// </summary>
    public void RemoveAllInvulnerability()
    {
        if (!photonView.IsMine) return;

        photonView.RPC("ForceRemoveInvulnerability", RpcTarget.All);
    }

    [PunRPC]
    void ForceRemoveInvulnerability()
    {
        StopCoroutine(nameof(TemporaryInvulnerabilityCoroutine));
        isInvulnerable = false;
        hasTemporaryInvulnerability = false;

        if (playerRenderer != null)
        {
            playerRenderer.material.color = originalRendererColor;
        }
    }

    void OnDestroy()
    {
        // Clean up any ongoing coroutines
        StopAllCoroutines();
    }
}