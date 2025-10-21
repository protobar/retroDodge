using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RetroDodgeRumble.Tutorial
{
    /// <summary>
    /// Main menu integration for character info with panel management
    /// </summary>
    public class MainMenuCharacterIntegration : MonoBehaviour
    {
        [Header("Main Menu Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject characterInfoPanel;
        
        [Header("Character Info Button")]
        [SerializeField] private Button characterInfoButton;
        [SerializeField] private TextMeshProUGUI characterInfoButtonText;
        
        [Header("Tutorial Integration")]
        [SerializeField] private SimpleTutorialManager tutorialManager;
        
        [Header("Character Data")]
        [SerializeField] private CharacterData[] availableCharacters;
        
        [Header("Settings")]
        [SerializeField] private bool debugMode = true;
        
        // State
        private bool isCharacterInfoActive = false;
        
        void Start()
        {
            InitializeMainMenuIntegration();
        }
        
        /// <summary>
        /// Initialize main menu integration
        /// </summary>
        void InitializeMainMenuIntegration()
        {
            // Get tutorial manager if not assigned
            if (tutorialManager == null)
            {
                tutorialManager = FindObjectOfType<SimpleTutorialManager>();
            }
            
            // Setup character info button
            if (characterInfoButton != null)
            {
                characterInfoButton.onClick.AddListener(ToggleCharacterInfo);
            }
            
            // Set available characters in tutorial manager
            if (tutorialManager != null && availableCharacters != null)
            {
                tutorialManager.SetAvailableCharacters(availableCharacters);
            }
            
            // Hide character info panel initially
            if (characterInfoPanel != null)
            {
                characterInfoPanel.SetActive(false);
            }
            
            if (debugMode)
            {
                Debug.Log("[MAIN MENU CHARACTER] Main menu character integration initialized");
            }
        }
        
        /// <summary>
        /// Toggle character info panel
        /// </summary>
        public void ToggleCharacterInfo()
        {
            if (isCharacterInfoActive)
            {
                HideCharacterInfo();
            }
            else
            {
                ShowCharacterInfo();
            }
        }
        
        /// <summary>
        /// Show character info panel
        /// </summary>
        public void ShowCharacterInfo()
        {
            if (tutorialManager == null)
            {
                Debug.LogWarning("[MAIN MENU CHARACTER] Tutorial manager not found!");
                return;
            }
            
            // Hide main menu panel
            if (mainMenuPanel != null)
            {
                mainMenuPanel.SetActive(false);
            }
            
            // Show character info panel
            if (characterInfoPanel != null)
            {
                characterInfoPanel.SetActive(true);
            }
            
            // Show character panel in tutorial manager (don't spawn new character)
            tutorialManager.ShowCharacterPanel();
            
            // Update button text
            if (characterInfoButtonText != null)
            {
                characterInfoButtonText.text = "Back to Menu";
            }
            
            isCharacterInfoActive = true;
            
            if (debugMode)
            {
                Debug.Log("[MAIN MENU CHARACTER] Character info panel shown");
            }
        }
        
        /// <summary>
        /// Hide character info panel
        /// </summary>
        public void HideCharacterInfo()
        {
            // Hide character info panel
            if (characterInfoPanel != null)
            {
                characterInfoPanel.SetActive(false);
            }
            
            // Show main menu panel
            if (mainMenuPanel != null)
            {
                mainMenuPanel.SetActive(true);
            }
            
            // Hide character panel in tutorial manager (keep character spawned)
            if (tutorialManager != null)
            {
                tutorialManager.HideCharacterPanel();
            }
            
            // Update button text
            if (characterInfoButtonText != null)
            {
                characterInfoButtonText.text = "Character Info";
            }
            
            isCharacterInfoActive = false;
            
            if (debugMode)
            {
                Debug.Log("[MAIN MENU CHARACTER] Character info panel hidden");
            }
        }
        
        /// <summary>
        /// Show character info for specific character
        /// </summary>
        public void ShowCharacterInfo(CharacterData characterData)
        {
            if (tutorialManager == null)
            {
                Debug.LogWarning("[MAIN MENU CHARACTER] Tutorial manager not found!");
                return;
            }
            
            // Hide main menu panel
            if (mainMenuPanel != null)
            {
                mainMenuPanel.SetActive(false);
            }
            
            // Show character info panel
            if (characterInfoPanel != null)
            {
                characterInfoPanel.SetActive(true);
            }
            
            // Show character panel in tutorial manager
            tutorialManager.ShowCharacterPanel(characterData);
            
            // Update button text
            if (characterInfoButtonText != null)
            {
                characterInfoButtonText.text = "Back to Menu";
            }
            
            isCharacterInfoActive = true;
            
            if (debugMode)
            {
                Debug.Log($"[MAIN MENU CHARACTER] Character info shown for {characterData.characterName}");
            }
        }
        
        /// <summary>
        /// Check if character info is active
        /// </summary>
        public bool IsCharacterInfoActive()
        {
            return isCharacterInfoActive;
        }
        
        /// <summary>
        /// Set available characters
        /// </summary>
        public void SetAvailableCharacters(CharacterData[] characters)
        {
            availableCharacters = characters;
            
            if (tutorialManager != null)
            {
                tutorialManager.SetAvailableCharacters(characters);
            }
            
            if (debugMode)
            {
                Debug.Log($"[MAIN MENU CHARACTER] Set {characters?.Length ?? 0} available characters");
            }
        }
        
        /// <summary>
        /// Get current character
        /// </summary>
        public CharacterData GetCurrentCharacter()
        {
            if (tutorialManager != null)
            {
                return tutorialManager.GetCurrentCharacter();
            }
            
            return null;
        }
        
        /// <summary>
        /// Go to previous character
        /// </summary>
        public void PreviousCharacter()
        {
            if (tutorialManager != null)
            {
                tutorialManager.PreviousCharacter();
            }
        }
        
        /// <summary>
        /// Go to next character
        /// </summary>
        public void NextCharacter()
        {
            if (tutorialManager != null)
            {
                tutorialManager.NextCharacter();
            }
        }
        
        /// <summary>
        /// Get current spawned character
        /// </summary>
        public GameObject GetCurrentSpawnedCharacter()
        {
            if (tutorialManager != null)
            {
                return tutorialManager.GetCurrentSpawnedCharacter();
            }
            return null;
        }
        
        /// <summary>
        /// Check if character is currently spawned
        /// </summary>
        public bool IsCharacterSpawned()
        {
            if (tutorialManager != null)
            {
                return tutorialManager.IsCharacterSpawned();
            }
            return false;
        }
    }
}
