using UnityEngine;

/// <summary>
/// Data structure for custom room settings
/// Handles match length, map selection, privacy, and other room configurations
/// </summary>
[System.Serializable]
public class RoomSettings
{
    [Header("Match Configuration")]
    [Tooltip("Match duration in seconds")]
    public int matchLengthSeconds = 60;
    
    [Header("Map Selection")]
    [Tooltip("Selected map identifier")]
    public string selectedMap = "Arena1";
    
    [Header("Room Privacy")]
    [Tooltip("Whether the room is private (requires password)")]
    public bool isPrivate = false;
    
    [Tooltip("Room password (only used if isPrivate is true)")]
    public string roomPassword = "";
    
    [Header("Advanced Settings")]
    [Tooltip("Maximum number of players allowed")]
    public int maxPlayers = 2;
    
    [Tooltip("Whether spectators are allowed")]
    public bool allowSpectators = false;
    
    [Tooltip("Whether to show room in lobby")]
    public bool isVisible = true;
    
    // ═══════════════════════════════════════════════════════════════
    // CONSTRUCTORS
    // ═══════════════════════════════════════════════════════════════
    
    public RoomSettings()
    {
        // Default constructor with sensible defaults
    }
    
    public RoomSettings(int matchLength, string map, bool isPrivate = false, string password = "")
    {
        matchLengthSeconds = matchLength;
        selectedMap = map;
        this.isPrivate = isPrivate;
        roomPassword = password;
    }
    
    // ═══════════════════════════════════════════════════════════════
    // VALIDATION METHODS
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Validates the room settings and returns any errors
    /// </summary>
    public string ValidateSettings()
    {
        if (matchLengthSeconds < 30 || matchLengthSeconds > 300)
            return "Match length must be between 30 seconds and 5 minutes";
        
        if (string.IsNullOrEmpty(selectedMap))
            return "Please select a map";
        
        if (isPrivate && string.IsNullOrEmpty(roomPassword))
            return "Private rooms require a password";
        
        if (maxPlayers < 2 || maxPlayers > 8)
            return "Max players must be between 2 and 8";
        
        return null; // No errors
    }
    
    /// <summary>
    /// Gets a human-readable description of the match length
    /// </summary>
    public string GetMatchLengthDescription()
    {
        if (matchLengthSeconds < 60)
            return $"{matchLengthSeconds}s";
        else if (matchLengthSeconds < 3600)
            return $"{matchLengthSeconds / 60}m";
        else
            return $"{matchLengthSeconds / 3600}h {(matchLengthSeconds % 3600) / 60}m";
    }
    
    /// <summary>
    /// Gets a human-readable description of the room privacy
    /// </summary>
    public string GetPrivacyDescription()
    {
        if (isPrivate)
            return "Private (Password Required)";
        else
            return "Public";
    }
    
    // ═══════════════════════════════════════════════════════════════
    // STATIC HELPER METHODS
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Get predefined match length options
    /// </summary>
    public static int[] GetMatchLengthOptions()
    {
        return new int[] { 30, 60, 120, 300 }; // 30s, 1min, 2min, 5min
    }
    
    /// <summary>
    /// Get human-readable match length descriptions
    /// </summary>
    public static string[] GetMatchLengthDescriptions()
    {
        return new string[] { "30s", "1m", "2m", "5m" };
    }
    
    /// <summary>
    /// Create a copy of these settings
    /// </summary>
    public RoomSettings Clone()
    {
        return new RoomSettings
        {
            matchLengthSeconds = this.matchLengthSeconds,
            selectedMap = this.selectedMap,
            isPrivate = this.isPrivate,
            roomPassword = this.roomPassword,
            maxPlayers = this.maxPlayers,
            allowSpectators = this.allowSpectators,
            isVisible = this.isVisible
        };
    }
}
