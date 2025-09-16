using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using Photon.Pun;
using System;
using System.Collections.Generic;

/// <summary>
/// PlayFab Authentication Manager
/// Handles all authentication methods: Email/Password, Guest, and Auto-login
/// Maintains persistent data across scenes
/// </summary>
public class PlayFabAuthManager : MonoBehaviour
{
    #region Singleton
    private static PlayFabAuthManager instance;
    public static PlayFabAuthManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<PlayFabAuthManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("PlayFabAuthManager");
                    instance = go.AddComponent<PlayFabAuthManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }
    #endregion

    #region Public Properties
    public string PlayFabId { get; private set; }
    public string PlayerDisplayName { get; private set; }
    public bool IsAuthenticated { get; private set; }
    public bool IsGuest { get; private set; }
    public string Email { get; private set; }
    #endregion

    #region Events
    public System.Action<bool, string> OnAuthenticationResult; // success, message
    public System.Action OnLoginSuccess;
    public System.Action<string> OnLoginError;
    #endregion

    #region Constants
    private const string EMAIL_KEY = "PlayFab_Email";
    private const string PASSWORD_KEY = "PlayFab_Password";
    private const string DISPLAY_NAME_KEY = "PlayFab_DisplayName";
    private const string GUEST_ID_KEY = "PlayFab_GuestId";
    #endregion

    void Start()
    {
        // Check for auto-login on startup
        CheckStoredCredentials();
    }

    #region Public Authentication Methods

    /// <summary>
    /// Login with email and password
    /// </summary>
    public void LoginWithEmail(string email, string password)
    {
        if (!IsValidEmail(email))
        {
            OnAuthenticationResult?.Invoke(false, "Invalid email format");
            return;
        }

        if (!IsValidPassword(password))
        {
            OnAuthenticationResult?.Invoke(false, "Password must be at least 6 characters");
            return;
        }

        var request = new LoginWithEmailAddressRequest
        {
            Email = email,
            Password = password,
            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetPlayerProfile = true,
                GetUserAccountInfo = true
            }
        };

        PlayFabClientAPI.LoginWithEmailAddress(request, OnLoginSuccessCallback, OnLoginErrorCallback);
    }

    /// <summary>
    /// Register new account with email and password
    /// </summary>
    public void RegisterAccount(string displayName, string email, string password, string confirmPassword)
    {
        if (!IsValidEmail(email))
        {
            OnAuthenticationResult?.Invoke(false, "Invalid email format");
            return;
        }

        if (!IsValidPassword(password))
        {
            OnAuthenticationResult?.Invoke(false, "Password must be at least 6 characters");
            return;
        }

        if (password != confirmPassword)
        {
            OnAuthenticationResult?.Invoke(false, "Passwords do not match");
            return;
        }

        if (string.IsNullOrEmpty(displayName) || displayName.Length < 2)
        {
            OnAuthenticationResult?.Invoke(false, "Display name must be at least 2 characters");
            return;
        }

        var request = new RegisterPlayFabUserRequest
        {
            Email = email,
            Password = password,
            DisplayName = displayName,
            RequireBothUsernameAndEmail = false
        };

        PlayFabClientAPI.RegisterPlayFabUser(request, OnRegisterSuccessCallback, OnRegisterErrorCallback);
    }

    /// <summary>
    /// Login as guest using device ID
    /// </summary>
    public void LoginAsGuest()
    {
        string deviceId = GetOrCreateDeviceId();

        var request = new LoginWithCustomIDRequest
        {
            CustomId = deviceId,
            CreateAccount = true,
            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetPlayerProfile = true,
                GetUserAccountInfo = true
            }
        };

        PlayFabClientAPI.LoginWithCustomID(request, OnGuestLoginSuccessCallback, OnLoginErrorCallback);
    }

    /// <summary>
    /// Logout and clear stored data
    /// </summary>
    public void Logout()
    {
        PlayFabId = null;
        PlayerDisplayName = null;
        Email = null;
        IsAuthenticated = false;
        IsGuest = false;

        // Clear stored credentials
        PlayerPrefs.DeleteKey(EMAIL_KEY);
        PlayerPrefs.DeleteKey(PASSWORD_KEY);
        PlayerPrefs.DeleteKey(DISPLAY_NAME_KEY);
        PlayerPrefs.DeleteKey(GUEST_ID_KEY);
        PlayerPrefs.Save();

        Debug.Log("[PLAYFAB AUTH] Logged out successfully");
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Check for stored credentials and attempt auto-login
    /// </summary>
    private void CheckStoredCredentials()
    {
        string email = PlayerPrefs.GetString(EMAIL_KEY, "");
        string password = PlayerPrefs.GetString(PASSWORD_KEY, "");
        string guestId = PlayerPrefs.GetString(GUEST_ID_KEY, "");

        if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
        {
            Debug.Log("[PLAYFAB AUTH] Found stored credentials, attempting auto-login");
            LoginWithEmail(email, password);
        }
        else if (!string.IsNullOrEmpty(guestId))
        {
            Debug.Log("[PLAYFAB AUTH] Found guest ID, attempting guest login");
            LoginAsGuest();
        }
    }

    /// <summary>
    /// Get or create unique device ID for guest login
    /// </summary>
    private string GetOrCreateDeviceId()
    {
        string deviceId = PlayerPrefs.GetString(GUEST_ID_KEY, "");

        if (string.IsNullOrEmpty(deviceId))
        {
            deviceId = SystemInfo.deviceUniqueIdentifier;
            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = "Guest_" + System.Guid.NewGuid().ToString();
            }
            PlayerPrefs.SetString(GUEST_ID_KEY, deviceId);
            PlayerPrefs.Save();
        }

        return deviceId;
    }

    /// <summary>
    /// Validate email format
    /// </summary>
    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validate password strength
    /// </summary>
    private bool IsValidPassword(string password)
    {
        return !string.IsNullOrEmpty(password) && password.Length >= 6;
    }

    /// <summary>
    /// Store credentials for auto-login
    /// </summary>
    private void StoreCredentials(string email, string password, string displayName = "")
    {
        PlayerPrefs.SetString(EMAIL_KEY, email);
        PlayerPrefs.SetString(PASSWORD_KEY, password);
        if (!string.IsNullOrEmpty(displayName))
        {
            PlayerPrefs.SetString(DISPLAY_NAME_KEY, displayName);
        }
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Sync PlayFab display name to Photon nickname
    /// </summary>
    private void SyncToPhoton()
    {
        if (!string.IsNullOrEmpty(PlayerDisplayName))
        {
            PhotonNetwork.NickName = PlayerDisplayName;
            Debug.Log($"[PLAYFAB AUTH] Synced display name to Photon: {PlayerDisplayName}");
        }
    }

    /// <summary>
    /// Get user-friendly error message from PlayFab error
    /// </summary>
    private string GetErrorMessage(PlayFabError error)
    {
        switch (error.Error)
        {
            case PlayFabErrorCode.InvalidEmailAddress:
                return "Please enter a valid email address";
            case PlayFabErrorCode.InvalidUsernameOrPassword:
                return "Invalid email or password. Please check your credentials and try again";
            case PlayFabErrorCode.EmailAddressNotAvailable:
                return "This email is already registered. Try signing in instead";
            case PlayFabErrorCode.AccountNotFound:
                return "No account found with this email. Please sign up first";
            case PlayFabErrorCode.InvalidPassword:
                return "Password is incorrect. Please try again";
            case PlayFabErrorCode.UsernameNotAvailable:
                return "This display name is already taken. Please choose another";
            case PlayFabErrorCode.InvalidParams:
                return "Please check all fields and try again";
            case PlayFabErrorCode.AccountBanned:
                return "This account has been suspended";
            case PlayFabErrorCode.AccountDeleted:
                return "This account has been deleted";
            default:
                // For generic errors, provide a more user-friendly message
                if (error.ErrorMessage.Contains("Invalid email address or password"))
                {
                    return "Invalid email or password. Please check your credentials and try again";
                }
                return "Authentication failed. Please try again";
        }
    }

    #endregion

    #region PlayFab Callbacks

    private void OnLoginSuccessCallback(LoginResult result)
    {
        PlayFabId = result.PlayFabId;
        PlayerDisplayName = result.InfoResultPayload?.PlayerProfile?.DisplayName ?? 
                          "Player_" + PlayFabId.Substring(0, 8);
        Email = PlayerPrefs.GetString(EMAIL_KEY, ""); // Get email from stored credentials
        IsAuthenticated = true;
        IsGuest = false;

        // Store credentials for auto-login
        StoreCredentials(Email, PlayerPrefs.GetString(PASSWORD_KEY, ""), PlayerDisplayName);

        // Sync to Photon
        SyncToPhoton();

        Debug.Log($"[PLAYFAB AUTH] Login successful: {PlayerDisplayName} ({Email})");
        OnAuthenticationResult?.Invoke(true, "Login successful!");
        OnLoginSuccess?.Invoke();
    }

    private void OnRegisterSuccessCallback(RegisterPlayFabUserResult result)
    {
        PlayFabId = result.PlayFabId;
        PlayerDisplayName = result.Username;
        Email = PlayerPrefs.GetString(EMAIL_KEY, ""); // Get email from stored credentials
        IsAuthenticated = true;
        IsGuest = false;

        // Store credentials for auto-login
        StoreCredentials(Email, PlayerPrefs.GetString(PASSWORD_KEY, ""), PlayerDisplayName);

        // Sync to Photon
        SyncToPhoton();

        Debug.Log($"[PLAYFAB AUTH] Registration successful: {PlayerDisplayName} ({Email})");
        OnAuthenticationResult?.Invoke(true, "Account created successfully!");
        OnLoginSuccess?.Invoke();
    }

    private void OnGuestLoginSuccessCallback(LoginResult result)
    {
        PlayFabId = result.PlayFabId;
        PlayerDisplayName = result.InfoResultPayload?.PlayerProfile?.DisplayName ?? 
                          "Guest_" + PlayFabId.Substring(0, 8);
        Email = null;
        IsAuthenticated = true;
        IsGuest = true;

        // Sync to Photon
        SyncToPhoton();

        Debug.Log($"[PLAYFAB AUTH] Guest login successful: {PlayerDisplayName}");
        OnAuthenticationResult?.Invoke(true, "Guest login successful!");
        OnLoginSuccess?.Invoke();
    }

    private void OnLoginErrorCallback(PlayFabError error)
    {
        string errorMessage = GetErrorMessage(error);
        Debug.LogWarning($"[PLAYFAB AUTH] Login failed: {errorMessage}");
        OnAuthenticationResult?.Invoke(false, errorMessage);
        OnLoginError?.Invoke(errorMessage);
    }

    private void OnRegisterErrorCallback(PlayFabError error)
    {
        string errorMessage = GetErrorMessage(error);
        Debug.LogWarning($"[PLAYFAB AUTH] Registration failed: {errorMessage}");
        OnAuthenticationResult?.Invoke(false, errorMessage);
        OnLoginError?.Invoke(errorMessage);
    }

    #endregion
}
