using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// PS5 DualSense Haptic Feedback Manager
/// Provides vibration/haptic feedback for game events
/// Requires Unity Input System package
/// </summary>
public class HapticFeedbackManager : MonoBehaviour
{
    [Header("Haptic Settings")]
    [SerializeField] private bool enableHaptics = true;
    [SerializeField] [Range(0f, 1f)] private float lightVibration = 0.15f;
    [SerializeField] [Range(0f, 1f)] private float mediumVibration = 0.4f;
    [SerializeField] [Range(0f, 1f)] private float strongVibration = 0.7f;
    
    [Header("Vibration Durations")]
    [SerializeField] private float lightDuration = 0.1f;
    [SerializeField] private float mediumDuration = 0.2f;
    [SerializeField] private float strongDuration = 0.3f;
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    
    private static HapticFeedbackManager _instance;
    public static HapticFeedbackManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<HapticFeedbackManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("HapticFeedbackManager");
                    _instance = go.AddComponent<HapticFeedbackManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }
    
#if ENABLE_INPUT_SYSTEM
    private Gamepad currentGamepad;
    private bool isPS5Controller = false;
    private Coroutine activeVibrationCoroutine;
#endif
    
    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        InitializeHaptics();
    }
    
    void InitializeHaptics()
    {
#if ENABLE_INPUT_SYSTEM
        if (!enableHaptics)
        {
            if (debugMode) Debug.Log("[HAPTICS] Haptics disabled");
            return;
        }
        
        // Check for gamepad
        if (Gamepad.current != null)
        {
            currentGamepad = Gamepad.current;
            isPS5Controller = IsPS5Controller(currentGamepad);
            
            if (debugMode)
            {
                Debug.Log($"[HAPTICS] Gamepad detected: {currentGamepad.name}");
                Debug.Log($"[HAPTICS] Is PS5 Controller: {isPS5Controller}");
            }
        }
        else
        {
            if (debugMode) Debug.Log("[HAPTICS] No gamepad detected");
        }
#else
        if (debugMode) Debug.LogWarning("[HAPTICS] Input System not enabled. Install Unity Input System package for haptic support.");
#endif
    }
    
    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        // Update gamepad reference in case controller connects/disconnects
        if (Gamepad.current != null && Gamepad.current != currentGamepad)
        {
            currentGamepad = Gamepad.current;
            isPS5Controller = IsPS5Controller(currentGamepad);
            
            if (debugMode)
                Debug.Log($"[HAPTICS] Gamepad updated: {currentGamepad.name}, Is PS5: {isPS5Controller}");
        }
#endif
    }
    
#if ENABLE_INPUT_SYSTEM
    /// <summary>
    /// Check if the gamepad is a PS5 DualSense controller
    /// </summary>
    bool IsPS5Controller(Gamepad gamepad)
    {
        if (gamepad == null) return false;
        
        string name = gamepad.name.ToLower();
        // Check for PS5 DualSense controller names
        // Common names: "DualSense", "PS5", "Wireless Controller" (when connected via USB)
        return name.Contains("dualsense") || 
               name.Contains("ps5") || 
               (name.Contains("wireless controller") && name.Contains("054c")); // Sony vendor ID
    }
    
    /// <summary>
    /// Trigger light vibration (jumps, dashes)
    /// </summary>
    public void VibrateLight(float customDuration = -1f)
    {
        if (!enableHaptics || currentGamepad == null) return;
        
        float duration = customDuration > 0 ? customDuration : lightDuration;
        StartVibration(lightVibration, lightVibration, duration);
        
        if (debugMode) Debug.Log($"[HAPTICS] Light vibration: {lightVibration} for {duration}s");
    }
    
    /// <summary>
    /// Trigger medium vibration (catches, hits)
    /// </summary>
    public void VibrateMedium(float customDuration = -1f)
    {
        if (!enableHaptics || currentGamepad == null) return;
        
        float duration = customDuration > 0 ? customDuration : mediumDuration;
        StartVibration(mediumVibration, mediumVibration, duration);
        
        if (debugMode) Debug.Log($"[HAPTICS] Medium vibration: {mediumVibration} for {duration}s");
    }
    
    /// <summary>
    /// Trigger strong vibration (ultimates, stuns)
    /// </summary>
    public void VibrateStrong(float customDuration = -1f)
    {
        if (!enableHaptics || currentGamepad == null) return;
        
        float duration = customDuration > 0 ? customDuration : strongDuration;
        StartVibration(strongVibration, strongVibration, duration);
        
        if (debugMode) Debug.Log($"[HAPTICS] Strong vibration: {strongVibration} for {duration}s");
    }
    
    /// <summary>
    /// Start continuous vibration (for ultimate charge)
    /// </summary>
    public void StartContinuousVibration(float intensity = 0.7f)
    {
        if (!enableHaptics || currentGamepad == null) return;
        
        if (activeVibrationCoroutine != null)
            StopCoroutine(activeVibrationCoroutine);
        
        currentGamepad.SetMotorSpeeds(intensity, intensity);
        
        if (debugMode) Debug.Log($"[HAPTICS] Continuous vibration started: {intensity}");
    }
    
    /// <summary>
    /// Stop continuous vibration
    /// </summary>
    public void StopContinuousVibration()
    {
        if (currentGamepad == null) return;
        
        currentGamepad.SetMotorSpeeds(0f, 0f);
        
        if (activeVibrationCoroutine != null)
        {
            StopCoroutine(activeVibrationCoroutine);
            activeVibrationCoroutine = null;
        }
        
        if (debugMode) Debug.Log("[HAPTICS] Continuous vibration stopped");
    }
    
    /// <summary>
    /// Start timed vibration
    /// </summary>
    void StartVibration(float lowFreq, float highFreq, float duration)
    {
        if (activeVibrationCoroutine != null)
            StopCoroutine(activeVibrationCoroutine);
        
        activeVibrationCoroutine = StartCoroutine(VibrationCoroutine(lowFreq, highFreq, duration));
    }
    
    System.Collections.IEnumerator VibrationCoroutine(float lowFreq, float highFreq, float duration)
    {
        if (currentGamepad != null)
        {
            currentGamepad.SetMotorSpeeds(lowFreq, highFreq);
        }
        
        yield return new WaitForSeconds(duration);
        
        if (currentGamepad != null)
        {
            currentGamepad.SetMotorSpeeds(0f, 0f);
        }
        
        activeVibrationCoroutine = null;
    }
    
    /// <summary>
    /// Custom vibration with specific frequencies
    /// </summary>
    public void VibrateCustom(float lowFrequency, float highFrequency, float duration)
    {
        if (!enableHaptics || currentGamepad == null) return;
        
        StartVibration(Mathf.Clamp01(lowFrequency), Mathf.Clamp01(highFrequency), duration);
        
        if (debugMode) Debug.Log($"[HAPTICS] Custom vibration: Low={lowFrequency}, High={highFrequency}, Duration={duration}s");
    }
#else
    // Stub methods when Input System is not available
    public void VibrateLight(float customDuration = -1f) { }
    public void VibrateMedium(float customDuration = -1f) { }
    public void VibrateStrong(float customDuration = -1f) { }
    public void StartContinuousVibration(float intensity = 0.7f) { }
    public void StopContinuousVibration() { }
    public void VibrateCustom(float lowFrequency, float highFrequency, float duration) { }
#endif
    
    void OnDestroy()
    {
#if ENABLE_INPUT_SYSTEM
        // Stop any active vibration when destroyed
        StopContinuousVibration();
#endif
    }
}

