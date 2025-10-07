using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using System.Linq;
using System;

/// <summary>
/// Advanced component copying tool for Unity Editor.
/// Allows selective component copying with persistent clipboard, multi-target paste, and dependency management.
/// Version 1.0.0
/// </summary>
public class CopySelectedComponentsWindow : EditorWindow
{
    private GameObject sourceObject;
    private GameObject targetObject;
    private Vector2 scrollPos;
    private List<ComponentData> componentData = new List<ComponentData>();
    private bool selectAll = false;
    private string searchFilter = "";
    private bool replaceExisting = false;

    // Custom clipboard system
    private static List<Component> customClipboard = new List<Component>();
    private static string clipboardSourceName = "";

    private const string CLIPBOARD_PREFS_KEY = "CopyComponents_Clipboard";
    private const string CLIPBOARD_SOURCE_KEY = "CopyComponents_Source";

    private class ComponentData
    {
        public Component component;
        public bool selected;
        public bool canCopy = true;
        public string warning = "";

        public ComponentData(Component comp)
        {
            component = comp;
            selected = false;
            ValidateComponent();
        }

        private void ValidateComponent()
        {
            // Check if component can be copied
            if (component.GetType().GetCustomAttributes(typeof(RequireComponent), true).Length > 0)
            {
                warning = "Requires dependencies";
            }
        }
    }

    [MenuItem("Tools/Copy Selected Components %#C")]
    public static void ShowWindow()
    {
        var window = GetWindow<CopySelectedComponentsWindow>("Copy Components");
        window.minSize = new Vector2(400, 350);
        window.LoadClipboardFromPrefs();
    }

    [MenuItem("GameObject/Paste Components from Clipboard %#V", false, 0)]
    private static void PasteFromCustomClipboard()
    {
        if (customClipboard.Count == 0)
        {
            Debug.LogWarning("No components in clipboard. Use Tools > Copy Selected Components to copy components first.");
            return;
        }

        GameObject[] targets = Selection.gameObjects;
        if (targets.Length == 0)
        {
            Debug.LogWarning("Please select one or more GameObjects in the Hierarchy to paste components to.");
            return;
        }

        PasteComponentsToTargets(targets, false);
    }

    [MenuItem("GameObject/Paste Components from Clipboard %#V", true)]
    private static bool ValidatePasteFromCustomClipboard()
    {
        return customClipboard.Count > 0 && Selection.gameObjects.Length > 0;
    }

    private void OnEnable()
    {
        LoadClipboardFromPrefs();
    }

    private void LoadClipboardFromPrefs()
    {
        // Try to restore clipboard from previous session
        if (EditorPrefs.HasKey(CLIPBOARD_PREFS_KEY))
        {
            string clipboardData = EditorPrefs.GetString(CLIPBOARD_PREFS_KEY);
            clipboardSourceName = EditorPrefs.GetString(CLIPBOARD_SOURCE_KEY, "");

            // Parse stored component instance IDs
            if (!string.IsNullOrEmpty(clipboardData))
            {
                string[] ids = clipboardData.Split(',');
                customClipboard.Clear();

                foreach (string idStr in ids)
                {
                    if (int.TryParse(idStr, out int id))
                    {
                        var comp = EditorUtility.InstanceIDToObject(id) as Component;
                        if (comp != null)
                        {
                            customClipboard.Add(comp);
                        }
                    }
                }

                if (customClipboard.Count > 0)
                {
                    Debug.Log($"Restored {customClipboard.Count} components from previous session");
                }
            }
        }
    }

    private void SaveClipboardToPrefs()
    {
        if (customClipboard.Count > 0)
        {
            string clipboardData = string.Join(",", customClipboard.Select(c => c.GetInstanceID().ToString()));
            EditorPrefs.SetString(CLIPBOARD_PREFS_KEY, clipboardData);
            EditorPrefs.SetString(CLIPBOARD_SOURCE_KEY, clipboardSourceName);
        }
        else
        {
            EditorPrefs.DeleteKey(CLIPBOARD_PREFS_KEY);
            EditorPrefs.DeleteKey(CLIPBOARD_SOURCE_KEY);
        }
    }

    private static void PasteComponentsToTargets(GameObject[] targets, bool replaceMode)
    {
        Undo.SetCurrentGroupName("Paste Components to Multiple Objects");
        int undoGroup = Undo.GetCurrentGroup();

        int totalCopied = 0;
        int totalReplaced = 0;

        foreach (var target in targets)
        {
            foreach (var sourceComp in customClipboard)
            {
                if (sourceComp == null) continue;

                try
                {
                    var type = sourceComp.GetType();

                    // Ensure component dependencies
                    if (Attribute.IsDefined(type, typeof(RequireComponent)))
                    {
                        EnsureComponentDependencies(target, type);
                    }

                    Component targetComp = null;
                    bool wasReplaced = false;

                    if (replaceMode)
                    {
                        // Check if component already exists
                        targetComp = target.GetComponent(type);
                        if (targetComp != null)
                        {
                            Undo.RecordObject(targetComp, "Replace Component Values");
                            wasReplaced = true;
                        }
                    }

                    if (targetComp == null)
                    {
                        targetComp = Undo.AddComponent(target, type);
                    }

                    // Copy serialized properties
                    CopySerializedProperties(sourceComp, targetComp);

                    // Mark dirty to ensure ScriptableObject references are serialized
                    EditorUtility.SetDirty(targetComp);

                    if (wasReplaced)
                        totalReplaced++;
                    else
                        totalCopied++;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to copy {sourceComp.GetType().Name} to {target.name}: {e.Message}");
                }
            }
        }

        Undo.CollapseUndoOperations(undoGroup);

        string message = replaceMode
            ? $"Pasted to {targets.Length} object(s): {totalCopied} new, {totalReplaced} replaced"
            : $"Pasted {totalCopied} component(s) to {targets.Length} object(s)";

        Debug.Log(message);
    }

    private static void EnsureComponentDependencies(GameObject target, Type componentType)
    {
        var requireComponent = Attribute.GetCustomAttribute(componentType, typeof(RequireComponent)) as RequireComponent;
        if (requireComponent == null) return;

        // Add required components if they don't exist
        if (requireComponent.m_Type0 != null && target.GetComponent(requireComponent.m_Type0) == null)
            Undo.AddComponent(target, requireComponent.m_Type0);

        if (requireComponent.m_Type1 != null && target.GetComponent(requireComponent.m_Type1) == null)
            Undo.AddComponent(target, requireComponent.m_Type1);

        if (requireComponent.m_Type2 != null && target.GetComponent(requireComponent.m_Type2) == null)
            Undo.AddComponent(target, requireComponent.m_Type2);
    }

    private static void CopySerializedProperties(Component source, Component destination)
    {
        var soSrc = new SerializedObject(source);
        var soDst = new SerializedObject(destination);

        var prop = soSrc.GetIterator();
        while (prop.NextVisible(true))
        {
            if (prop.name == "m_Script") continue;

            var dstProp = soDst.FindProperty(prop.propertyPath);
            if (dstProp != null && dstProp.propertyType == prop.propertyType)
            {
                // Use CopyFromSerializedPropertyIfDifferent for better handling of nested fields
                soDst.CopyFromSerializedPropertyIfDifferent(prop);
            }
        }

        soDst.ApplyModifiedProperties();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Component Copy Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        DrawSourceSection();

        if (componentData.Count > 0)
        {
            EditorGUILayout.Space();
            DrawComponentList();
        }

        EditorGUILayout.Space();
        DrawTargetSection();

        EditorGUILayout.Space();
        DrawActionButtons();
    }

    private void DrawSourceSection()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);

        var newSource = (GameObject)EditorGUILayout.ObjectField("Source Object", sourceObject, typeof(GameObject), true);

        if (newSource != sourceObject)
        {
            sourceObject = newSource;
            if (sourceObject != null)
            {
                LoadComponents();
            }
            else
            {
                componentData.Clear();
            }
        }

        if (sourceObject != null && GUILayout.Button("Reload Components"))
        {
            LoadComponents();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawComponentList()
    {
        EditorGUILayout.BeginVertical("box");

        // Header with select all and search
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Components to Copy", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        if (GUILayout.Button(selectAll ? "Deselect All" : "Select All", GUILayout.Width(100)))
        {
            selectAll = !selectAll;
            foreach (var data in componentData)
            {
                if (data.canCopy)
                    data.selected = selectAll;
            }
        }
        EditorGUILayout.EndHorizontal();

        // Search filter
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
        searchFilter = EditorGUILayout.TextField(searchFilter);
        if (GUILayout.Button("×", GUILayout.Width(20)))
        {
            searchFilter = "";
            GUI.FocusControl(null);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // Component list
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.MinHeight(150), GUILayout.MaxHeight(300));

        int selectedCount = 0;
        foreach (var data in componentData)
        {
            if (data.component == null) continue;

            string typeName = data.component.GetType().Name;

            // Filter by search
            if (!string.IsNullOrEmpty(searchFilter) &&
                !typeName.ToLower().Contains(searchFilter.ToLower()))
                continue;

            EditorGUILayout.BeginHorizontal();

            GUI.enabled = data.canCopy;
            bool newSelected = EditorGUILayout.ToggleLeft(typeName, data.selected);
            if (newSelected != data.selected)
            {
                data.selected = newSelected;
            }

            if (data.selected) selectedCount++;

            GUI.enabled = true;

            // Show warning icon if exists
            if (!string.IsNullOrEmpty(data.warning))
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(new GUIContent("⚠", data.warning), GUILayout.Width(20));
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.LabelField($"Selected: {selectedCount} / {componentData.Count}", EditorStyles.miniLabel);

        EditorGUILayout.EndVertical();
    }

    private void DrawTargetSection()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);

        targetObject = (GameObject)EditorGUILayout.ObjectField("Target Object", targetObject, typeof(GameObject), true);

        // Replace mode toggle
        replaceExisting = EditorGUILayout.Toggle(new GUIContent("Replace Existing",
            "If enabled, existing components will be updated instead of creating duplicates"), replaceExisting);

        // Show target info
        if (targetObject != null)
        {
            int existingComponents = targetObject.GetComponents<Component>().Length - 1; // -1 for Transform
            EditorGUILayout.LabelField($"Existing components: {existingComponents}", EditorStyles.miniLabel);

            // Check for conflicts
            CheckForConflicts();
        }

        EditorGUILayout.EndVertical();
    }

    private void CheckForConflicts()
    {
        if (targetObject == null) return;

        var existingTypes = targetObject.GetComponents<Component>().Select(c => c.GetType()).ToList();
        var conflicts = componentData.Where(d => d.selected && existingTypes.Contains(d.component.GetType())).ToList();

        if (conflicts.Count > 0)
        {
            if (replaceExisting)
            {
                EditorGUILayout.HelpBox($"{conflicts.Count} component(s) will be replaced with new values.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox($"Warning: {conflicts.Count} component(s) already exist on target. They will be added as duplicates.", MessageType.Warning);
            }
        }
    }

    private void DrawActionButtons()
    {
        bool canPaste = sourceObject != null &&
                       targetObject != null &&
                       componentData.Any(d => d.selected);

        GUI.enabled = canPaste;

        if (GUILayout.Button("Paste Selected Components", GUILayout.Height(30)))
        {
            PasteSelectedComponents();
        }

        GUI.enabled = true;

        // Additional utility buttons
        EditorGUILayout.BeginHorizontal();

        GUI.enabled = componentData.Any(d => d.selected);
        if (GUILayout.Button("Copy to Clipboard"))
        {
            CopyToCustomClipboard();
        }
        GUI.enabled = true;

        if (GUILayout.Button("Clear Clipboard"))
        {
            ClearClipboard();
        }

        if (componentData.Count > 0 && GUILayout.Button("Clear Selection"))
        {
            foreach (var data in componentData)
            {
                data.selected = false;
            }
            selectAll = false;
        }

        EditorGUILayout.EndHorizontal();

        // Show clipboard status
        if (customClipboard.Count > 0)
        {
            EditorGUILayout.HelpBox(
                $"📋 Clipboard: {customClipboard.Count} component(s) from '{clipboardSourceName}'\n" +
                "Select GameObject(s) → Right-click → 'Paste Components from Clipboard' (Ctrl+Shift+V)",
                MessageType.Info);
        }
        else if (componentData.Any(d => d.selected))
        {
            EditorGUILayout.HelpBox(
                "💡 Click 'Copy to Clipboard' to store components, then paste to any GameObject(s)!",
                MessageType.Info);
        }
    }

    private void LoadComponents()
    {
        componentData.Clear();

        if (sourceObject == null) return;

        foreach (var comp in sourceObject.GetComponents<Component>())
        {
            if (comp == null || comp is Transform) continue;

            componentData.Add(new ComponentData(comp));
        }

        if (componentData.Count == 0)
        {
            ShowNotification(new GUIContent("No copyable components found"));
        }
    }

    private void PasteSelectedComponents()
    {
        if (targetObject == null) return;

        Undo.SetCurrentGroupName("Paste Components");
        int undoGroup = Undo.GetCurrentGroup();

        int copiedCount = 0;
        int replacedCount = 0;

        foreach (var data in componentData)
        {
            if (!data.selected || data.component == null || !data.canCopy) continue;

            var type = data.component.GetType();

            try
            {
                // Ensure component dependencies
                if (Attribute.IsDefined(type, typeof(RequireComponent)))
                {
                    EnsureComponentDependencies(targetObject, type);
                }

                Component targetComp = null;
                bool wasReplaced = false;

                if (replaceExisting)
                {
                    targetComp = targetObject.GetComponent(type);
                    if (targetComp != null)
                    {
                        Undo.RecordObject(targetComp, "Replace Component Values");
                        wasReplaced = true;
                    }
                }

                if (targetComp == null)
                {
                    targetComp = Undo.AddComponent(targetObject, type);
                }

                // Copy serialized properties
                CopySerializedProperties(data.component, targetComp);

                // Mark dirty to ensure ScriptableObject references are serialized
                EditorUtility.SetDirty(targetComp);

                if (wasReplaced)
                    replacedCount++;
                else
                    copiedCount++;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to copy {type.Name}: {e.Message}");
            }
        }

        Undo.CollapseUndoOperations(undoGroup);

        string message = replaceExisting
            ? $"Success! {copiedCount} new, {replacedCount} replaced"
            : $"Success! Copied {copiedCount} component(s)";

        ShowNotification(new GUIContent(message));
        Debug.Log($"Pasted components to {targetObject.name}: {copiedCount} new, {replacedCount} replaced");
    }

    private void CopyToCustomClipboard()
    {
        var selectedComps = componentData.Where(d => d.selected && d.component != null).Select(d => d.component).ToList();

        if (selectedComps.Count == 0)
        {
            ShowNotification(new GUIContent("No components selected"));
            return;
        }

        customClipboard = selectedComps;
        clipboardSourceName = sourceObject != null ? sourceObject.name : "Unknown";

        // Persist clipboard
        SaveClipboardToPrefs();

        ShowNotification(new GUIContent($"✓ Copied {customClipboard.Count} component(s) to clipboard"));
        Debug.Log($"Copied {customClipboard.Count} components to clipboard from {clipboardSourceName}");
    }

    private void ClearClipboard()
    {
        if (customClipboard.Count == 0)
        {
            ShowNotification(new GUIContent("Clipboard already empty"));
            return;
        }

        customClipboard.Clear();
        clipboardSourceName = "";
        SaveClipboardToPrefs();

        ShowNotification(new GUIContent("Clipboard cleared"));
    }
}