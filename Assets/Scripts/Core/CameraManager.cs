using UnityEngine;

/// <summary>
/// Camera Manager - Integrates CameraController with MatchManager
/// Ensures camera works properly in both offline and online modes
/// </summary>
public class CameraManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraController cameraController;
    [SerializeField] private MatchManager matchManager;

    // Camera shake is now handled directly by CameraController
    // This manager only handles camera refresh and bounds

    void Start()
    {
        // Find components if not assigned
        if (cameraController == null)
            cameraController = FindObjectOfType<CameraController>();
        
        if (matchManager == null)
            matchManager = FindObjectOfType<MatchManager>();

        // Camera shake is now handled directly by CameraController
        // No need to subscribe to damage events here
    }

    void OnDestroy()
    {
        // No event subscriptions to clean up
    }

    /// <summary>
    /// Refresh camera when players are spawned/respawned
    /// </summary>
    public void RefreshCamera()
    {
        if (cameraController != null)
        {
            cameraController.RefreshPlayers();
        }
    }

    /// <summary>
    /// Set camera bounds based on arena size
    /// </summary>
    public void SetArenaBounds(float left, float right, float top, float bottom)
    {
        if (cameraController != null)
        {
            cameraController.SetBounds(left, right, top, bottom);
        }
    }
}
