using UnityEngine;
using System.Collections;
using Photon.Pun;

/// <summary>
/// Optimized PlayerHealth with streamlined PUN2 networking
/// Removed redundant features and fixed PlayerCharacter compatibility
/// </summary>
public class PlayerHealth : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth = 100;
    [SerializeField] private float invulnerabilityDuration = 1f;

    [Header("Audio")]
    [SerializeField] private AudioClip hurtSound;
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private AudioClip invulnerabilitySound;

    // Core state
    private bool isDead = false;
    private bool isInvulnerable = false;
    private bool hasTemporaryInvulnerability = false;
    private float lastDamageTime = 0f;

    // Components - cached once
    private PlayerCharacter playerCharacter;
    private AudioSource audioSource;
    private Renderer playerRenderer;
    private Color originalColor;
    private bool isShowingDamageReaction = false;

    // Network sync
    private int networkHealth;
    private bool networkIsDead;

    // Events
    public System.Action<int, int> OnHealthChanged;
    public System.Action<PlayerCharacter> OnPlayerDeath;
    public System.Action<int, PlayerCharacter> OnDamageTaken;

    void Awake()
    {
        playerCharacter = GetComponent<PlayerCharacter>();
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        playerRenderer = GetComponentInChildren<Renderer>();

        audioSource.playOnAwake = false;
        audioSource.volume = 0.7f;

        if (playerRenderer != null)
        {
            originalColor = playerRenderer.material.color;
        }

        currentHealth = maxHealth;
        networkHealth = currentHealth;
    }

    void Start()
    {
        StartCoroutine(SpawnInvulnerability());
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    IEnumerator SpawnInvulnerability()
    {
        isInvulnerable = true;

        // Simple flashing effect
        if (playerRenderer != null)
        {
            for (float elapsed = 0f; elapsed < invulnerabilityDuration; elapsed += 0.1f)
            {
                playerRenderer.enabled = !playerRenderer.enabled;
                yield return new WaitForSeconds(0.1f);
            }
            playerRenderer.enabled = true;
        }
        else
        {
            yield return new WaitForSeconds(invulnerabilityDuration);
        }

        isInvulnerable = false;
    }

    /// <summary>
    /// Simplified damage method - authority on owner only
    /// </summary>
    public void TakeDamage(int damage, PlayerCharacter attacker)
    {
        if (!PhotonNetwork.OfflineMode && (!photonView.IsMine || isDead || isInvulnerable)) return;
        if (PhotonNetwork.OfflineMode && (isDead || isInvulnerable)) return;

        int actualDamage = Mathf.Min(damage, currentHealth);
        currentHealth -= actualDamage;
        lastDamageTime = Time.time;

        // Add ability charges
        playerCharacter?.OnDamageTaken(actualDamage);

        // Network sync
        if (!PhotonNetwork.OfflineMode)
        {
            photonView.RPC("OnDamageReceived", RpcTarget.Others, currentHealth, actualDamage);
        }

        // Effects and events
        PlaySound(hurtSound);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDamageTaken?.Invoke(actualDamage, attacker);

        if (currentHealth <= 0)
        {
            Die(attacker);
        }
        else
        {
            StartCoroutine(DamageReaction());
        }
    }

    [PunRPC]
    void OnDamageReceived(int newHealth, int damageAmount)
    {
        currentHealth = newHealth;
        PlaySound(hurtSound);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        StartCoroutine(DamageReaction());
    }

    /// <summary>
    /// Ball damage RPC - simplified
    /// </summary>
    [PunRPC]
    void TakeDamageFromBall(int damage, int attackerViewID)
    {
        if (!photonView.IsMine || isDead || isInvulnerable) return;

        PlayerCharacter attacker = null;
        if (attackerViewID != -1)
        {
            var attackerView = PhotonView.Find(attackerViewID);
            attacker = attackerView?.GetComponent<PlayerCharacter>();
        }

        TakeDamage(damage, attacker);
    }

    public void Die(PlayerCharacter killer)
    {
        if (isDead) return;
        if (!PhotonNetwork.OfflineMode && !photonView.IsMine) return;

        Debug.Log($"[DEATH] {gameObject.name} is dying. OfflineMode: {PhotonNetwork.OfflineMode}");

        isDead = true;
        currentHealth = 0;

        int killerViewID = killer?.GetComponent<PhotonView>()?.ViewID ?? -1;

        // Sync death to other clients
        if (!PhotonNetwork.OfflineMode)
        {
            photonView.RPC("OnPlayerDied", RpcTarget.Others, killerViewID);
        }

        // Process death locally
        ProcessDeath(killer);

        // FIXED: Notify MatchManager that ANY client can trigger round end
        MatchManager matchManager = FindObjectOfType<MatchManager>();
        if (matchManager != null)
        {
            if (PhotonNetwork.OfflineMode)
            {
                // In offline, winner is the other side (1 or 2)
                int mySide = playerCharacter != null ? playerCharacter.GetPlayerSide() : 0;
                int winner = mySide == 1 ? 2 : 1;
                Debug.Log($"[DEATH] Player {mySide} died, winner is {winner}. Requesting round end...");
                matchManager.RequestRoundEnd(winner, "knockout");
            }
            else
            {
                // Determine winner based on who died
                int winnerActorNumber = 0;
                PlayerCharacter[] allPlayers = FindObjectsOfType<PlayerCharacter>();

                foreach (PlayerCharacter player in allPlayers)
                {
                    if (player != playerCharacter && player.photonView != null)
                    {
                        winnerActorNumber = player.photonView.Owner.ActorNumber;
                        break;
                    }
                }

                if (winnerActorNumber > 0)
                {
                    // Use the new RequestRoundEnd method that works for any client
                    matchManager.RequestRoundEnd(winnerActorNumber, "knockout");
                }
            }
        }
        else
        {
            Debug.LogError("[DEATH] MatchManager not found! Round will not end properly.");
        }
    }

    [PunRPC]
    void OnPlayerDied(int killerViewID)
    {
        isDead = true;
        currentHealth = 0;

        PlayerCharacter killer = null;
        if (killerViewID != -1)
        {
            var killerView = PhotonView.Find(killerViewID);
            killer = killerView?.GetComponent<PlayerCharacter>();
        }

        ProcessDeath(killer);
    }

    void ProcessDeath(PlayerCharacter killer)
    {
        PlaySound(deathSound);
        OnPlayerDeath?.Invoke(playerCharacter);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // FIXED: Don't disable PlayerCharacter component - just disable controls
        if (playerCharacter != null)
        {
            playerCharacter.SetInputEnabled(false);
            playerCharacter.SetMovementEnabled(false);
            // Keep component enabled for round system to work properly
        }
    }

    /// <summary>
    /// FIXED: Add respawn/revive method for round system
    /// </summary>
    public void RevivePlayer()
    {
        if (!photonView.IsMine) return;

        // Reset health and state
        currentHealth = maxHealth;
        isDead = false;
        isInvulnerable = false;
        hasTemporaryInvulnerability = false;

        // Re-enable player controls
        if (playerCharacter != null)
        {
            playerCharacter.SetInputEnabled(true);
            playerCharacter.SetMovementEnabled(true);
            playerCharacter.ResetForNewMatch(); // Reset player state
        }

        // Sync revival across network
        if (!PhotonNetwork.OfflineMode)
        {
            photonView.RPC("OnPlayerRevived", RpcTarget.Others);
        }

        // Trigger events
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // Brief spawn invulnerability
        StartCoroutine(SpawnInvulnerability());
    }

    [PunRPC]
    void OnPlayerRevived()
    {
        // Remote player revived
        currentHealth = maxHealth;
        isDead = false;
        isInvulnerable = false;
        hasTemporaryInvulnerability = false;

        // Restore visual state
        if (playerRenderer != null)
        {
            playerRenderer.material.color = originalColor;
        }

        // Trigger UI update
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // Re-enable controls for remote players too
        if (playerCharacter != null)
        {
            playerCharacter.SetInputEnabled(true);
            playerCharacter.SetMovementEnabled(true);
        }
    }

    /// <summary>
    /// FIXED: Reset health for new round (called by MatchManager)
    /// </summary>
    public void ResetHealthForNewRound()
    {
        if (!PhotonNetwork.OfflineMode && !photonView.IsMine) return; // Only owner can reset health in online mode

        Debug.Log($"[HEALTH RESET] Resetting health for {gameObject.name}");

        currentHealth = maxHealth;
        isDead = false;
        isInvulnerable = false;
        hasTemporaryInvulnerability = false;

        // Sync health reset to other clients
        if (!PhotonNetwork.OfflineMode)
        {
            photonView.RPC("SyncHealthReset", RpcTarget.Others, maxHealth);
        }

        // Trigger health changed event
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // Re-enable player if disabled
        if (playerCharacter != null)
        {
            playerCharacter.SetInputEnabled(false); // Will be enabled when fight starts
            playerCharacter.SetMovementEnabled(false);
        }
    }

    [PunRPC]
    void SyncHealthReset(int resetHealth)
    {
        currentHealth = resetHealth;
        isDead = false;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        Debug.Log($"[HEALTH SYNC] Health reset synced: {currentHealth}/{maxHealth}");
    }

    /// <summary>
    /// Simplified temporary invulnerability for abilities
    /// </summary>
    public void SetTemporaryInvulnerability(float duration)
    {
        if (!photonView.IsMine) return;
        if (PhotonNetwork.OfflineMode)
        {
            StartTemporaryInvulnerability(duration);
        }
        else
        {
            photonView.RPC("StartTemporaryInvulnerability", RpcTarget.All, duration);
        }
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

        // Simple pulsing effect
        Color characterColor = GetCharacterIntendedColor();
        float elapsed = 0f;
        while (elapsed < duration && playerRenderer != null)
        {
            float pulse = Mathf.Sin(elapsed * 10f) * 0.3f + 0.7f;
            playerRenderer.material.color = Color.Lerp(characterColor, Color.yellow, pulse);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Restore state
        isInvulnerable = wasInvulnerable;
        hasTemporaryInvulnerability = false;
        if (playerRenderer != null)
        {
            playerRenderer.material.color = characterColor;
        }
    }

    IEnumerator DamageReaction()
    {
        if (playerRenderer != null && !isShowingDamageReaction)
        {
            isShowingDamageReaction = true;
            
            // Get the character's intended color from CharacterData
            Color characterIntendedColor = GetCharacterIntendedColor();
            
            // Flash red
            playerRenderer.material.color = Color.red;
            yield return new WaitForSeconds(0.2f);
            
            // Reset to character's intended color
            if (playerRenderer != null)
            {
                playerRenderer.material.color = characterIntendedColor;
            }
            
            isShowingDamageReaction = false;
        }
    }
    
    Color GetCharacterIntendedColor()
    {
        // Get the character's intended color from CharacterData
        var playerCharacter = GetComponent<PlayerCharacter>();
        if (playerCharacter != null && playerCharacter.GetCharacterData() != null)
        {
            return playerCharacter.GetCharacterData().characterColor;
        }
        
        // Fallback to stored original color
        if (originalColor != Color.clear)
        {
            return originalColor;
        }
        
        // Final fallback
        return Color.white;
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // Network sync for health display
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(currentHealth);
            stream.SendNext(isDead);
        }
        else
        {
            networkHealth = (int)stream.ReceiveNext();
            networkIsDead = (bool)stream.ReceiveNext();

            if (Mathf.Abs(networkHealth - currentHealth) > 1)
            {
                currentHealth = networkHealth;
                OnHealthChanged?.Invoke(currentHealth, maxHealth);
            }

            if (networkIsDead != isDead)
            {
                isDead = networkIsDead;
                if (isDead) ProcessDeath(null);
            }
        }
    }

    // Simplified public API
    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;
    public float GetHealthPercentage() => (float)currentHealth / maxHealth;
    public bool IsDead() => isDead;
    public bool IsInvulnerable() => isInvulnerable;

    public void SetMaxHealth(int newMaxHealth)
    {
        if (PhotonNetwork.OfflineMode)
        {
            maxHealth = newMaxHealth;
            currentHealth = Mathf.Min(currentHealth, maxHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            return;
        }
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

    public void SetHealth(int newHealth)
    {
        if (PhotonNetwork.OfflineMode)
        {
            currentHealth = Mathf.Clamp(newHealth, 0, maxHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            if (currentHealth <= 0 && !isDead)
            {
                Die(null);
            }
            return;
        }
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
}