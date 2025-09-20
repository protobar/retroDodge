using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RetroDodge.Progression
{
    /// <summary>
    /// Editor utility to create default progression configuration
    /// </summary>
    public class CreateDefaultConfig : MonoBehaviour
    {
        [ContextMenu("Create Default Progression Config")]
        public void CreateDefaultProgressionConfig()
        {
#if UNITY_EDITOR
            // Create the configuration asset
            ProgressionConfiguration config = ScriptableObject.CreateInstance<ProgressionConfiguration>();
            config.ResetToDefaults();

            // Ensure Resources folder exists
            if (!System.IO.Directory.Exists("Assets/Resources"))
            {
                System.IO.Directory.CreateDirectory("Assets/Resources");
            }

            // Save the asset
            string path = "Assets/Resources/DefaultProgressionConfig.asset";
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PROGRESSION] Created default configuration at {path}");
#else
            Debug.LogWarning("[PROGRESSION] CreateDefaultConfig can only be used in the Unity Editor");
#endif
        }
    }
}
