using UnityEngine;

/// <summary>
/// ScriptableObject for custom controller button mapping
/// FULLY FLEXIBLE: Any action can be mapped to ANY button OR trigger
/// Use the custom editor to auto-detect button presses
/// </summary>
[CreateAssetMenu(fileName = "ControllerMapping", menuName = "Game/Controller Mapping")]
public class ControllerMappingData : ScriptableObject
{
    [System.Serializable]
    public class ActionMapping
    {
        [Tooltip("Type of input: Button or Trigger")]
        public InputType inputType = InputType.Button;
        
        [Tooltip("Button assignment (if inputType is Button)")]
        public ControllerButton button = ControllerButton.ButtonSouth;
        
        [Tooltip("Trigger assignment (if inputType is Trigger)")]
        public ControllerTrigger trigger = ControllerTrigger.RightTrigger;
        
        [Tooltip("For triggers: threshold to activate (0.0 - 1.0)")]
        [Range(0.1f, 1.0f)]
        public float triggerThreshold = 0.5f;
        
        public enum InputType
        {
            Button,
            Trigger
        }
    }
    
    [Header("Controller Action Mapping")]
    [Tooltip("Jump action mapping - Use custom editor to auto-detect button presses")]
    public ActionMapping jumpMapping = new ActionMapping { inputType = ActionMapping.InputType.Button, button = ControllerButton.ButtonSouth };
    
    [Tooltip("Throw action mapping")]
    public ActionMapping throwMapping = new ActionMapping { inputType = ActionMapping.InputType.Button, button = ControllerButton.ButtonWest };
    
    [Tooltip("Catch action mapping")]
    public ActionMapping catchMapping = new ActionMapping { inputType = ActionMapping.InputType.Button, button = ControllerButton.ButtonEast };
    
    [Tooltip("Pickup action mapping")]
    public ActionMapping pickupMapping = new ActionMapping { inputType = ActionMapping.InputType.Button, button = ControllerButton.ButtonNorth };
    
    [Tooltip("Dash action mapping")]
    public ActionMapping dashMapping = new ActionMapping { inputType = ActionMapping.InputType.Button, button = ControllerButton.RightShoulder };
    
    [Tooltip("Ultimate action mapping (supports hold/release)")]
    public ActionMapping ultimateMapping = new ActionMapping { inputType = ActionMapping.InputType.Trigger, trigger = ControllerTrigger.RightTrigger, triggerThreshold = 0.5f };
    
    [Tooltip("Trick action mapping")]
    public ActionMapping trickMapping = new ActionMapping { inputType = ActionMapping.InputType.Button, button = ControllerButton.LeftShoulder };
    
    [Tooltip("Treat action mapping")]
    public ActionMapping treatMapping = new ActionMapping { inputType = ActionMapping.InputType.Trigger, trigger = ControllerTrigger.LeftTrigger, triggerThreshold = 0.5f };
    
    [Tooltip("Duck action mapping")]
    public ActionMapping duckMapping = new ActionMapping { inputType = ActionMapping.InputType.Button, button = ControllerButton.DPadDown };
    
    [Header("Stick Settings")]
    [Tooltip("Stick used for movement")]
    public ControllerStick movementStick = ControllerStick.LeftStick;
    
    [Tooltip("Deadzone for movement stick (0.1 - 0.5)")]
    [Range(0.1f, 0.5f)]
    public float movementDeadzone = 0.2f;
    
    [Header("Trigger Settings (Global)")]
    [Tooltip("Default threshold for trigger release (0.0 - 1.0)")]
    [Range(0.1f, 1.0f)]
    public float triggerReleaseThreshold = 0.3f;
    
    [Header("Description")]
    [TextArea(2, 4)]
    [Tooltip("Description of this controller mapping preset")]
    public string description = "Default controller mapping";
    
    // ═══════════════════════════════════════════════════════════════
    // ENUMS
    // ═══════════════════════════════════════════════════════════════
    
    public enum ControllerButton
    {
        ButtonSouth,    // X (PS5) / A (Xbox)
        ButtonWest,     // Square (PS5) / X (Xbox)
        ButtonEast,     // Circle (PS5) / B (Xbox)
        ButtonNorth,    // Triangle (PS5) / Y (Xbox)
        LeftShoulder,   // L1 (PS5) / LB (Xbox)
        RightShoulder,  // R1 (PS5) / RB (Xbox)
        LeftStick,      // Left stick press
        RightStick,     // Right stick press
        DPadUp,
        DPadDown,
        DPadLeft,
        DPadRight,
        Start,
        Select
    }
    
    public enum ControllerTrigger
    {
        LeftTrigger,   // L2 (PS5) / LT (Xbox)
        RightTrigger   // R2 (PS5) / RT (Xbox)
    }
    
    public enum ControllerStick
    {
        LeftStick,
        RightStick
    }
    
    // ═══════════════════════════════════════════════════════════════
    // VALIDATION
    // ═══════════════════════════════════════════════════════════════
    
    private void OnValidate()
    {
        // Ensure trigger release threshold is valid
        foreach (var mapping in new[] { jumpMapping, throwMapping, catchMapping, pickupMapping, 
                                        dashMapping, ultimateMapping, trickMapping, treatMapping, duckMapping })
        {
            if (mapping.inputType == ActionMapping.InputType.Trigger)
            {
                if (mapping.triggerThreshold <= triggerReleaseThreshold)
                {
                    mapping.triggerThreshold = triggerReleaseThreshold + 0.1f;
                }
            }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // HELPER METHODS
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Get a formatted description of the mapping
    /// </summary>
    public string GetFormattedDescription()
    {
        if (!string.IsNullOrEmpty(description))
            return description;
        
        return $"Jump: {GetMappingString(jumpMapping)}, Throw: {GetMappingString(throwMapping)}, " +
               $"Catch: {GetMappingString(catchMapping)}, Pickup: {GetMappingString(pickupMapping)}, " +
               $"Dash: {GetMappingString(dashMapping)}, Ultimate: {GetMappingString(ultimateMapping)}, " +
               $"Trick: {GetMappingString(trickMapping)}, Treat: {GetMappingString(treatMapping)}, " +
               $"Duck: {GetMappingString(duckMapping)}";
    }
    
    string GetMappingString(ActionMapping mapping)
    {
        if (mapping.inputType == ActionMapping.InputType.Button)
            return mapping.button.ToString();
        else
            return mapping.trigger.ToString();
    }
}
