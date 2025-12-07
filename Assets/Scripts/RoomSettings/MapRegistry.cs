using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Central registry for managing all available maps
/// Provides easy access to map data and handles map selection logic
/// </summary>
public class MapRegistry : MonoBehaviour
{
    [Header("Map Configuration")]
    [Tooltip("All available map data assets")]
    [SerializeField] private MapData[] availableMaps;
    
    [Tooltip("Default map to use if no selection is made")]
    [SerializeField] private MapData defaultMap;
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    
    // ═══════════════════════════════════════════════════════════════
    // SINGLETON PATTERN
    // ═══════════════════════════════════════════════════════════════
    
    private static MapRegistry _instance;
    public static MapRegistry Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<MapRegistry>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("MapRegistry");
                    _instance = go.AddComponent<MapRegistry>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════
    
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeMaps();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    private void InitializeMaps()
    {
        // If no maps are assigned, try to find them automatically
        if (availableMaps == null || availableMaps.Length == 0)
        {
            LoadMapsFromResources();
        }
        
        // Set default map if not assigned
        if (defaultMap == null && availableMaps.Length > 0)
        {
            defaultMap = availableMaps[0];
        }
        
        if (debugMode)
        {
            Debug.Log($"[MAP REGISTRY] Initialized with {availableMaps.Length} maps");
            foreach (var map in availableMaps)
            {
                Debug.Log($"[MAP REGISTRY] - {map.mapName} ({map.mapId}) - Unlocked: {map.isUnlocked}");
            }
        }
    }
    
    private void LoadMapsFromResources()
    {
        // Try to load maps from Resources folder
        MapData[] resourceMaps = Resources.LoadAll<MapData>("Maps");
        if (resourceMaps.Length > 0)
        {
            availableMaps = resourceMaps;
            if (debugMode) Debug.Log($"[MAP REGISTRY] Loaded {resourceMaps.Length} maps from Resources");
        }
        else
        {
            // Create a default map if none found
            CreateDefaultMap();
        }
    }
    
    private void CreateDefaultMap()
    {
        // Create a default map data
        MapData defaultMapData = ScriptableObject.CreateInstance<MapData>();
        defaultMapData.mapId = "Arena1";
        defaultMapData.mapName = "Arena 1";
        defaultMapData.sceneName = "GameplayArena";
        defaultMapData.mapDescription = "The classic arena for intense dodgeball matches";
        defaultMapData.isUnlocked = true;
        defaultMapData.mapSize = MapData.MapSize.Medium;
        
        availableMaps = new MapData[] { defaultMapData };
        defaultMap = defaultMapData;
        
        if (debugMode) Debug.Log("[MAP REGISTRY] Created default map");
    }
    
    // ═══════════════════════════════════════════════════════════════
    // PUBLIC API
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Get all available maps
    /// </summary>
    public MapData[] GetAllMaps()
    {
        return availableMaps?.Where(m => m != null).ToArray() ?? new MapData[0];
    }
    
    /// <summary>
    /// Get all unlocked maps
    /// </summary>
    public MapData[] GetUnlockedMaps()
    {
        return availableMaps?.Where(m => m != null && m.isUnlocked).ToArray() ?? new MapData[0];
    }
    
    /// <summary>
    /// Get map by ID
    /// </summary>
    public MapData GetMapById(string mapId)
    {
        if (string.IsNullOrEmpty(mapId)) return defaultMap;
        
        return availableMaps?.FirstOrDefault(m => m != null && m.mapId == mapId) ?? defaultMap;
    }
    
    /// <summary>
    /// Get map by scene name
    /// </summary>
    public MapData GetMapBySceneName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return defaultMap;
        
        return availableMaps?.FirstOrDefault(m => m != null && m.sceneName == sceneName) ?? defaultMap;
    }
    
    /// <summary>
    /// Get default map
    /// </summary>
    public MapData GetDefaultMap()
    {
        return defaultMap;
    }
    
    /// <summary>
    /// Get map names for UI dropdowns
    /// </summary>
    public string[] GetMapNames()
    {
        var unlockedMaps = GetUnlockedMaps();
        return unlockedMaps.Select(m => m.mapName).ToArray();
    }
    
    /// <summary>
    /// Get map IDs for UI dropdowns
    /// </summary>
    public string[] GetMapIds()
    {
        var unlockedMaps = GetUnlockedMaps();
        return unlockedMaps.Select(m => m.mapId).ToArray();
    }
    
    /// <summary>
    /// Get map previews for UI
    /// </summary>
    public Sprite[] GetMapPreviews()
    {
        var unlockedMaps = GetUnlockedMaps();
        return unlockedMaps.Select(m => m.mapPreview).ToArray();
    }
    
    /// <summary>
    /// Check if a map exists and is unlocked
    /// </summary>
    public bool IsMapAvailable(string mapId)
    {
        var map = GetMapById(mapId);
        return map != null && map.isUnlocked;
    }
    
    /// <summary>
    /// Get maps by size category
    /// </summary>
    public MapData[] GetMapsBySize(MapData.MapSize size)
    {
        return availableMaps?.Where(m => m != null && m.isUnlocked && m.mapSize == size).ToArray() ?? new MapData[0];
    }
    
    /// <summary>
    /// Get competitive maps only
    /// </summary>
    public MapData[] GetCompetitiveMaps()
    {
        return availableMaps?.Where(m => m != null && m.isUnlocked && m.isCompetitive).ToArray() ?? new MapData[0];
    }

    /// <summary>
    /// Get a random unlocked map ID.
    /// If competitiveOnly is true, prefers competitive maps and falls back to any unlocked map.
    /// Returns the default map ID (or \"Arena1\") if no maps are available.
    /// </summary>
    public string GetRandomUnlockedMapId(bool competitiveOnly = false)
    {
        MapData[] candidateMaps = null;

        if (competitiveOnly)
        {
            candidateMaps = GetCompetitiveMaps();
        }

        if (candidateMaps == null || candidateMaps.Length == 0)
        {
            candidateMaps = GetUnlockedMaps();
        }

        if (candidateMaps == null || candidateMaps.Length == 0)
        {
            return defaultMap != null ? defaultMap.mapId : "Arena1";
        }

        int index = UnityEngine.Random.Range(0, candidateMaps.Length);
        return candidateMaps[index].mapId;
    }
    
    // ═══════════════════════════════════════════════════════════════
    // EDITOR HELPERS
    // ═══════════════════════════════════════════════════════════════
    
    #if UNITY_EDITOR
    [ContextMenu("Refresh Maps")]
    private void RefreshMaps()
    {
        LoadMapsFromResources();
        if (debugMode) Debug.Log("[MAP REGISTRY] Maps refreshed");
    }
    
    [ContextMenu("Log All Maps")]
    private void LogAllMaps()
    {
        var maps = GetAllMaps();
        Debug.Log($"[MAP REGISTRY] Found {maps.Length} maps:");
        foreach (var map in maps)
        {
            Debug.Log($"- {map.mapName} ({map.mapId}) - Scene: {map.sceneName} - Unlocked: {map.isUnlocked}");
        }
    }
    #endif
}
