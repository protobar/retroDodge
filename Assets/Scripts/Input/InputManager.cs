using UnityEngine;
using System.Collections.Generic;

// ==================== INPUT MANAGER ====================
public class InputManager : MonoBehaviour
{
    private static InputManager instance;
    public static InputManager Instance => instance;

    [Header("Input Settings")]
    public float inputBufferTime = 0.1f;
    public bool enableInputBuffer = true;

    // PC Controls
    [Header("PC Controls")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode dodgeKey = KeyCode.LeftShift;
    public KeyCode throwKey = KeyCode.Mouse0;
    public KeyCode catchKey = KeyCode.Mouse1;
    public KeyCode ultimateKey = KeyCode.Q;

    // Input buffer
    private Dictionary<string, float> inputBuffer = new Dictionary<string, float>();

    // Mobile controls
    [Header("Mobile Controls")]
    public GameObject mobileControlsUI;
    public MobileControlButton jumpButton;
    public MobileControlButton dodgeButton;
    public MobileControlButton throwButton;
    public MobileControlButton catchButton;
    public MobileControlButton ultimateButton;
    public MobileJoystick movementJoystick;

    private bool isMobile = false;

    // Registered players
    private List<CharacterBase> registeredPlayers = new List<CharacterBase>();

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

#if UNITY_ANDROID || UNITY_IOS
            isMobile = true;
            InitializeMobileControls();
#else
            isMobile = false;
            if (mobileControlsUI != null)
                mobileControlsUI.SetActive(false);
#endif
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        // Clean up expired buffered inputs
        CleanupInputBuffer();
    }

    public void RegisterPlayer(CharacterBase player)
    {
        if (!registeredPlayers.Contains(player))
        {
            registeredPlayers.Add(player);
        }
    }

    public void UnregisterPlayer(CharacterBase player)
    {
        if (registeredPlayers.Contains(player))
        {
            registeredPlayers.Remove(player);
        }
    }

    // Static input methods for easy access
    public static float GetHorizontal()
    {
        if (Instance.isMobile && Instance.movementJoystick != null)
        {
            return Instance.movementJoystick.GetHorizontalInput();
        }
        return Input.GetAxis("Horizontal");
    }

    public static float GetVertical()
    {
        if (Instance.isMobile && Instance.movementJoystick != null)
        {
            return Instance.movementJoystick.GetVerticalInput();
        }
        return Input.GetAxis("Vertical");
    }

    public static bool GetJump()
    {
        if (Instance.isMobile && Instance.jumpButton != null)
        {
            return Instance.jumpButton.WasPressed();
        }
        return Instance.GetBufferedInput("Jump", () => Input.GetKeyDown(Instance.jumpKey));
    }

    public static bool GetDodge()
    {
        if (Instance.isMobile && Instance.dodgeButton != null)
        {
            return Instance.dodgeButton.WasPressed();
        }
        return Instance.GetBufferedInput("Dodge", () => Input.GetKeyDown(Instance.dodgeKey));
    }

    public static bool GetThrow()
    {
        if (Instance.isMobile && Instance.throwButton != null)
        {
            return Instance.throwButton.WasPressed();
        }
        return Instance.GetBufferedInput("Throw", () => Input.GetMouseButtonDown(0));
    }

    public static bool GetThrowHeld()
    {
        if (Instance.isMobile && Instance.throwButton != null)
        {
            return Instance.throwButton.IsHeld();
        }
        return Input.GetMouseButton(0);
    }

    public static bool GetCatch()
    {
        if (Instance.isMobile && Instance.catchButton != null)
        {
            return Instance.catchButton.WasPressed();
        }
        return Instance.GetBufferedInput("Catch", () => Input.GetMouseButtonDown(1));
    }

    public static bool GetUltimate()
    {
        if (Instance.isMobile && Instance.ultimateButton != null)
        {
            return Instance.ultimateButton.WasPressed();
        }
        return Instance.GetBufferedInput("Ultimate", () => Input.GetKeyDown(Instance.ultimateKey));
    }

    private bool GetBufferedInput(string inputName, System.Func<bool> inputCheck)
    {
        if (!enableInputBuffer)
        {
            return inputCheck();
        }

        if (inputCheck())
        {
            inputBuffer[inputName] = Time.time;
            return true;
        }

        if (inputBuffer.ContainsKey(inputName))
        {
            if (Time.time - inputBuffer[inputName] <= inputBufferTime)
            {
                inputBuffer.Remove(inputName);
                return true;
            }
        }

        return false;
    }

    private void CleanupInputBuffer()
    {
        List<string> keysToRemove = new List<string>();

        foreach (var kvp in inputBuffer)
        {
            if (Time.time - kvp.Value > inputBufferTime)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (string key in keysToRemove)
        {
            inputBuffer.Remove(key);
        }
    }

    void InitializeMobileControls()
    {
        if (mobileControlsUI != null)
        {
            mobileControlsUI.SetActive(true);

            // Initialize mobile control components
            if (jumpButton == null)
                jumpButton = mobileControlsUI.GetComponentInChildren<MobileControlButton>();
        }
    }

    // Mobile input methods (called by UI buttons)
    public void OnMobileJump()
    {
        if (enableInputBuffer)
            inputBuffer["Jump"] = Time.time;
    }

    public void OnMobileDodge()
    {
        if (enableInputBuffer)
            inputBuffer["Dodge"] = Time.time;
    }

    public void OnMobileThrow()
    {
        if (enableInputBuffer)
            inputBuffer["Throw"] = Time.time;
    }

    public void OnMobileCatch()
    {
        if (enableInputBuffer)
            inputBuffer["Catch"] = Time.time;
    }

    public void OnMobileUltimate()
    {
        if (enableInputBuffer)
            inputBuffer["Ultimate"] = Time.time;
    }

    // Settings
    public void SetInputBufferEnabled(bool enabled)
    {
        enableInputBuffer = enabled;
    }

    public void SetInputBufferTime(float time)
    {
        inputBufferTime = Mathf.Clamp(time, 0.05f, 0.5f);
    }
}