using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;

namespace RetroDodge.Progression
{
    /// <summary>
    /// UI component for displaying the competitive leaderboard
    /// Shows top 100 players with rank, name, icon, rank name, and SR
    /// </summary>
    public class LeaderboardUI : MonoBehaviour
    {
        [Header("Main Panel")]
        [SerializeField] private GameObject leaderboardPanel;
        [SerializeField] private Button openLeaderboardButton;
        [SerializeField] private Button closeLeaderboardButton;
        
        [Header("Leaderboard Content")]
        [SerializeField] private ScrollRect leaderboardScrollRect;
        [SerializeField] private Transform leaderboardContent;
        [SerializeField] private GameObject leaderboardEntryPrefab;
        
        [Header("Header Controls")]
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button findMeButton;
        [SerializeField] private TMP_Text loadingText;
        [SerializeField] private TMP_Text errorText;
        
        [Header("Player Highlight")]
        [SerializeField] private Color playerHighlightColor = new Color(0.2f, 0.8f, 1f, 0.3f);
        [SerializeField] private Color alternateRowColor = new Color(0.1f, 0.1f, 0.1f, 0.1f);
        [SerializeField] private Color normalRowColor = Color.clear;
        
        [Header("Settings")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private float scrollSpeed = 10f;
        [SerializeField] private float findMeScrollDuration = 1f;
        
        private List<GameObject> leaderboardEntries = new List<GameObject>();
        private string currentPlayerId;
        private bool isScrollingToPlayer = false;
        
        void Start()
        {
            SetupButtons();
            SubscribeToEvents();
            
            // Hide panel initially
            if (leaderboardPanel != null)
                leaderboardPanel.SetActive(false);
                
            // Get current player ID from PlayFabAuthManager
            if (PlayFabAuthManager.Instance != null && PlayFabAuthManager.Instance.IsAuthenticated)
            {
                currentPlayerId = PlayFabAuthManager.Instance.PlayFabId;
            }
            else
            {
                currentPlayerId = PlayFab.PlayFabSettings.staticSettings.TitleId; // Fallback
            }
        }
        
        void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
        
        private void SetupButtons()
        {
            if (openLeaderboardButton != null)
                openLeaderboardButton.onClick.AddListener(OpenLeaderboard);
                
            if (closeLeaderboardButton != null)
                closeLeaderboardButton.onClick.AddListener(CloseLeaderboard);
                
            if (refreshButton != null)
                refreshButton.onClick.AddListener(RefreshLeaderboard);
                
            if (findMeButton != null)
                findMeButton.onClick.AddListener(FindMe);
        }
        
        private void SubscribeToEvents()
        {
            LeaderboardManager.OnLeaderboardLoaded += OnLeaderboardLoaded;
            LeaderboardManager.OnLeaderboardError += OnLeaderboardError;
        }
        
        private void UnsubscribeFromEvents()
        {
            LeaderboardManager.OnLeaderboardLoaded -= OnLeaderboardLoaded;
            LeaderboardManager.OnLeaderboardError -= OnLeaderboardError;
        }
        
        /// <summary>
        /// Open the leaderboard panel
        /// </summary>
        public void OpenLeaderboard()
        {
            if (leaderboardPanel != null)
            {
                leaderboardPanel.SetActive(true);
                RefreshLeaderboard();
            }
        }
        
        /// <summary>
        /// Close the leaderboard panel
        /// </summary>
        public void CloseLeaderboard()
        {
            if (leaderboardPanel != null)
                leaderboardPanel.SetActive(false);
        }
        
        /// <summary>
        /// Refresh the leaderboard data
        /// </summary>
        public void RefreshLeaderboard()
        {
            if (LeaderboardManager.Instance == null)
            {
                ShowError("LeaderboardManager not found");
                return;
            }
            
            ShowLoading(true);
            HideError();
            
            LeaderboardManager.Instance.LoadLeaderboard();
        }
        
        /// <summary>
        /// Scroll to current player's rank
        /// </summary>
        public void FindMe()
        {
            if (LeaderboardManager.Instance == null || isScrollingToPlayer)
            {
                if (enableDebugLogs) Debug.LogWarning("[LeaderboardUI] Cannot find player - LeaderboardManager not found or already scrolling");
                return;
            }
            
            // First try to find player in current leaderboard
            int playerIndex = LeaderboardManager.Instance.GetPlayerRank(currentPlayerId);
            
            if (playerIndex == -1)
            {
                // Player not in current leaderboard, load leaderboard around player
                if (enableDebugLogs) Debug.Log("[LeaderboardUI] Player not in current leaderboard, loading around player");
                ShowLoading(true);
                HideError();
                LeaderboardManager.Instance.LoadLeaderboardAroundPlayer();
                return;
            }
            
            StartCoroutine(ScrollToPlayer(playerIndex));
        }
        
        private void OnLeaderboardLoaded(List<LeaderboardEntry> entries)
        {
            ShowLoading(false);
            HideError();
            PopulateLeaderboard(entries);
            
            // If we loaded leaderboard around player, automatically scroll to player
            if (entries.Count > 0 && entries.Count <= 20) // Around player typically returns fewer entries
            {
                int playerIndex = LeaderboardManager.Instance.GetPlayerRank(currentPlayerId);
                if (playerIndex != -1)
                {
                    StartCoroutine(ScrollToPlayer(playerIndex));
                }
            }
            
            if (enableDebugLogs) Debug.Log($"[LeaderboardUI] Loaded {entries.Count} leaderboard entries");
        }
        
        private void OnLeaderboardError(string errorMessage)
        {
            ShowLoading(false);
            ShowError($"Failed to load leaderboard: {errorMessage}");
            
            if (enableDebugLogs) Debug.LogError($"[LeaderboardUI] Leaderboard error: {errorMessage}");
        }
        
        private void PopulateLeaderboard(List<LeaderboardEntry> entries)
        {
            // Clear existing entries
            ClearLeaderboard();
            
            // Create new entries
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var entryGO = CreateLeaderboardEntry(entry, i);
                leaderboardEntries.Add(entryGO);
            }
        }
        
        private GameObject CreateLeaderboardEntry(LeaderboardEntry entry, int index)
        {
            var entryGO = Instantiate(leaderboardEntryPrefab, leaderboardContent);
            var entryUI = entryGO.GetComponent<LeaderboardEntryUI>();
            
            if (entryUI != null)
            {
                entryUI.SetupEntry(entry);
                
                // Highlight current player
                if (entry.playerId == currentPlayerId)
                {
                    entryUI.HighlightPlayer(playerHighlightColor);
                }
                else
                {
                    // Alternate row colors for readability
                    Color rowColor = (index % 2 == 0) ? normalRowColor : alternateRowColor;
                    entryUI.SetRowColor(rowColor);
                }
            }
            
            return entryGO;
        }
        
        private void ClearLeaderboard()
        {
            foreach (var entry in leaderboardEntries)
            {
                if (entry != null)
                    Destroy(entry);
            }
            leaderboardEntries.Clear();
        }
        
        private IEnumerator ScrollToPlayer(int playerIndex)
        {
            isScrollingToPlayer = true;
            
            if (leaderboardScrollRect == null || leaderboardEntries.Count == 0)
            {
                isScrollingToPlayer = false;
                yield break;
            }
            
            // Calculate target position
            float entryHeight = leaderboardEntries[0].GetComponent<RectTransform>().rect.height;
            float targetY = playerIndex * entryHeight;
            float contentHeight = leaderboardContent.GetComponent<RectTransform>().rect.height;
            float viewportHeight = leaderboardScrollRect.viewport.rect.height;
            
            // Normalize position (0 = top, 1 = bottom)
            float normalizedPosition = Mathf.Clamp01(targetY / (contentHeight - viewportHeight));
            
            // Smooth scroll to position
            float startPosition = leaderboardScrollRect.verticalNormalizedPosition;
            float elapsedTime = 0f;
            
            while (elapsedTime < findMeScrollDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / findMeScrollDuration;
                t = 1f - Mathf.Pow(1f - t, 3f); // Ease out cubic
                
                leaderboardScrollRect.verticalNormalizedPosition = Mathf.Lerp(startPosition, 1f - normalizedPosition, t);
                yield return null;
            }
            
            leaderboardScrollRect.verticalNormalizedPosition = 1f - normalizedPosition;
            isScrollingToPlayer = false;
            
            if (enableDebugLogs) Debug.Log($"[LeaderboardUI] Scrolled to player at rank {playerIndex + 1}");
        }
        
        private void ShowLoading(bool show)
        {
            if (loadingText != null)
                loadingText.gameObject.SetActive(show);
                
            if (refreshButton != null)
                refreshButton.interactable = !show;
        }
        
        private void ShowError(string message)
        {
            if (errorText != null)
            {
                errorText.text = message;
                errorText.gameObject.SetActive(true);
            }
        }
        
        private void HideError()
        {
            if (errorText != null)
                errorText.gameObject.SetActive(false);
        }
        
        /// <summary>
        /// Force refresh leaderboard (for debugging)
        /// </summary>
        [ContextMenu("Force Refresh Leaderboard")]
        public void ForceRefreshLeaderboard()
        {
            RefreshLeaderboard();
        }
    }
}
