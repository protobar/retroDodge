using UnityEngine;

/// <summary>
/// FIXED: Bootstrap script to ensure RoomStateManager exists from the start
/// Place this in MainMenu scene to ensure RoomStateManager is available throughout the entire flow
/// </summary>
public class RoomStateManagerBootstrap : MonoBehaviour
{
    [Header("Bootstrap Settings")]
    [SerializeField] private bool createOnStart = true;
    [SerializeField] private bool debugMode = false;
    
    void Start()
    {
        if (createOnStart)
        {
            // Ensure RoomStateManager exists from the very beginning
            var manager = RoomStateManager.GetOrCreateInstance();
            
            if (debugMode)
            {
                Debug.Log($"[BOOTSTRAP] RoomStateManager ensured in scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
                Debug.Log($"[BOOTSTRAP] Manager instance: {(manager != null ? "CREATED" : "FAILED")}");
            }
        }
    }
    
    void OnGUI()
    {
        if (!debugMode) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 100));
        GUILayout.Label("=== ROOM STATE MANAGER BOOTSTRAP ===");
        
        if (RoomStateManager.Instance != null)
        {
            GUILayout.Label("RoomStateManager: ACTIVE");
        }
        else
        {
            GUILayout.Label("RoomStateManager: NOT FOUND");
        }
        
        GUILayout.EndArea();
    }
}

