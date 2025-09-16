using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Connection Manager - Handles scene flow and Photon connection after authentication
/// Manages the transition from Connection scene to MainMenu scene
/// </summary>
public class ConnectionManager : MonoBehaviourPunCallbacks
{
    [Header("Scene Settings")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private float connectionTimeout = 10f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    // State management
    private bool isConnectingToPhoton = false;
    private float connectionStartTime = 0f;
    private bool hasTransitionedToMainMenu = false;

    // References
    private ConnectionUI connectionUI;

    void Start()
    {
        // Find ConnectionUI component
        connectionUI = FindObjectOfType<ConnectionUI>();
        if (connectionUI == null)
        {
            Debug.LogWarning("[CONNECTION MANAGER] ConnectionUI not found!");
        }

        // Subscribe to PlayFab authentication events
        PlayFabAuthManager.Instance.OnLoginSuccess += OnAuthenticationSuccess;
        PlayFabAuthManager.Instance.OnLoginError += OnAuthenticationError;

        // Check if already authenticated
        if (PlayFabAuthManager.Instance.IsAuthenticated)
        {
            Debug.Log("[CONNECTION MANAGER] Already authenticated, connecting to Photon...");
            ConnectToPhoton();
        }
        else
        {
            Debug.Log("[CONNECTION MANAGER] Waiting for authentication...");
        }
    }

    void Update()
    {
        // Handle connection timeout
        if (isConnectingToPhoton && Time.time - connectionStartTime > connectionTimeout)
        {
            Debug.LogWarning("[CONNECTION MANAGER] Photon connection timeout");
            OnConnectionTimeout();
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from events
        if (PlayFabAuthManager.Instance != null)
        {
            PlayFabAuthManager.Instance.OnLoginSuccess -= OnAuthenticationSuccess;
            PlayFabAuthManager.Instance.OnLoginError -= OnAuthenticationError;
        }
    }

    #region Authentication Event Handlers

    private void OnAuthenticationSuccess()
    {
        Debug.Log("[CONNECTION MANAGER] Authentication successful, connecting to Photon...");
        
        if (connectionUI != null)
        {
            connectionUI.ShowLoadingPanel("Connecting to servers...");
        }

        ConnectToPhoton();
    }

    private void OnAuthenticationError(string errorMessage)
    {
        Debug.LogWarning($"[CONNECTION MANAGER] Authentication failed: {errorMessage}");
        
        if (connectionUI != null)
        {
            connectionUI.ShowError(errorMessage);
        }
    }

    #endregion

    #region Photon Connection

    private void ConnectToPhoton()
    {
        if (isConnectingToPhoton) return;

        isConnectingToPhoton = true;
        connectionStartTime = Time.time;

        Debug.Log("[CONNECTION MANAGER] Starting Photon connection...");

        // Configure Photon settings
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.GameVersion = "1.0";
        PhotonNetwork.SendRate = 20;
        PhotonNetwork.SerializationRate = 10;

        // Connect to Photon
        PhotonNetwork.ConnectUsingSettings();
    }

    private void OnConnectionTimeout()
    {
        isConnectingToPhoton = false;
        
        if (connectionUI != null)
        {
            connectionUI.ShowError("Connection timeout. Please check your internet connection and try again.");
        }

        Debug.LogWarning("[CONNECTION MANAGER] Connection timeout");
    }

    #endregion

    #region Photon Callbacks

    public override void OnConnectedToMaster()
    {
        Debug.Log("[CONNECTION MANAGER] Connected to Photon Master Server");
        
        isConnectingToPhoton = false;

        if (connectionUI != null)
        {
            connectionUI.ShowLoadingPanel("Connection successful! Loading main menu...");
        }

        // Transition to MainMenu scene
        TransitionToMainMenu();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"[CONNECTION MANAGER] Disconnected from Photon: {cause}");
        
        isConnectingToPhoton = false;

        if (hasTransitionedToMainMenu) return; // Don't show error if we've already moved to main menu

        // Handle different types of disconnections
        if (connectionUI != null)
        {
            string errorMessage = GetDisconnectErrorMessage(cause);
            connectionUI.ShowError(errorMessage);
        }
    }

    // Note: OnConnectionFail doesn't exist in PUN2 MonoBehaviourPunCallbacks
    // Connection failures are handled by OnDisconnected callback

    #endregion

    #region Scene Transition

    private void TransitionToMainMenu()
    {
        if (hasTransitionedToMainMenu) return;

        hasTransitionedToMainMenu = true;

        Debug.Log("[CONNECTION MANAGER] Transitioning to MainMenu scene...");

        // Small delay to show success message
        Invoke(nameof(LoadMainMenuScene), 1f);
    }

    private void LoadMainMenuScene()
    {
        try
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[CONNECTION MANAGER] Failed to load MainMenu scene: {ex.Message}");
            
            if (connectionUI != null)
            {
                connectionUI.ShowError("Failed to load main menu. Please restart the game.");
            }
        }
    }

    #endregion

    #region Utility Methods

    private string GetDisconnectErrorMessage(DisconnectCause cause)
    {
        switch (cause)
        {
            case DisconnectCause.ClientTimeout:
                return "Connection timeout. Please check your internet connection.";
            case DisconnectCause.ServerTimeout:
                return "Server timeout. Please try again later.";
            case DisconnectCause.DisconnectByClientLogic:
                return "Connection cancelled.";
            case DisconnectCause.DisconnectByServerLogic:
                return "Server disconnected. Please try again.";
            case DisconnectCause.InvalidRegion:
                return "Invalid region selected.";
            case DisconnectCause.CustomAuthenticationFailed:
                return "Authentication failed. Please try logging in again.";
            case DisconnectCause.AuthenticationTicketExpired:
                return "Session expired. Please log in again.";
            case DisconnectCause.Exception:
                return "Connection error. Please check your internet connection.";
            default:
                return "Connection failed. Please try again.";
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Manually trigger connection to Photon (for testing)
    /// </summary>
    [ContextMenu("Test Photon Connection")]
    public void TestPhotonConnection()
    {
        if (PlayFabAuthManager.Instance.IsAuthenticated)
        {
            ConnectToPhoton();
        }
        else
        {
            Debug.LogWarning("[CONNECTION MANAGER] Cannot test connection - not authenticated");
        }
    }

    /// <summary>
    /// Force transition to MainMenu (for testing)
    /// </summary>
    [ContextMenu("Force Load MainMenu")]
    public void ForceLoadMainMenu()
    {
        LoadMainMenuScene();
    }

    #endregion
}
