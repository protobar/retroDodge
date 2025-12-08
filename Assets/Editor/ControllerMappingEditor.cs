using UnityEngine;
using UnityEditor;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Custom editor for ControllerMappingData with auto-detection
/// Press any button/trigger on your controller to automatically assign it
/// </summary>
[CustomEditor(typeof(ControllerMappingData))]
public class ControllerMappingEditor : Editor
{
    private ControllerMappingData mappingData;
    private string detectingForAction = "";
    private float lastDetectionTime = 0f;
    private const float DETECTION_TIMEOUT = 5f; // Stop detecting after 5 seconds
    
#if ENABLE_INPUT_SYSTEM
    private Gamepad lastDetectedGamepad = null;
#endif
    
    void OnEnable()
    {
        mappingData = (ControllerMappingData)target;
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("CONTROLLER AUTO-DETECTION:\n" +
                               "1. Click 'Detect' button next to any action\n" +
                               "2. Press ANY button or trigger on your controller\n" +
                               "3. It will automatically assign that input!", 
                               MessageType.Info);
        EditorGUILayout.Space();
        
        // Show detection status
        if (!string.IsNullOrEmpty(detectingForAction))
        {
            float timeSinceStart = Time.realtimeSinceStartup - lastDetectionTime;
            if (timeSinceStart < DETECTION_TIMEOUT)
            {
                EditorGUILayout.HelpBox($"🔍 Detecting for: {detectingForAction}\nPress any button/trigger on your controller... ({DETECTION_TIMEOUT - timeSinceStart:F1}s)", 
                                       MessageType.Warning);
                
                // Auto-detect input
                DetectControllerInput();
            }
            else
            {
                detectingForAction = "";
                EditorGUILayout.HelpBox("Detection timeout. Click 'Detect' again to retry.", MessageType.Info);
            }
        }
        
        EditorGUILayout.Space();
        
        // Draw default inspector
        DrawDefaultInspector();
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Action Mappings", EditorStyles.boldLabel);
        
        // Draw each action with detect button
        DrawActionMapping("Jump", serializedObject.FindProperty("jumpMapping"), mappingData.jumpMapping);
        DrawActionMapping("Throw", serializedObject.FindProperty("throwMapping"), mappingData.throwMapping);
        DrawActionMapping("Catch", serializedObject.FindProperty("catchMapping"), mappingData.catchMapping);
        DrawActionMapping("Pickup", serializedObject.FindProperty("pickupMapping"), mappingData.pickupMapping);
        DrawActionMapping("Dash", serializedObject.FindProperty("dashMapping"), mappingData.dashMapping);
        DrawActionMapping("Ultimate", serializedObject.FindProperty("ultimateMapping"), mappingData.ultimateMapping);
        DrawActionMapping("Trick", serializedObject.FindProperty("trickMapping"), mappingData.trickMapping);
        DrawActionMapping("Treat", serializedObject.FindProperty("treatMapping"), mappingData.treatMapping);
        DrawActionMapping("Duck", serializedObject.FindProperty("duckMapping"), mappingData.duckMapping);
        
        EditorGUILayout.Space();
        
        // Reset to defaults button
        if (GUILayout.Button("Reset All to Defaults", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("Reset Mappings", 
                "Reset all controller mappings to default values?", 
                "Yes", "Cancel"))
            {
                ResetToDefaults();
            }
        }
        
        serializedObject.ApplyModifiedProperties();
        
        // Repaint to update detection status
        if (!string.IsNullOrEmpty(detectingForAction))
        {
            Repaint();
        }
    }
    
    void DrawActionMapping(string actionName, SerializedProperty property, ControllerMappingData.ActionMapping mapping)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(actionName, EditorStyles.boldLabel, GUILayout.Width(100));
        
        // Detect button
        GUI.enabled = string.IsNullOrEmpty(detectingForAction) || detectingForAction == actionName;
        if (GUILayout.Button(detectingForAction == actionName ? "Detecting..." : "Detect", GUILayout.Width(80)))
        {
            StartDetection(actionName);
        }
        GUI.enabled = true;
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        // Show current mapping
        string currentMapping = GetCurrentMappingString(mapping);
        EditorGUILayout.LabelField($"Current: {currentMapping}", EditorStyles.miniLabel);
        
        // Draw the mapping property
        EditorGUILayout.PropertyField(property, true);
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }
    
    string GetCurrentMappingString(ControllerMappingData.ActionMapping mapping)
    {
        if (mapping.inputType == ControllerMappingData.ActionMapping.InputType.Button)
        {
            return $"Button: {mapping.button}";
        }
        else
        {
            return $"Trigger: {mapping.trigger} (Threshold: {mapping.triggerThreshold:F2})";
        }
    }
    
    void StartDetection(string actionName)
    {
        detectingForAction = actionName;
        lastDetectionTime = Time.realtimeSinceStartup;
        
#if ENABLE_INPUT_SYSTEM
        if (Gamepad.current != null)
        {
            lastDetectedGamepad = Gamepad.current;
        }
#endif
    }
    
    void DetectControllerInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Gamepad.current == null)
        {
            // Check if gamepad was just connected
            if (Gamepad.all.Count > 0)
            {
                lastDetectedGamepad = Gamepad.all[0];
            }
            else
            {
                return; // No gamepad connected
            }
        }
        else
        {
            lastDetectedGamepad = Gamepad.current;
        }
        
        var gamepad = lastDetectedGamepad;
        if (gamepad == null) return;
        
        // Check all buttons
        if (gamepad.buttonSouth.wasPressedThisFrame)
        {
            AssignButton(ControllerMappingData.ControllerButton.ButtonSouth);
            return;
        }
        if (gamepad.buttonWest.wasPressedThisFrame)
        {
            AssignButton(ControllerMappingData.ControllerButton.ButtonWest);
            return;
        }
        if (gamepad.buttonEast.wasPressedThisFrame)
        {
            AssignButton(ControllerMappingData.ControllerButton.ButtonEast);
            return;
        }
        if (gamepad.buttonNorth.wasPressedThisFrame)
        {
            AssignButton(ControllerMappingData.ControllerButton.ButtonNorth);
            return;
        }
        if (gamepad.leftShoulder.wasPressedThisFrame)
        {
            AssignButton(ControllerMappingData.ControllerButton.LeftShoulder);
            return;
        }
        if (gamepad.rightShoulder.wasPressedThisFrame)
        {
            AssignButton(ControllerMappingData.ControllerButton.RightShoulder);
            return;
        }
        if (gamepad.leftStickButton.wasPressedThisFrame)
        {
            AssignButton(ControllerMappingData.ControllerButton.LeftStick);
            return;
        }
        if (gamepad.rightStickButton.wasPressedThisFrame)
        {
            AssignButton(ControllerMappingData.ControllerButton.RightStick);
            return;
        }
        if (gamepad.dpad.up.wasPressedThisFrame)
        {
            AssignButton(ControllerMappingData.ControllerButton.DPadUp);
            return;
        }
        if (gamepad.dpad.down.wasPressedThisFrame)
        {
            AssignButton(ControllerMappingData.ControllerButton.DPadDown);
            return;
        }
        if (gamepad.dpad.left.wasPressedThisFrame)
        {
            AssignButton(ControllerMappingData.ControllerButton.DPadLeft);
            return;
        }
        if (gamepad.dpad.right.wasPressedThisFrame)
        {
            AssignButton(ControllerMappingData.ControllerButton.DPadRight);
            return;
        }
        if (gamepad.startButton.wasPressedThisFrame)
        {
            AssignButton(ControllerMappingData.ControllerButton.Start);
            return;
        }
        if (gamepad.selectButton.wasPressedThisFrame)
        {
            AssignButton(ControllerMappingData.ControllerButton.Select);
            return;
        }
        
        // Check triggers (with threshold to avoid accidental detection)
        float leftTrigger = gamepad.leftTrigger.ReadValue();
        if (leftTrigger > 0.3f)
        {
            AssignTrigger(ControllerMappingData.ControllerTrigger.LeftTrigger, leftTrigger);
            return;
        }
        
        float rightTrigger = gamepad.rightTrigger.ReadValue();
        if (rightTrigger > 0.3f)
        {
            AssignTrigger(ControllerMappingData.ControllerTrigger.RightTrigger, rightTrigger);
            return;
        }
#endif
    }
    
    void AssignButton(ControllerMappingData.ControllerButton button)
    {
        Undo.RecordObject(mappingData, $"Assign {button} to {detectingForAction}");
        
        var mapping = GetMappingForAction(detectingForAction);
        if (mapping != null)
        {
            mapping.inputType = ControllerMappingData.ActionMapping.InputType.Button;
            mapping.button = button;
            
            EditorUtility.SetDirty(mappingData);
            detectingForAction = "";
            
            Debug.Log($"✅ Assigned {button} to {detectingForAction} action!");
        }
    }
    
    void AssignTrigger(ControllerMappingData.ControllerTrigger trigger, float value)
    {
        Undo.RecordObject(mappingData, $"Assign {trigger} to {detectingForAction}");
        
        var mapping = GetMappingForAction(detectingForAction);
        if (mapping != null)
        {
            mapping.inputType = ControllerMappingData.ActionMapping.InputType.Trigger;
            mapping.trigger = trigger;
            mapping.triggerThreshold = Mathf.Clamp01(value);
            
            EditorUtility.SetDirty(mappingData);
            detectingForAction = "";
            
            Debug.Log($"✅ Assigned {trigger} (threshold: {value:F2}) to {detectingForAction} action!");
        }
    }
    
    ControllerMappingData.ActionMapping GetMappingForAction(string actionName)
    {
        switch (actionName)
        {
            case "Jump": return mappingData.jumpMapping;
            case "Throw": return mappingData.throwMapping;
            case "Catch": return mappingData.catchMapping;
            case "Pickup": return mappingData.pickupMapping;
            case "Dash": return mappingData.dashMapping;
            case "Ultimate": return mappingData.ultimateMapping;
            case "Trick": return mappingData.trickMapping;
            case "Treat": return mappingData.treatMapping;
            case "Duck": return mappingData.duckMapping;
            default: return null;
        }
    }
    
    void ResetToDefaults()
    {
        Undo.RecordObject(mappingData, "Reset Controller Mappings to Defaults");
        
        mappingData.jumpMapping = new ControllerMappingData.ActionMapping 
        { inputType = ControllerMappingData.ActionMapping.InputType.Button, button = ControllerMappingData.ControllerButton.ButtonSouth };
        
        mappingData.throwMapping = new ControllerMappingData.ActionMapping 
        { inputType = ControllerMappingData.ActionMapping.InputType.Button, button = ControllerMappingData.ControllerButton.ButtonWest };
        
        mappingData.catchMapping = new ControllerMappingData.ActionMapping 
        { inputType = ControllerMappingData.ActionMapping.InputType.Button, button = ControllerMappingData.ControllerButton.ButtonEast };
        
        mappingData.pickupMapping = new ControllerMappingData.ActionMapping 
        { inputType = ControllerMappingData.ActionMapping.InputType.Button, button = ControllerMappingData.ControllerButton.ButtonNorth };
        
        mappingData.dashMapping = new ControllerMappingData.ActionMapping 
        { inputType = ControllerMappingData.ActionMapping.InputType.Button, button = ControllerMappingData.ControllerButton.RightShoulder };
        
        mappingData.ultimateMapping = new ControllerMappingData.ActionMapping 
        { inputType = ControllerMappingData.ActionMapping.InputType.Trigger, trigger = ControllerMappingData.ControllerTrigger.RightTrigger, triggerThreshold = 0.5f };
        
        mappingData.trickMapping = new ControllerMappingData.ActionMapping 
        { inputType = ControllerMappingData.ActionMapping.InputType.Button, button = ControllerMappingData.ControllerButton.LeftShoulder };
        
        mappingData.treatMapping = new ControllerMappingData.ActionMapping 
        { inputType = ControllerMappingData.ActionMapping.InputType.Trigger, trigger = ControllerMappingData.ControllerTrigger.LeftTrigger, triggerThreshold = 0.5f };
        
        mappingData.duckMapping = new ControllerMappingData.ActionMapping 
        { inputType = ControllerMappingData.ActionMapping.InputType.Button, button = ControllerMappingData.ControllerButton.DPadDown };
        
        EditorUtility.SetDirty(mappingData);
        Debug.Log("✅ Controller mappings reset to defaults!");
    }
}

