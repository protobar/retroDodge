using UnityEngine;
using System;
using UnityEngine.SceneManagement;

namespace RetroDodge.Progression
{
    /// <summary>
    /// Handles match results and applies progression rewards
    /// Connects the match system to the progression system
    /// </summary>
    public class MatchResultHandler : MonoBehaviour
    {
        [Header("Debug")]
        public bool enableDebugLogs = true;
        
        /// <summary>
        /// Singleton instance
        /// </summary>
        public static MatchResultHandler Instance { get; private set; }
        
        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        void Start()
        {
            // Subscribe to match events
            // Note: You'll need to connect this to your actual match end events
            // For now, this is a template that you can integrate with your existing match system
        }

        void OnEnable()
        {
            // Subscribe to scene loaded events to auto-assign to MatchManager
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            // Unsubscribe from scene loaded events
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>
        /// Automatically assign this MatchResultHandler to MatchManager when gameplay scene loads
        /// </summary>
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Check if this is a gameplay scene (contains MatchManager)
            if (scene.name.Contains("Gameplay") || scene.name.Contains("Match"))
            {
                StartCoroutine(AutoAssignToMatchManager());
            }
        }

        /// <summary>
        /// Coroutine to find and assign to MatchManager after a short delay
        /// </summary>
        System.Collections.IEnumerator AutoAssignToMatchManager()
        {
            // Wait multiple frames to ensure all objects are fully loaded
            yield return null;
            yield return null;
            
            // Try to find MatchManager with retries
            MatchManager matchManager = null;
            int attempts = 0;
            const int maxAttempts = 10;
            
            while (matchManager == null && attempts < maxAttempts)
            {
                matchManager = FindObjectOfType<MatchManager>();
                if (matchManager == null)
                {
                    attempts++;
                    yield return new WaitForSeconds(0.1f);
                }
            }
            
            if (matchManager != null)
            {
                // Use reflection to set the matchResultHandler field
                var field = typeof(MatchManager).GetField("matchResultHandler", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (field != null)
                {
                    field.SetValue(matchManager, this);
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[MatchResultHandler] Successfully assigned to MatchManager in {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} " +
                                $"(attempts: {attempts + 1})");
                    }
                }
                else
                {
                    Debug.LogError("[MatchResultHandler] Could not find matchResultHandler field in MatchManager");
                }
            }
            else
            {
                Debug.LogError($"[MatchResultHandler] No MatchManager found in {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} after {maxAttempts} attempts");
            }
        }
        
        /// <summary>
        /// Process a match result and apply rewards
        /// Call this when a match ends
        /// </summary>
        public void ProcessMatchResult(MatchResult matchResult)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[MatchResultHandler] Processing {matchResult.gameMode} match result. " +
                         $"Win: {matchResult.isWin}, Duration: {matchResult.matchDuration:F1}s, " +
                         $"Damage: {matchResult.damageDealt}/{matchResult.damageTaken}");
            }
            
            // Apply rewards through PlayerDataManager
            if (PlayerDataManager.Instance != null)
            {
                if (enableDebugLogs)
                {
                    Debug.Log("[MatchResultHandler] PlayerDataManager found, applying rewards...");
                }
                
                PlayerDataManager.Instance.ApplyMatchRewards(matchResult);
                
                if (enableDebugLogs)
                {
                    var rewards = PlayerDataManager.Instance.GetLastMatchRewards();
                    if (rewards.HasValue)
                    {
                        Debug.Log($"[MatchResultHandler] Rewards applied successfully - " +
                                $"XP: {rewards.Value.xpGained}, Coins: {rewards.Value.dodgeCoinsGained}, " +
                                $"SR: {rewards.Value.srChange}");
                    }
                    else
                    {
                        Debug.LogWarning("[MatchResultHandler] No rewards found after applying match result");
                    }
                }
            }
            else
            {
                Debug.LogError("[MatchResultHandler] PlayerDataManager not found. Cannot apply rewards.");
            }
        }
        
        /// <summary>
        /// Create a match result from match data
        /// Call this when a match ends to create the result data
        /// </summary>
        public MatchResult CreateMatchResult(
            GameMode gameMode,
            bool isWin,
            int finalScore,
            float matchDuration,
            string characterUsed,
            int damageDealt = 0,
            int damageTaken = 0,
            int opponentSR = 0,
            int currentSeriesMatch = 1,
            int totalSeriesMatches = 1,
            bool seriesCompleted = true,
            int seriesWinner = -1)
        {
            switch (gameMode)
            {
                case GameMode.Competitive:
                    return MatchResult.CreateCompetitive(
                        isWin, finalScore, matchDuration, characterUsed,
                        currentSeriesMatch, totalSeriesMatches, seriesCompleted, seriesWinner, opponentSR
                    );
                
                case GameMode.AI:
                    return MatchResult.CreateAI(
                        isWin, finalScore, matchDuration, characterUsed, damageDealt, damageTaken
                    );
                
                case GameMode.Custom:
                    return MatchResult.CreateCustom(
                        isWin, finalScore, matchDuration, characterUsed
                    );
                
                default: // Casual
                    return MatchResult.Create(
                        gameMode, isWin, finalScore, matchDuration, characterUsed
                    );
            }
        }
        
        /// <summary>
        /// Helper method to determine if a match was a comeback
        /// </summary>
        public bool WasComeback(int finalScore, int opponentScore, int maxRounds)
        {
            // Consider it a comeback if you won after being behind by 2+ rounds
            int roundsBehind = opponentScore - finalScore;
            return finalScore > opponentScore && roundsBehind >= 2;
        }
        
        /// <summary>
        /// Helper method to determine if a match was a choke
        /// </summary>
        public bool WasChoke(int finalScore, int opponentScore, int maxRounds)
        {
            // Consider it a choke if you lost after being ahead by 2+ rounds
            int roundsAhead = finalScore - opponentScore;
            return opponentScore > finalScore && roundsAhead >= 2;
        }
        
        /// <summary>
        /// Get the current game mode from room properties
        /// </summary>
        public GameMode GetCurrentGameMode()
        {
            // This should be integrated with your existing room state system
            // For now, return a default value
            return GameMode.Casual;
        }
        
        /// <summary>
        /// Check if this is the first win of the day
        /// </summary>
        public bool IsFirstWinOfDay()
        {
            if (PlayerDataManager.Instance != null && PlayerDataManager.Instance.IsDataLoaded())
            {
                var data = PlayerDataManager.Instance.GetPlayerData();
                return data.hasFirstWinOfDay;
            }
            return false;
        }
        
        /// <summary>
        /// Manually assign this handler to MatchManager (for debugging)
        /// </summary>
        [ContextMenu("Force Assign to MatchManager")]
        public void ForceAssignToMatchManager()
        {
            MatchManager matchManager = FindObjectOfType<MatchManager>();
            if (matchManager != null)
            {
                var field = typeof(MatchManager).GetField("matchResultHandler", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (field != null)
                {
                    field.SetValue(matchManager, this);
                    Debug.Log("[MatchResultHandler] Manually assigned to MatchManager");
                }
                else
                {
                    Debug.LogError("[MatchResultHandler] Could not find matchResultHandler field in MatchManager");
                }
            }
            else
            {
                Debug.LogError("[MatchResultHandler] No MatchManager found in scene");
            }
        }
    }
}
