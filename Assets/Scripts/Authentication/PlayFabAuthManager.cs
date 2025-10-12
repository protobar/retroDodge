using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using Photon.Pun;
using System;
using System.Collections;

/// <summary>
/// PlayFab Authentication Manager - FIXED VERSION
/// Uses secure RememberMe pattern instead of storing passwords
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
    public System.Action<bool, string> OnAuthenticationResult;
    public System.Action OnLoginSuccess;
    public System.Action<string> OnLoginError;
    #endregion

    #region Constants - UPDATED TO USE REMEMBERME PATTERN
    private const string REMEMBER_ME_KEY = "PlayFab_RememberMe";
    private const string REMEMBER_ME_ID_KEY = "PlayFab_RememberMeId";
    private const string EMAIL_KEY = "PlayFab_Email";
    private const string DISPLAY_NAME_KEY = "PlayFab_DisplayName";
    private const string AUTH_TYPE_KEY = "PlayFab_AuthType"; // "Email" or "Guest"
    private const string GUEST_ID_KEY = "PlayFab_GuestId";
    #endregion

    private bool hasAttemptedAutoLogin = false;

    void Start()
    {
        // Wait a frame to ensure PlayFab is fully initialized
        StartCoroutine(DelayedAutoLogin());
    }

    private IEnumerator DelayedAutoLogin()
    {
        // Wait for PlayFab to initialize
        yield return new WaitForSeconds(0.5f);

        if (!hasAttemptedAutoLogin)
        {
            hasAttemptedAutoLogin = true;
            AttemptAutoLogin();
        }
    }

    #region Public Authentication Methods

    /// <summary>
    /// Login with email and password
    /// </summary>
    public void LoginWithEmail(string email, string password, bool rememberMe = true)
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

        // FIXED: Store email and rememberMe preference for use in callback
        PlayerPrefs.SetString(EMAIL_KEY, email);
        PlayerPrefs.SetInt(REMEMBER_ME_KEY, rememberMe ? 1 : 0);
        PlayerPrefs.Save();

        PlayFabClientAPI.LoginWithEmailAddress(request,
            result => OnEmailLoginSuccess(result, rememberMe),
            OnLoginErrorCallback);
    }

    /// <summary>
    /// Register new account with email and password
    /// </summary>
    public void RegisterAccount(string displayName, string email, string password, string confirmPassword, bool rememberMe = true)
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

        // FIXED: Only store email and display name, NOT password
        PlayerPrefs.SetString(EMAIL_KEY, email);
        PlayerPrefs.SetString(DISPLAY_NAME_KEY, displayName);
        PlayerPrefs.SetInt(REMEMBER_ME_KEY, rememberMe ? 1 : 0);
        PlayerPrefs.Save();

        var request = new RegisterPlayFabUserRequest
        {
            Email = email,
            Password = password,
            DisplayName = displayName,
            RequireBothUsernameAndEmail = false
        };

        PlayFabClientAPI.RegisterPlayFabUser(request,
            result => OnRegisterSuccess(result, rememberMe),
            OnRegisterErrorCallback);
    }

    /// <summary>
    /// Login as guest using device ID
    /// </summary>
    public void LoginAsGuest(bool rememberMe = true)
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

        PlayerPrefs.SetInt(REMEMBER_ME_KEY, rememberMe ? 1 : 0);
        PlayerPrefs.Save();

        PlayFabClientAPI.LoginWithCustomID(request,
            result => OnGuestLoginSuccess(result, rememberMe),
            OnLoginErrorCallback);
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
        hasAttemptedAutoLogin = false;

        // Clear ALL stored credentials
        PlayerPrefs.DeleteKey(REMEMBER_ME_KEY);
        PlayerPrefs.DeleteKey(REMEMBER_ME_ID_KEY);
        PlayerPrefs.DeleteKey(EMAIL_KEY);
        PlayerPrefs.DeleteKey(DISPLAY_NAME_KEY);
        PlayerPrefs.DeleteKey(AUTH_TYPE_KEY);
        PlayerPrefs.DeleteKey(GUEST_ID_KEY);
        PlayerPrefs.Save();

        Debug.Log("[PLAYFAB AUTH] Logged out successfully");
    }

    /// <summary>
    /// Send password reset email
    /// </summary>
    public void SendPasswordResetEmail(string email)
    {
        if (!IsValidEmail(email))
        {
            OnAuthenticationResult?.Invoke(false, "Invalid email format");
            return;
        }

        var request = new SendAccountRecoveryEmailRequest
        {
            Email = email,
            TitleId = PlayFabSettings.staticSettings.TitleId
        };

        Debug.Log($"[PLAYFAB AUTH] Sending password reset email to: {email}");
        PlayFabClientAPI.SendAccountRecoveryEmail(request, OnPasswordResetSuccess, OnPasswordResetError);
    }

    #endregion

    #region Auto-Login with RememberMe Pattern

    /// <summary>
    /// FIXED: Attempt auto-login using RememberMe pattern
    /// </summary>
    private void AttemptAutoLogin()
    {
        if (IsAuthenticated)
        {
            Debug.Log("[PLAYFAB AUTH] Already authenticated");
            return;
        }

        bool rememberMe = PlayerPrefs.GetInt(REMEMBER_ME_KEY, 0) == 1;

        if (!rememberMe)
        {
            Debug.Log("[PLAYFAB AUTH] RememberMe not enabled, skipping auto-login");
            return;
        }

        string authType = PlayerPrefs.GetString(AUTH_TYPE_KEY, "");
        string rememberMeId = PlayerPrefs.GetString(REMEMBER_ME_ID_KEY, "");

        Debug.Log($"[PLAYFAB AUTH] Attempting auto-login - AuthType: {authType}, HasRememberMeId: {!string.IsNullOrEmpty(rememberMeId)}");

        if (authType == "Email" && !string.IsNullOrEmpty(rememberMeId))
        {
            // Auto-login with RememberMe CustomID
            LoginWithRememberMe(rememberMeId);
        }
        else if (authType == "Guest")
        {
            // Auto-login as guest
            LoginAsGuest(true);
        }
        else
        {
            Debug.Log("[PLAYFAB AUTH] No valid auto-login credentials found");
        }
    }

    /// <summary>
    /// NEW: Login using RememberMe ID (secure alternative to storing password)
    /// </summary>
    private void LoginWithRememberMe(string rememberMeId)
    {
        Debug.Log("[PLAYFAB AUTH] Attempting RememberMe auto-login");

        var request = new LoginWithCustomIDRequest
        {
            CustomId = rememberMeId,
            CreateAccount = false, // Don't create if doesn't exist
            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetPlayerProfile = true,
                GetUserAccountInfo = true
            }
        };

        PlayFabClientAPI.LoginWithCustomID(request, OnRememberMeLoginSuccess, OnRememberMeLoginError);
    }

    #endregion

    #region Private Callbacks

    /// <summary>
    /// FIXED: Email login success with RememberMe linking
    /// </summary>
    private void OnEmailLoginSuccess(LoginResult result, bool rememberMe)
    {
        PlayFabId = result.PlayFabId;
        PlayerDisplayName = result.InfoResultPayload?.PlayerProfile?.DisplayName ??
                          "Player_" + PlayFabId.Substring(0, 8);
        Email = PlayerPrefs.GetString(EMAIL_KEY, "");
        IsAuthenticated = true;
        IsGuest = false;

        // FIXED: Link RememberMe ID if enabled
        if (rememberMe)
        {
            string rememberMeId = GetOrCreateRememberMeId();
            LinkRememberMeId(rememberMeId);

            PlayerPrefs.SetString(AUTH_TYPE_KEY, "Email");
            PlayerPrefs.SetString(DISPLAY_NAME_KEY, PlayerDisplayName);
            PlayerPrefs.Save();
        }

        SyncToPhoton();
        Debug.Log($"[PLAYFAB AUTH] Email login successful: {PlayerDisplayName} ({Email})");
        OnAuthenticationResult?.Invoke(true, "Login successful!");
        OnLoginSuccess?.Invoke();
    }

    /// <summary>
    /// FIXED: Registration success with RememberMe linking
    /// </summary>
    private void OnRegisterSuccess(RegisterPlayFabUserResult result, bool rememberMe)
    {
        PlayFabId = result.PlayFabId;
        PlayerDisplayName = PlayerPrefs.GetString(DISPLAY_NAME_KEY, "") ??
                          result.Username ??
                          "Player_" + PlayFabId.Substring(0, 8);
        Email = PlayerPrefs.GetString(EMAIL_KEY, "");
        IsAuthenticated = true;
        IsGuest = false;

        // FIXED: Link RememberMe ID if enabled
        if (rememberMe)
        {
            string rememberMeId = GetOrCreateRememberMeId();
            LinkRememberMeId(rememberMeId);

            PlayerPrefs.SetString(AUTH_TYPE_KEY, "Email");
            PlayerPrefs.Save();
        }

        SyncToPhoton();
        Debug.Log($"[PLAYFAB AUTH] Registration successful: {PlayerDisplayName} ({Email})");
        OnAuthenticationResult?.Invoke(true, "Account created successfully!");
        OnLoginSuccess?.Invoke();
    }

    /// <summary>
    /// FIXED: Guest login success
    /// </summary>
    private void OnGuestLoginSuccess(LoginResult result, bool rememberMe)
    {
        PlayFabId = result.PlayFabId;
        PlayerDisplayName = result.InfoResultPayload?.PlayerProfile?.DisplayName ??
                          "Guest_" + PlayFabId.Substring(0, 8);
        Email = null;
        IsAuthenticated = true;
        IsGuest = true;

        if (rememberMe)
        {
            PlayerPrefs.SetString(AUTH_TYPE_KEY, "Guest");
            PlayerPrefs.Save();
        }

        SyncToPhoton();
        Debug.Log($"[PLAYFAB AUTH] Guest login successful: {PlayerDisplayName}");
        OnAuthenticationResult?.Invoke(true, "Guest login successful!");
        OnLoginSuccess?.Invoke();
    }

    /// <summary>
    /// NEW: RememberMe login success
    /// </summary>
    private void OnRememberMeLoginSuccess(LoginResult result)
    {
        PlayFabId = result.PlayFabId;
        PlayerDisplayName = result.InfoResultPayload?.PlayerProfile?.DisplayName ??
                          PlayerPrefs.GetString(DISPLAY_NAME_KEY, "Player");
        Email = PlayerPrefs.GetString(EMAIL_KEY, "");
        IsAuthenticated = true;
        IsGuest = false;

        SyncToPhoton();
        Debug.Log($"[PLAYFAB AUTH] Auto-login successful: {PlayerDisplayName}");
        OnLoginSuccess?.Invoke();
    }

    /// <summary>
    /// NEW: RememberMe login error - clear stored data and require manual login
    /// </summary>
    private void OnRememberMeLoginError(PlayFabError error)
    {
        Debug.LogWarning($"[PLAYFAB AUTH] Auto-login failed: {error.ErrorMessage}");

        // Clear RememberMe data if it's invalid
        PlayerPrefs.DeleteKey(REMEMBER_ME_ID_KEY);
        PlayerPrefs.DeleteKey(AUTH_TYPE_KEY);
        PlayerPrefs.Save();

        // Don't show error to user for auto-login failures
        // Just silently fail and require manual login
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

        // FIXED: Clear stored credentials on registration failure
        PlayerPrefs.DeleteKey(EMAIL_KEY);
        PlayerPrefs.DeleteKey(DISPLAY_NAME_KEY);
        PlayerPrefs.Save();

        OnAuthenticationResult?.Invoke(false, errorMessage);
        OnLoginError?.Invoke(errorMessage);
    }

    private void OnPasswordResetSuccess(SendAccountRecoveryEmailResult result)
    {
        Debug.Log("[PLAYFAB AUTH] Password reset email sent successfully");
        OnAuthenticationResult?.Invoke(true, "Password reset email sent!");
    }

    private void OnPasswordResetError(PlayFabError error)
    {
        string errorMessage = GetPasswordResetErrorMessage(error);
        Debug.LogWarning($"[PLAYFAB AUTH] Password reset failed: {errorMessage}");
        OnAuthenticationResult?.Invoke(false, errorMessage);
    }

    #endregion

    #region RememberMe ID Management

    /// <summary>
    /// NEW: Get or create RememberMe ID (used instead of storing password)
    /// </summary>
    private string GetOrCreateRememberMeId()
    {
        string rememberMeId = PlayerPrefs.GetString(REMEMBER_ME_ID_KEY, "");

        if (string.IsNullOrEmpty(rememberMeId))
        {
            rememberMeId = "RM_" + System.Guid.NewGuid().ToString();
            PlayerPrefs.SetString(REMEMBER_ME_ID_KEY, rememberMeId);
            PlayerPrefs.Save();
            Debug.Log($"[PLAYFAB AUTH] Created new RememberMe ID");
        }

        return rememberMeId;
    }

    /// <summary>
    /// NEW: Link RememberMe CustomID to the PlayFab account
    /// </summary>
    private void LinkRememberMeId(string rememberMeId)
    {
        var request = new LinkCustomIDRequest
        {
            CustomId = rememberMeId,
            ForceLink = false // Don't overwrite existing links
        };

        PlayFabClientAPI.LinkCustomID(request,
            result => {
                Debug.Log("[PLAYFAB AUTH] RememberMe ID linked successfully");
            },
            error => {
                // If already linked, that's fine
                if (error.Error == PlayFabErrorCode.LinkedAccountAlreadyClaimed)
                {
                    Debug.Log("[PLAYFAB AUTH] RememberMe ID already linked");
                }
                else
                {
                    Debug.LogWarning($"[PLAYFAB AUTH] Failed to link RememberMe ID: {error.ErrorMessage}");
                }
            });
    }

    #endregion

    #region Helper Methods

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

    private bool IsValidPassword(string password)
    {
        return !string.IsNullOrEmpty(password) && password.Length >= 6;
    }

    private void SyncToPhoton()
    {
        if (!string.IsNullOrEmpty(PlayerDisplayName))
        {
            PhotonNetwork.NickName = PlayerDisplayName;
            Debug.Log($"[PLAYFAB AUTH] Synced to Photon: {PlayerDisplayName}");
        }
    }

    private string GetErrorMessage(PlayFabError error)
    {
        switch (error.Error)
        {
            case PlayFabErrorCode.InvalidEmailAddress:
                return "Please enter a valid email address";
            case PlayFabErrorCode.InvalidUsernameOrPassword:
                return "Invalid email or password";
            case PlayFabErrorCode.EmailAddressNotAvailable:
                return "This email is already registered";
            case PlayFabErrorCode.AccountNotFound:
                return "No account found with this email";
            case PlayFabErrorCode.InvalidPassword:
                return "Password is incorrect";
            case PlayFabErrorCode.UsernameNotAvailable:
                return "Display name is already taken";
            case PlayFabErrorCode.AccountBanned:
                return "This account has been suspended";
            case PlayFabErrorCode.AccountDeleted:
                return "This account has been deleted";
            default:
                return "Authentication failed. Please try again";
        }
    }

    private string GetPasswordResetErrorMessage(PlayFabError error)
    {
        switch (error.Error)
        {
            case PlayFabErrorCode.InvalidEmailAddress:
            case PlayFabErrorCode.InvalidParams:
                return "Please enter a valid email address";
            case PlayFabErrorCode.EmailAddressNotAvailable:
            case PlayFabErrorCode.AccountNotFound:
                return "No account found with this email";
            default:
                return $"Failed to send password reset email: {error.ErrorMessage}";
        }
    }

    #endregion

    #region Debug Methods

    [ContextMenu("Force Auto-Login Check")]
    public void ForceAutoLoginCheck()
    {
        hasAttemptedAutoLogin = false;
        AttemptAutoLogin();
    }

    [ContextMenu("Clear All Saved Data")]
    public void ClearAllSavedData()
    {
        Logout();
        Debug.Log("[PLAYFAB AUTH] All saved data cleared");
    }

    [ContextMenu("Debug Saved Credentials")]
    public void DebugSavedCredentials()
    {
        Debug.Log("=== SAVED CREDENTIALS DEBUG ===");
        Debug.Log($"RememberMe: {PlayerPrefs.GetInt(REMEMBER_ME_KEY, 0)}");
        Debug.Log($"RememberMeId: {PlayerPrefs.GetString(REMEMBER_ME_ID_KEY, "None")}");
        Debug.Log($"Email: {PlayerPrefs.GetString(EMAIL_KEY, "None")}");
        Debug.Log($"DisplayName: {PlayerPrefs.GetString(DISPLAY_NAME_KEY, "None")}");
        Debug.Log($"AuthType: {PlayerPrefs.GetString(AUTH_TYPE_KEY, "None")}");
        Debug.Log($"GuestId: {PlayerPrefs.GetString(GUEST_ID_KEY, "None")}");
    }

    #endregion
}