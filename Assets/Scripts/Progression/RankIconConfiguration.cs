using UnityEngine;

namespace RetroDodge.Progression
{
    /// <summary>
    /// ScriptableObject for managing rank icons configuration
    /// Separated from ProgressionConfiguration for better modularity and team workflow
    /// </summary>
    [CreateAssetMenu(fileName = "RankIconConfig", menuName = "RDR/Rank Icon Configuration")]
    public class RankIconConfiguration : ScriptableObject
    {
        [System.Serializable]
        public class RankIconData
        {
            [Header("Rank Information")]
            [Tooltip("Exact rank name (case-sensitive)")]
            public string rankName;
            
            [Header("Icon Sprites")]
            [Tooltip("Main rank icon sprite")]
            public Sprite iconSprite;
            
            [Tooltip("Optional: Smaller icon for compact UI elements")]
            public Sprite smallIcon;
            
            [Tooltip("Optional: Larger icon for detailed UI elements")]
            public Sprite largeIcon;
            
            [Header("Visual Properties")]
            [Tooltip("Optional: Custom color tint for the icon")]
            public Color iconColor = Color.white;
            
            [Tooltip("Optional: Icon size multiplier")]
            [Range(0.5f, 2f)]
            public float sizeMultiplier = 1f;
        }
        
        [Header("Rank Icons Configuration")]
        [Tooltip("Array of rank icon data")]
        public RankIconData[] rankIcons;
        
        [Header("Settings")]
        [Tooltip("Enable debug logging for icon loading")]
        public bool enableDebugLogs = true;
        
        /// <summary>
        /// Get rank icon sprite by rank name
        /// </summary>
        /// <param name="rankName">The rank name to look up</param>
        /// <param name="useSmallIcon">Whether to use small icon variant</param>
        /// <param name="useLargeIcon">Whether to use large icon variant</param>
        /// <returns>Sprite for the rank, or null if not found</returns>
        public Sprite GetRankIcon(string rankName, bool useSmallIcon = false, bool useLargeIcon = false)
        {
            if (string.IsNullOrEmpty(rankName))
            {
                if (enableDebugLogs) Debug.LogWarning("[RankIconConfiguration] Rank name is null or empty");
                return null;
            }
            
            var rankData = System.Array.Find(rankIcons, r => r.rankName == rankName);
            
            if (rankData == null)
            {
                if (enableDebugLogs) Debug.LogWarning($"[RankIconConfiguration] Rank icon not found for: {rankName}");
                return null;
            }
            
            // Return appropriate icon variant
            if (useLargeIcon && rankData.largeIcon != null)
            {
                return rankData.largeIcon;
            }
            else if (useSmallIcon && rankData.smallIcon != null)
            {
                return rankData.smallIcon;
            }
            else
            {
                return rankData.iconSprite;
            }
        }
        
        /// <summary>
        /// Get rank icon data by rank name
        /// </summary>
        /// <param name="rankName">The rank name to look up</param>
        /// <returns>RankIconData for the rank, or null if not found</returns>
        public RankIconData GetRankIconData(string rankName)
        {
            if (string.IsNullOrEmpty(rankName)) return null;
            
            return System.Array.Find(rankIcons, r => r.rankName == rankName);
        }
        
        /// <summary>
        /// Check if a rank icon exists
        /// </summary>
        /// <param name="rankName">The rank name to check</param>
        /// <returns>True if rank icon exists</returns>
        public bool HasRankIcon(string rankName)
        {
            if (string.IsNullOrEmpty(rankName)) return false;
            
            return System.Array.Exists(rankIcons, r => r.rankName == rankName);
        }
        
        /// <summary>
        /// Get all available rank names
        /// </summary>
        /// <returns>Array of all rank names</returns>
        public string[] GetAllRankNames()
        {
            string[] names = new string[rankIcons.Length];
            for (int i = 0; i < rankIcons.Length; i++)
            {
                names[i] = rankIcons[i].rankName;
            }
            return names;
        }
        
        /// <summary>
        /// Validate the configuration
        /// </summary>
        [ContextMenu("Validate Configuration")]
        public void ValidateConfiguration()
        {
            if (rankIcons == null || rankIcons.Length == 0)
            {
                Debug.LogWarning("[RankIconConfiguration] No rank icons configured!");
                return;
            }
            
            int validIcons = 0;
            int missingIcons = 0;
            
            foreach (var rankData in rankIcons)
            {
                if (string.IsNullOrEmpty(rankData.rankName))
                {
                    Debug.LogError("[RankIconConfiguration] Found rank data with empty rank name!");
                    continue;
                }
                
                if (rankData.iconSprite == null)
                {
                    Debug.LogError($"[RankIconConfiguration] Missing main icon for rank: {rankData.rankName}");
                    missingIcons++;
                }
                else
                {
                    validIcons++;
                }
            }
            
            Debug.Log($"[RankIconConfiguration] Validation complete: {validIcons} valid icons, {missingIcons} missing icons");
        }
        
        /// <summary>
        /// Get configuration summary for debugging
        /// </summary>
        [ContextMenu("Print Configuration Summary")]
        public void PrintConfigurationSummary()
        {
            Debug.Log("=== Rank Icon Configuration Summary ===");
            Debug.Log($"Total rank icons configured: {rankIcons?.Length ?? 0}");
            
            if (rankIcons != null)
            {
                foreach (var rankData in rankIcons)
                {
                    string iconStatus = rankData.iconSprite != null ? "✅" : "❌";
                    string smallStatus = rankData.smallIcon != null ? "✅" : "❌";
                    string largeStatus = rankData.largeIcon != null ? "✅" : "❌";
                    
                    Debug.Log($"  {rankData.rankName}: Main {iconStatus}, Small {smallStatus}, Large {largeStatus}");
                }
            }
            
            Debug.Log("=== End Summary ===");
        }
    }
}



