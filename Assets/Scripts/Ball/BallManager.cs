using UnityEngine;

/// <summary>
/// Updated BallManager that integrates with the character system
/// FIXED: Updated to use new enum names from enhanced CharacterData
/// </summary>
public class BallManager : MonoBehaviour
{
    public static BallManager Instance { get; private set; }

    [Header("Ball Settings")]
    [SerializeField] public GameObject ballPrefab;
    [SerializeField] private Vector3 spawnPosition = new Vector3(0, 2f, 0);
    [SerializeField] private float respawnDelay = 2f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    // Current ball reference
    private BallController currentBall;

    // Game state
    private bool gameActive = true;

    void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        SpawnBall();
    }

    void SpawnBall()
    {
        if (ballPrefab == null)
        {
            Debug.LogError("Ball prefab not assigned in BallManager!");
            return;
        }

        // Destroy existing ball if any
        if (currentBall != null)
        {
            Destroy(currentBall.gameObject);
        }

        // Spawn new ball
        GameObject ballObj = Instantiate(ballPrefab, spawnPosition, Quaternion.identity);
        currentBall = ballObj.GetComponent<BallController>();

        if (currentBall == null)
        {
            Debug.LogError("Ball prefab doesn't have BallController component!");
        }

        Debug.Log("Ball spawned at center!");
    }

    /// <summary>
    /// Request ball pickup - works with any character type
    /// </summary>
    public void RequestBallPickup(PlayerCharacter character)
    {
        if (currentBall != null && gameActive)
        {
            bool success = currentBall.TryPickup(character);
            if (success && debugMode)
            {
                string characterName = character.GetCharacterData()?.characterName ?? character.name;
                Debug.Log($"{characterName} successfully picked up the ball!");
            }
        }
    }

    /// <summary>
    /// LEGACY: For backward compatibility with old CharacterController
    /// </summary>
    public void RequestBallPickup(CharacterController character)
    {
        if (currentBall != null && gameActive)
        {
            bool success = currentBall.TryPickupLegacy(character);
            if (success && debugMode)
            {
                Debug.Log($"{character.name} successfully picked up the ball!");
            }
        }
    }

    /// <summary>
    /// NEW: Request ball throw with character data integration
    /// This method gets damage from the character's data
    /// </summary>
    public void RequestBallThrowWithCharacterData(PlayerCharacter thrower, CharacterData characterData, ThrowType throwType, int damage)
    {
        if (currentBall != null && currentBall.GetHolder() == thrower)
        {
            // Apply character-specific throw modifications
            float throwSpeed = characterData.GetThrowSpeed(GetBaseThrowSpeed(throwType));
            Vector3 direction = Vector3.right; // BallController will handle targeting

            // Apply accuracy modifier
            if (currentBall.GetCurrentTarget() != null)
            {
                Vector3 targetDir = (currentBall.GetCurrentTarget().position - currentBall.transform.position).normalized;
                direction = characterData.ApplyThrowAccuracy(targetDir);
            }

            // Set ball damage and throw properties
            currentBall.SetThrowData(throwType, damage, throwSpeed);

            // Execute throw
            currentBall.ThrowBall(direction, 1f);

            // Apply throw effects
            if (characterData.throwEffect != null)
            {
                Instantiate(characterData.throwEffect, currentBall.transform.position, Quaternion.identity);
            }

            if (debugMode)
            {
                Debug.Log($"{characterData.characterName} threw {throwType} ball: {damage} damage, {throwSpeed} speed");
            }

            // Schedule respawn if ball goes out of bounds
            StartCoroutine(CheckForRespawn());
        }
    }

    /// <summary>
    /// LEGACY: Simplified throw method for backward compatibility
    /// </summary>
    public void RequestBallThrowSimple(PlayerCharacter thrower, float power = 1f)
    {
        if (currentBall != null && currentBall.GetHolder() == thrower)
        {
            CharacterData characterData = thrower.GetCharacterData();
            if (characterData != null)
            {
                // Use character data for throw
                ThrowType throwType = thrower.IsGrounded() ? ThrowType.Normal : ThrowType.JumpThrow;
                int damage = characterData.GetThrowDamage(throwType);
                RequestBallThrowWithCharacterData(thrower, characterData, throwType, damage);
            }
            else
            {
                // Fallback to basic throw
                currentBall.ThrowBall(Vector3.right, power);
                StartCoroutine(CheckForRespawn());
            }
        }
    }

    /// <summary>
    /// LEGACY: For old CharacterController compatibility
    /// </summary>
    public void RequestBallThrowSimple(CharacterController thrower, float power = 1f)
    {
        if (currentBall != null && currentBall.GetHolderLegacy() == thrower)
        {
            // Basic throw without character data
            currentBall.ThrowBall(Vector3.right, power);
            StartCoroutine(CheckForRespawn());
        }
    }

    /// <summary>
    /// Get base throw speed based on throw type
    /// </summary>
    float GetBaseThrowSpeed(ThrowType throwType)
    {
        switch (throwType)
        {
            case ThrowType.Normal:
                return 18f;
            case ThrowType.JumpThrow:
                return 22f;
            case ThrowType.Ultimate:
                return 25f;
            default:
                return 18f;
        }
    }

    /// <summary>
    /// FIXED: Handle ultimate throws with new UltimateType enum
    /// </summary>
    public void RequestUltimateThrow(PlayerCharacter thrower, UltimateType ultimateType)
    {
        if (currentBall == null || currentBall.GetHolder() != thrower) return;

        CharacterData characterData = thrower.GetCharacterData();
        if (characterData == null) return;

        int ultimateDamage = characterData.GetUltimateDamage();

        switch (ultimateType)
        {
            case UltimateType.PowerThrow:
                // Single powerful throw
                RequestBallThrowWithCharacterData(thrower, characterData, ThrowType.Ultimate, ultimateDamage);
                break;

            case UltimateType.MultiThrow:
                // Create multiple balls (implementation handled by PlayerCharacter)
                StartCoroutine(MultiThrowCoroutine(thrower, characterData));
                break;

            case UltimateType.Curveball:
                // Enable curve behavior (implementation handled by PlayerCharacter)
                RequestBallThrowWithCharacterData(thrower, characterData, ThrowType.Ultimate, ultimateDamage);
                break;

            default:
                // Default to power throw
                RequestBallThrowWithCharacterData(thrower, characterData, ThrowType.Ultimate, ultimateDamage);
                break;
        }

        if (debugMode)
        {
            Debug.Log($"{characterData.characterName} used ultimate: {ultimateType}");
        }
    }

    /// <summary>
    /// Handle multi-throw ultimate (called from RequestUltimateThrow)
    /// </summary>
    System.Collections.IEnumerator MultiThrowCoroutine(PlayerCharacter thrower, CharacterData characterData)
    {
        int throwCount = characterData.GetMultiThrowCount();
        int damagePerBall = characterData.GetUltimateDamage();
        float throwSpeed = characterData.GetUltimateSpeed();

        for (int i = 0; i < throwCount; i++)
        {
            // Create a temporary ball for multi-throw
            GameObject tempBallObj = Instantiate(ballPrefab, thrower.transform.position + Vector3.up * 1.5f, Quaternion.identity);
            BallController tempBall = tempBallObj.GetComponent<BallController>();

            if (tempBall != null)
            {
                tempBall.SetThrowData(ThrowType.Ultimate, damagePerBall, throwSpeed);

                // Throw in spread pattern
                float spreadAngle = characterData.GetMultiThrowSpread();
                float angleOffset = (i - (throwCount - 1) * 0.5f) * (spreadAngle / throwCount);
                Vector3 throwDir = Quaternion.Euler(0, angleOffset, 0) * Vector3.right;
                throwDir.y = 0.1f;

                tempBall.ThrowBall(throwDir.normalized, 1f);
            }

            yield return new WaitForSeconds(characterData.GetMultiThrowDelay());
        }
    }

    public void ResetBall()
    {
        if (currentBall != null)
        {
            currentBall.ResetBall();
        }
        else
        {
            SpawnBall();
        }
    }

    System.Collections.IEnumerator CheckForRespawn()
    {
        yield return new WaitForSeconds(respawnDelay);

        // Check if ball is out of bounds or needs respawn
        if (currentBall != null)
        {
            Vector3 ballPos = currentBall.transform.position;

            // If ball fell too low or went too far out
            if (ballPos.y < -3f || Mathf.Abs(ballPos.x) > 20f)
            {
                SpawnBall();
            }
        }
    }

    // Public getters
    public BallController GetCurrentBall() => currentBall;
    public bool HasActiveBall() => currentBall != null;
    public Vector3 GetBallSpawnPosition() => spawnPosition;

    // Game state control
    public void SetGameActive(bool active)
    {
        gameActive = active;
    }

    // Character registration for multiplayer
    public void RegisterPlayer(int playerId, Transform playerTransform, PlayerCharacter playerCharacter, bool isLocal = true)
    {
        // TODO: Implement player registration for multiplayer
        if (debugMode)
        {
            string characterName = playerCharacter.GetCharacterData()?.characterName ?? "Unknown";
            Debug.Log($"Registered player {playerId} ({characterName}) - Local: {isLocal}");
        }
    }

    // Debug methods
    void Update()
    {
        // Debug key to reset ball
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetBall();
        }

        // Debug key to respawn ball
        if (Input.GetKeyDown(KeyCode.T))
        {
            SpawnBall();
        }

        // Debug key to show character info
        if (debugMode && Input.GetKeyDown(KeyCode.I))
        {
            ShowCharacterDebugInfo();
        }
    }

    void ShowCharacterDebugInfo()
    {
        PlayerCharacter[] allPlayers = FindObjectsOfType<PlayerCharacter>();

        Debug.Log("=== CHARACTER DEBUG INFO ===");
        foreach (PlayerCharacter player in allPlayers)
        {
            CharacterData data = player.GetCharacterData();
            if (data != null)
            {
                Debug.Log($"{data.characterName}: HP={data.maxHealth}, Speed={data.moveSpeed}, " +
                         $"Ultimate={data.ultimateType}, Trick={data.trickType}, Treat={data.treatType}");
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        // Draw spawn position
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(spawnPosition, 0.5f);

        // Draw character throw ranges (if ball exists)
        if (currentBall != null)
        {
            PlayerCharacter holder = currentBall.GetHolder();
            if (holder != null)
            {
                CharacterData data = holder.GetCharacterData();
                if (data != null)
                {
                    // Visualize throw accuracy
                    Gizmos.color = Color.yellow;
                    float accuracy = data.throwAccuracy;
                    Gizmos.DrawWireSphere(currentBall.transform.position, (1f - accuracy) * 2f);
                }
            }
        }
    }
}