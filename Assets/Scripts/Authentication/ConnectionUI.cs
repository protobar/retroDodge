using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Connection UI Manager - Handles all UI panels and user interactions for authentication
/// Manages panel switching, input validation, and error display
/// </summary>
public class ConnectionUI : MonoBehaviour
{
    [Header("Main Panels")]
    [SerializeField] private GameObject splashPanel;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject signInPanel;
    [SerializeField] private GameObject signUpPanel;
    [SerializeField] private GameObject forgotPasswordPanel;
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private GameObject errorPanel;

    [Header("Splash Panel Elements")]
    [SerializeField] private Button splashClickArea;

    [Header("Main Panel Elements")]
    [SerializeField] private Button signInButton;
    [SerializeField] private Button signUpButton;
    [SerializeField] private Button guestButton;

    [Header("Sign In Panel Elements")]
    [SerializeField] private TMP_InputField signInEmailInput;
    [SerializeField] private TMP_InputField signInPasswordInput;
    [SerializeField] private Button signInPasswordToggleButton;
    [SerializeField] private Button signInForgotPasswordButton;
    [SerializeField] private Button signInLoginButton;
    [SerializeField] private Button signInBackButton;
    [SerializeField] private TMP_Text signInErrorText;

    [Header("Sign Up Panel Elements")]
    [SerializeField] private TMP_InputField signUpDisplayNameInput;
    [SerializeField] private TMP_InputField signUpEmailInput;
    [SerializeField] private TMP_InputField signUpPasswordInput;
    [SerializeField] private Button signUpPasswordToggleButton;
    [SerializeField] private TMP_InputField signUpConfirmPasswordInput;
    [SerializeField] private Button signUpConfirmPasswordToggleButton;
    [SerializeField] private Button signUpRegisterButton;
    [SerializeField] private Button signUpBackButton;
    [SerializeField] private TMP_Text signUpErrorText;

    [Header("Loading Panel Elements")]
    [SerializeField] private TMP_Text loadingStatusText;
    [SerializeField] private GameObject loadingSpinner;

    [Header("Forgot Password Panel Elements")]
    [SerializeField] private TMP_InputField forgotPasswordEmailInput;
    [SerializeField] private Button forgotPasswordSendButton;
    [SerializeField] private Button forgotPasswordBackButton;
    [SerializeField] private TMP_Text forgotPasswordErrorText;
    [SerializeField] private TMP_Text forgotPasswordSuccessText;

    [Header("Error Panel Elements")]
    [SerializeField] private TMP_Text errorMessageText;
    [SerializeField] private Button errorCloseButton;

    [Header("Settings")]
    [SerializeField] private float panelTransitionDuration = 0.3f;
    [SerializeField] private bool debugMode = true;

    // State management
    private GameObject currentActivePanel;
    private Coroutine panelTransitionCoroutine;
    private bool splashScreenActive = true;
    
    // Password toggle states
    private bool signInPasswordVisible = false;
    private bool signUpPasswordVisible = false;
    private bool signUpConfirmPasswordVisible = false;

    void Start()
    {
        SetupUI();
        SetupButtonListeners();
        ShowSplashScreen();
    }

    #region UI Setup

    private void SetupUI()
    {
        // Hide all panels initially
        HideAllPanels();

        // Setup input field validation
        SetupInputFieldValidation();

        // Setup loading spinner animation
        //SetupLoadingSpinner();

        Debug.Log("[CONNECTION UI] UI setup complete");
    }

    private void SetupButtonListeners()
    {
        // Splash panel button
        if (splashClickArea != null)
            splashClickArea.onClick.AddListener(() => OnSplashClicked());

        // Main panel buttons
        if (signInButton != null)
            signInButton.onClick.AddListener(() => ShowSignInPanel());
        
        if (signUpButton != null)
            signUpButton.onClick.AddListener(() => ShowSignUpPanel());
        
        if (guestButton != null)
            guestButton.onClick.AddListener(() => LoginAsGuest());

        // Sign in panel buttons
        if (signInPasswordToggleButton != null)
            signInPasswordToggleButton.onClick.AddListener(() => ToggleSignInPasswordVisibility());
        
        if (signInForgotPasswordButton != null)
            signInForgotPasswordButton.onClick.AddListener(() => ShowForgotPasswordPanel());
        
        if (signInLoginButton != null)
            signInLoginButton.onClick.AddListener(() => AttemptSignIn());
        
        if (signInBackButton != null)
            signInBackButton.onClick.AddListener(() => ShowMainPanel());

        // Sign up panel buttons
        if (signUpPasswordToggleButton != null)
            signUpPasswordToggleButton.onClick.AddListener(() => ToggleSignUpPasswordVisibility());
        
        if (signUpConfirmPasswordToggleButton != null)
            signUpConfirmPasswordToggleButton.onClick.AddListener(() => ToggleSignUpConfirmPasswordVisibility());
        
        if (signUpRegisterButton != null)
            signUpRegisterButton.onClick.AddListener(() => AttemptSignUp());
        
        if (signUpBackButton != null)
            signUpBackButton.onClick.AddListener(() => ShowMainPanel());

        // Forgot password panel buttons
        if (forgotPasswordSendButton != null)
            forgotPasswordSendButton.onClick.AddListener(() => AttemptPasswordReset());
        
        if (forgotPasswordBackButton != null)
            forgotPasswordBackButton.onClick.AddListener(() => ShowSignInPanel());

        // Error panel buttons
        if (errorCloseButton != null)
            errorCloseButton.onClick.AddListener(() => HideErrorPanel());

        Debug.Log("[CONNECTION UI] Button listeners setup complete");
    }

    private void SetupInputFieldValidation()
    {
        // Sign in email validation
        if (signInEmailInput != null)
        {
            signInEmailInput.onEndEdit.AddListener((string value) => ValidateSignInInputs());
        }

        // Sign in password validation
        if (signInPasswordInput != null)
        {
            signInPasswordInput.onEndEdit.AddListener((string value) => ValidateSignInInputs());
        }

        // Sign up validation
        if (signUpDisplayNameInput != null)
        {
            signUpDisplayNameInput.onEndEdit.AddListener((string value) => ValidateSignUpInputs());
        }

        if (signUpEmailInput != null)
        {
            signUpEmailInput.onEndEdit.AddListener((string value) => ValidateSignUpInputs());
        }

        if (signUpPasswordInput != null)
        {
            signUpPasswordInput.onEndEdit.AddListener((string value) => ValidateSignUpInputs());
        }

        if (signUpConfirmPasswordInput != null)
        {
            signUpConfirmPasswordInput.onEndEdit.AddListener((string value) => ValidateSignUpInputs());
        }
    }

    /*private void SetupLoadingSpinner()
    {
        if (loadingSpinner != null)
        {
            StartCoroutine(AnimateLoadingSpinner());
        }
    }*/

    #endregion

    #region Panel Management

    private void HideAllPanels()
    {
        if (splashPanel != null) splashPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(false);
        if (signInPanel != null) signInPanel.SetActive(false);
        if (signUpPanel != null) signUpPanel.SetActive(false);
        if (forgotPasswordPanel != null) forgotPasswordPanel.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(false);
        if (errorPanel != null) errorPanel.SetActive(false);
    }

    public void ShowSplashScreen()
    {
        if (debugMode) Debug.Log("[CONNECTION UI] Showing splash screen");
        
        splashScreenActive = true;
        SwitchToPanel(splashPanel);
    }

    private void OnSplashClicked()
    {
        if (!splashScreenActive) return;
        
        if (debugMode) Debug.Log("[CONNECTION UI] Splash screen clicked");
        
        splashScreenActive = false;
        
        // Transition to main panel
        ShowMainPanel();
    }

    public void ShowMainPanel()
    {
        if (debugMode) Debug.Log("[CONNECTION UI] Showing main panel");
        SwitchToPanel(mainPanel);
        ClearAllInputs();
        ClearAllErrors();
    }

    public void ShowSignInPanel()
    {
        if (debugMode) Debug.Log("[CONNECTION UI] Showing sign in panel");
        SwitchToPanel(signInPanel);
        ClearSignInErrors();
        ResetPasswordVisibilityStates();
    }

    public void ShowSignUpPanel()
    {
        if (debugMode) Debug.Log("[CONNECTION UI] Showing sign up panel");
        SwitchToPanel(signUpPanel);
        ClearSignUpErrors();
        ResetPasswordVisibilityStates();
    }

    public void ShowForgotPasswordPanel()
    {
        if (debugMode) Debug.Log("[CONNECTION UI] Showing forgot password panel");
        SwitchToPanel(forgotPasswordPanel);
        ClearForgotPasswordErrors();
        ClearForgotPasswordSuccess();
    }

    public void ShowLoadingPanel(string statusMessage = "Loading...")
    {
        if (debugMode) Debug.Log($"[CONNECTION UI] Showing loading panel: {statusMessage}");
        SwitchToPanel(loadingPanel);
        
        if (loadingStatusText != null)
        {
            loadingStatusText.text = statusMessage;
        }
    }

    public void HideLoadingPanel()
    {
        if (debugMode) Debug.Log("[CONNECTION UI] Hiding loading panel");
        
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }
    }

    public void ShowErrorPanel(string errorMessage)
    {
        if (debugMode) Debug.Log($"[CONNECTION UI] Showing error panel: {errorMessage}");
        SwitchToPanel(errorPanel);
        
        if (errorMessageText != null)
        {
            errorMessageText.text = errorMessage;
        }
    }

    private void SwitchToPanel(GameObject targetPanel)
    {
        if (targetPanel == null) return;

        if (panelTransitionCoroutine != null)
        {
            StopCoroutine(panelTransitionCoroutine);
        }

        panelTransitionCoroutine = StartCoroutine(TransitionToPanel(targetPanel));
    }

    private IEnumerator TransitionToPanel(GameObject targetPanel)
    {
        // Hide current panel
        if (currentActivePanel != null)
        {
            currentActivePanel.SetActive(false);
        }

        // Show target panel
        targetPanel.SetActive(true);
        currentActivePanel = targetPanel;

        yield return new WaitForSeconds(panelTransitionDuration);
        panelTransitionCoroutine = null;
    }

    #endregion

    #region Password Toggle Methods

    private void ToggleSignInPasswordVisibility()
    {
        signInPasswordVisible = !signInPasswordVisible;
        UpdatePasswordFieldVisibility(signInPasswordInput, signInPasswordVisible);
        
        if (debugMode) Debug.Log($"[CONNECTION UI] Sign in password visibility toggled: {signInPasswordVisible}");
    }

    private void ToggleSignUpPasswordVisibility()
    {
        signUpPasswordVisible = !signUpPasswordVisible;
        UpdatePasswordFieldVisibility(signUpPasswordInput, signUpPasswordVisible);
        
        if (debugMode) Debug.Log($"[CONNECTION UI] Sign up password visibility toggled: {signUpPasswordVisible}");
    }

    private void ToggleSignUpConfirmPasswordVisibility()
    {
        signUpConfirmPasswordVisible = !signUpConfirmPasswordVisible;
        UpdatePasswordFieldVisibility(signUpConfirmPasswordInput, signUpConfirmPasswordVisible);
        
        if (debugMode) Debug.Log($"[CONNECTION UI] Sign up confirm password visibility toggled: {signUpConfirmPasswordVisible}");
    }

    private void UpdatePasswordFieldVisibility(TMP_InputField passwordField, bool isVisible)
    {
        if (passwordField == null) return;

        passwordField.contentType = isVisible ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
        passwordField.ForceLabelUpdate();
    }

    private void ResetPasswordVisibilityStates()
    {
        signInPasswordVisible = false;
        signUpPasswordVisible = false;
        signUpConfirmPasswordVisible = false;
        
        // Reset all password fields to hidden
        UpdatePasswordFieldVisibility(signInPasswordInput, false);
        UpdatePasswordFieldVisibility(signUpPasswordInput, false);
        UpdatePasswordFieldVisibility(signUpConfirmPasswordInput, false);
        
        if (debugMode) Debug.Log("[CONNECTION UI] Password visibility states reset");
    }

    #endregion

    #region Authentication Methods

    private void AttemptSignIn()
    {
        string email = signInEmailInput?.text?.Trim() ?? "";
        string password = signInPasswordInput?.text ?? "";

        if (debugMode) Debug.Log($"[CONNECTION UI] Attempting sign in - Email: '{email}', Password length: {password.Length}");

        // Clear previous errors
        ClearSignInErrors();

        if (ValidateSignInInputs())
        {
            if (debugMode) Debug.Log("[CONNECTION UI] Validation passed, proceeding with sign in");
            ShowLoadingPanel("Signing in...");
            PlayFabAuthManager.Instance.LoginWithEmail(email, password);
        }
        else
        {
            if (debugMode) Debug.Log("[CONNECTION UI] Validation failed, hiding loading panel");
            // Ensure loading panel is hidden if validation fails
            HideLoadingPanel();
        }
    }

    private void AttemptSignUp()
    {
        string displayName = signUpDisplayNameInput?.text?.Trim() ?? "";
        string email = signUpEmailInput?.text?.Trim() ?? "";
        string password = signUpPasswordInput?.text ?? "";
        string confirmPassword = signUpConfirmPasswordInput?.text ?? "";

        if (debugMode) Debug.Log($"[CONNECTION UI] Attempting sign up - DisplayName: '{displayName}', Email: '{email}', Password length: {password.Length}, Confirm length: {confirmPassword.Length}");

        // Clear previous errors
        ClearSignUpErrors();

        if (ValidateSignUpInputs())
        {
            if (debugMode) Debug.Log("[CONNECTION UI] Validation passed, proceeding with sign up");
            ShowLoadingPanel("Creating account...");
            PlayFabAuthManager.Instance.RegisterAccount(displayName, email, password, confirmPassword);
        }
        else
        {
            if (debugMode) Debug.Log("[CONNECTION UI] Validation failed, hiding loading panel");
            // Ensure loading panel is hidden if validation fails
            HideLoadingPanel();
        }
    }

    private void LoginAsGuest()
    {
        ShowLoadingPanel("Logging in as guest...");
        PlayFabAuthManager.Instance.LoginAsGuest();
    }

    private void AttemptPasswordReset()
    {
        string email = forgotPasswordEmailInput?.text?.Trim() ?? "";

        if (debugMode) Debug.Log($"[CONNECTION UI] Attempting password reset - Email: '{email}'");

        // Clear previous errors and success messages
        ClearForgotPasswordErrors();
        ClearForgotPasswordSuccess();

        if (string.IsNullOrEmpty(email))
        {
            ShowForgotPasswordError("Email is required");
            return;
        }

        if (!IsValidEmail(email))
        {
            ShowForgotPasswordError("Invalid email format");
            return;
        }

        if (debugMode) Debug.Log("[CONNECTION UI] Validation passed, proceeding with password reset");
        ShowLoadingPanel("Sending password reset email...");
        PlayFabAuthManager.Instance.SendPasswordResetEmail(email);
    }

    #endregion

    #region Input Validation

    private bool ValidateSignInInputs()
    {
        bool isValid = true;
        string email = signInEmailInput?.text?.Trim() ?? "";
        string password = signInPasswordInput?.text ?? "";

        if (debugMode) Debug.Log($"[CONNECTION UI] Validating sign in - Email: '{email}', Password length: {password.Length}");

        if (string.IsNullOrEmpty(email))
        {
            ShowSignInError("Email is required");
            isValid = false;
        }
        else if (!IsValidEmail(email))
        {
            ShowSignInError("Invalid email format");
            isValid = false;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowSignInError("Password is required");
            isValid = false;
        }
        else if (password.Length < 6)
        {
            ShowSignInError("Password must be at least 6 characters");
            isValid = false;
        }

        if (debugMode) Debug.Log($"[CONNECTION UI] Sign in validation result: {isValid}");
        return isValid;
    }

    private bool ValidateSignUpInputs()
    {
        bool isValid = true;
        string displayName = signUpDisplayNameInput?.text?.Trim() ?? "";
        string email = signUpEmailInput?.text?.Trim() ?? "";
        string password = signUpPasswordInput?.text ?? "";
        string confirmPassword = signUpConfirmPasswordInput?.text ?? "";

        if (debugMode) Debug.Log($"[CONNECTION UI] Validating sign up - DisplayName: '{displayName}', Email: '{email}', Password length: {password.Length}, Confirm length: {confirmPassword.Length}");

        if (string.IsNullOrEmpty(displayName))
        {
            ShowSignUpError("Display name is required");
            isValid = false;
        }
        else if (displayName.Length < 2)
        {
            ShowSignUpError("Display name must be at least 2 characters");
            isValid = false;
        }

        if (string.IsNullOrEmpty(email))
        {
            ShowSignUpError("Email is required");
            isValid = false;
        }
        else if (!IsValidEmail(email))
        {
            ShowSignUpError("Invalid email format");
            isValid = false;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowSignUpError("Password is required");
            isValid = false;
        }
        else if (password.Length < 6)
        {
            ShowSignUpError("Password must be at least 6 characters");
            isValid = false;
        }

        if (string.IsNullOrEmpty(confirmPassword))
        {
            ShowSignUpError("Please confirm your password");
            isValid = false;
        }
        else if (password != confirmPassword)
        {
            ShowSignUpError("Passwords do not match");
            isValid = false;
        }

        if (debugMode) Debug.Log($"[CONNECTION UI] Sign up validation result: {isValid}");
        return isValid;
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

    #endregion

    #region Error Display

    public void ShowError(string errorMessage)
    {
        ShowErrorPanel(errorMessage);
    }

    private void ShowSignInError(string errorMessage)
    {
        if (signInErrorText != null)
        {
            signInErrorText.text = errorMessage;
            signInErrorText.gameObject.SetActive(true);
        }
    }

    private void ShowSignUpError(string errorMessage)
    {
        if (signUpErrorText != null)
        {
            signUpErrorText.text = errorMessage;
            signUpErrorText.gameObject.SetActive(true);
        }
    }

    private void ClearAllErrors()
    {
        ClearSignInErrors();
        ClearSignUpErrors();
        ClearForgotPasswordErrors();
        ClearForgotPasswordSuccess();
        HideErrorPanel();
    }

    private void ClearSignInErrors()
    {
        if (signInErrorText != null)
        {
            signInErrorText.text = "";
            signInErrorText.gameObject.SetActive(false);
        }
    }

    private void ClearSignUpErrors()
    {
        if (signUpErrorText != null)
        {
            signUpErrorText.text = "";
            signUpErrorText.gameObject.SetActive(false);
        }
    }

    private void ShowForgotPasswordError(string errorMessage)
    {
        if (forgotPasswordErrorText != null)
        {
            forgotPasswordErrorText.text = errorMessage;
            forgotPasswordErrorText.gameObject.SetActive(true);
        }
    }

    private void ShowForgotPasswordSuccess(string successMessage)
    {
        if (forgotPasswordSuccessText != null)
        {
            forgotPasswordSuccessText.text = successMessage;
            forgotPasswordSuccessText.gameObject.SetActive(true);
        }
    }

    private void ClearForgotPasswordErrors()
    {
        if (forgotPasswordErrorText != null)
        {
            forgotPasswordErrorText.text = "";
            forgotPasswordErrorText.gameObject.SetActive(false);
        }
    }

    private void ClearForgotPasswordSuccess()
    {
        if (forgotPasswordSuccessText != null)
        {
            forgotPasswordSuccessText.text = "";
            forgotPasswordSuccessText.gameObject.SetActive(false);
        }
    }

    private void HideErrorPanel()
    {
        if (errorPanel != null)
        {
            errorPanel.SetActive(false);
        }
    }

    #endregion

    #region Input Clearing

    private void ClearAllInputs()
    {
        ClearSignInInputs();
        ClearSignUpInputs();
        ClearForgotPasswordInputs();
    }

    private void ClearSignInInputs()
    {
        if (signInEmailInput != null) signInEmailInput.text = "";
        if (signInPasswordInput != null) signInPasswordInput.text = "";
    }

    private void ClearSignUpInputs()
    {
        if (signUpDisplayNameInput != null) signUpDisplayNameInput.text = "";
        if (signUpEmailInput != null) signUpEmailInput.text = "";
        if (signUpPasswordInput != null) signUpPasswordInput.text = "";
        if (signUpConfirmPasswordInput != null) signUpConfirmPasswordInput.text = "";
    }

    private void ClearForgotPasswordInputs()
    {
        if (forgotPasswordEmailInput != null) forgotPasswordEmailInput.text = "";
    }

    #endregion

    #region Loading Animation

    /*private IEnumerator AnimateLoadingSpinner()
    {
        while (true)
        {
            if (loadingSpinner != null && loadingSpinner.activeInHierarchy)
            {
                loadingSpinner.transform.Rotate(0, 0, -90f * Time.deltaTime);
            }
            yield return null;
        }
    }*/

    #endregion

    #region Public Methods

    /// <summary>
    /// Update loading status text
    /// </summary>
    public void UpdateLoadingStatus(string status)
    {
        if (loadingStatusText != null)
        {
            loadingStatusText.text = status;
        }
    }

    /// <summary>
    /// Show error message in current panel
    /// </summary>
    public void ShowErrorMessage(string message)
    {
        if (currentActivePanel == signInPanel)
        {
            ShowSignInError(message);
        }
        else if (currentActivePanel == signUpPanel)
        {
            ShowSignUpError(message);
        }
        else if (currentActivePanel == forgotPasswordPanel)
        {
            ShowForgotPasswordError(message);
        }
        else
        {
            ShowErrorPanel(message);
        }
    }

    public void ShowSuccessMessage(string message)
    {
        if (currentActivePanel == forgotPasswordPanel)
        {
            ShowForgotPasswordSuccess(message);
        }
        else
        {
            ShowErrorPanel(message); // Fallback to error panel for success messages
        }
    }

    #endregion
}
