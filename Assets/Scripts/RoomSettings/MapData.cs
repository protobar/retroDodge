using UnityEngine;

/// <summary>
/// ScriptableObject for map data configuration
/// Allows easy addition of new maps without code changes
/// </summary>
[CreateAssetMenu(fileName = "MapData", menuName = "Game/Map Data")]
public class MapData : ScriptableObject
{
    [Header("Map Information")]
    [Tooltip("Unique identifier for the map")]
    public string mapId = "Arena1";
    
    [Tooltip("Display name shown in UI")]
    public string mapName = "Arena 1";
    
    [Tooltip("Scene name to load")]
    public string sceneName = "GameplayArena";
    
    [Tooltip("Map description shown in UI")]
    [TextArea(2, 4)]
    public string mapDescription = "The classic arena for intense dodgeball matches";
    
    [Header("Visual Elements")]
    [Tooltip("Map preview image for UI")]
    public Sprite mapPreview;
    
    [Tooltip("Map thumbnail for selection")]
    public Sprite mapThumbnail;
    
    [Header("Map Settings")]
    [Tooltip("Whether this map is unlocked by default")]
    public bool isUnlocked = true;
    
    [Tooltip("Map difficulty level (1-5)")]
    [Range(1, 5)]
    public int difficultyLevel = 1;
    
    [Tooltip("Recommended for competitive play")]
    public bool isCompetitive = true;
    
    [Tooltip("Map size category")]
    public MapSize mapSize = MapSize.Medium;
    
    [Header("Gameplay Modifiers")]
    [Tooltip("Special gameplay features on this map")]
    public string[] specialFeatures = new string[0];
    
    [Tooltip("Map-specific rules or modifications")]
    [TextArea(2, 3)]
    public string mapRules = "";
    
    // ═══════════════════════════════════════════════════════════════
    // ENUMS
    // ═══════════════════════════════════════════════════════════════
    
    public enum MapSize
    {
        Small,      // Close quarters, fast-paced
        Medium,     // Balanced gameplay
        Large,      // Strategic, longer matches
        ExtraLarge  // Epic battles, maximum strategy
    }
    
    // ═══════════════════════════════════════════════════════════════
    // VALIDATION
    // ═══════════════════════════════════════════════════════════════
    
    private void OnValidate()
    {
        // Ensure mapId is not empty
        if (string.IsNullOrEmpty(mapId))
            mapId = name;
        
        // Ensure mapName is not empty
        if (string.IsNullOrEmpty(mapName))
            mapName = mapId;
        
        // Ensure sceneName is not empty
        if (string.IsNullOrEmpty(sceneName))
            sceneName = "GameplayArena";
    }
    
    // ═══════════════════════════════════════════════════════════════
    // HELPER METHODS
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Get a formatted description of the map
    /// </summary>
    public string GetFormattedDescription()
    {
        string desc = mapDescription;
        
        if (!string.IsNullOrEmpty(mapRules))
            desc += $"\n\n<color=yellow>Special Rules:</color> {mapRules}";
        
        if (specialFeatures.Length > 0)
        {
            desc += $"\n\n<color=cyan>Features:</color> {string.Join(", ", specialFeatures)}";
        }
        
        return desc;
    }
    
    /// <summary>
    /// Get map size description
    /// </summary>
    public string GetSizeDescription()
    {
        switch (mapSize)
        {
            case MapSize.Small: return "Small Arena - Fast & Furious";
            case MapSize.Medium: return "Medium Arena - Balanced";
            case MapSize.Large: return "Large Arena - Strategic";
            case MapSize.ExtraLarge: return "Extra Large - Epic Battles";
            default: return "Unknown Size";
        }
    }
    
    /// <summary>
    /// Check if this map is available for selection
    /// </summary>
    public bool IsAvailable()
    {
        return isUnlocked;
    }
}
