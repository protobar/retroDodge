using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RetroDodgeRumble.Tutorial
{
    /// <summary>
    /// Simple tutorial manager for FYP - UI-based tutorial with images
    /// </summary>
    public class SimpleTutorialManager : MonoBehaviour
    {
        [Header("Tutorial Panel")]
        [SerializeField] private GameObject tutorialPanel;
        [SerializeField] private GameObject characterPanel;
        
        [Header("Tutorial UI Elements")]
        [SerializeField] private Image tutorialImage;
        [SerializeField] private TextMeshProUGUI tutorialTitle;
        [SerializeField] private TextMeshProUGUI tutorialDescription;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private TextMeshProUGUI pageCounter;
        
        [Header("Character Panel UI Elements")]
        [SerializeField] private Image characterIcon;
        [SerializeField] private TextMeshProUGUI characterName;
        [SerializeField] private TextMeshProUGUI characterTagline;
        [SerializeField] private TextMeshProUGUI characterDescription;
        [SerializeField] private TextMeshProUGUI characterLore;
        [SerializeField] private TextMeshProUGUI ultimateDescription;
        [SerializeField] private TextMeshProUGUI trickDescription;
        [SerializeField] private TextMeshProUGUI treatDescription;
        [SerializeField] private Button characterCloseButton;
        [SerializeField] private Button previousCharacterButton;
        [SerializeField] private Button nextCharacterButton;
        [SerializeField] private TextMeshProUGUI characterCounter;
        
        [Header("Character Spawning")]
        [SerializeField] private Transform characterSpawnParent;
        [SerializeField] private Vector3 characterSpawnPosition = new Vector3(0, 0, 0);
        [SerializeField] private Vector3 characterSpawnRotation = new Vector3(0, 0, 0);
        [SerializeField] private Vector3 characterSpawnScale = new Vector3(1, 1, 1);
        [SerializeField] private GameObject[] characterPreviewPrefabs; // Separate preview prefabs with only animators
        
        [Header("Tutorial Content")]
        [SerializeField] private TutorialPage[] tutorialPages;
        [SerializeField] private int currentPage = 0;
        
        [Header("Character Data")]
        [SerializeField] private CharacterData[] availableCharacters;
        [SerializeField] private int currentCharacterIndex = 0;
        
        [Header("Settings")]
        [SerializeField] private bool debugMode = true;
        [SerializeField] private string newPlayerKey = "IsNewPlayer";
        [SerializeField] private string tutorialSeenKey = "TutorialSeen";
        
        // State
        private bool isTutorialActive = false;
        private bool isCharacterPanelActive = false;
        private GameObject currentSpawnedCharacter;
        private Animator currentCharacterAnimator;
        
        // Events
        public System.Action OnTutorialCompleted;
        public System.Action OnTutorialSkipped;
        
        void Start()
        {
            InitializeTutorial();
            
            // Spawn first character automatically
            SpawnFirstCharacter();
        }
        
        /// <summary>
        /// Initialize tutorial system
        /// </summary>
        void InitializeTutorial()
        {
            // Setup button listeners
            if (nextButton != null)
                nextButton.onClick.AddListener(NextPage);
            
            if (backButton != null)
                backButton.onClick.AddListener(PreviousPage);
            
            if (closeButton != null)
                closeButton.onClick.AddListener(CloseTutorial);
            
            if (skipButton != null)
                skipButton.onClick.AddListener(SkipTutorial);
            
            if (characterCloseButton != null)
                characterCloseButton.onClick.AddListener(CloseCharacterPanel);
            
            if (previousCharacterButton != null)
                previousCharacterButton.onClick.AddListener(PreviousCharacter);
            
            if (nextCharacterButton != null)
                nextCharacterButton.onClick.AddListener(NextCharacter);
            
            // Hide panels initially
            if (tutorialPanel != null)
                tutorialPanel.SetActive(false);
            
            if (characterPanel != null)
                characterPanel.SetActive(false);
            
            // Check if new player
            CheckForNewPlayer();
            
            if (debugMode)
            {
                Debug.Log("[SIMPLE TUTORIAL] Tutorial system initialized");
            }
        }
        
        /// <summary>
        /// Check if player is new and show tutorial
        /// </summary>
        void CheckForNewPlayer()
        {
            bool isNewPlayer = !PlayerPrefs.HasKey(newPlayerKey);
            bool tutorialSeen = PlayerPrefs.GetInt(tutorialSeenKey, 0) == 1;
            
            if (isNewPlayer && !tutorialSeen)
            {
                // Mark as not new player
                PlayerPrefs.SetInt(newPlayerKey, 1);
                PlayerPrefs.Save();
                
                // Show tutorial after short delay
                Invoke(nameof(ShowTutorial), 1f);
                
                if (debugMode)
                {
                    Debug.Log("[SIMPLE TUTORIAL] New player detected - showing tutorial");
                }
            }
        }
        
        /// <summary>
        /// Show tutorial panel
        /// </summary>
        public void ShowTutorial()
        {
            if (tutorialPanel == null) return;
            
            isTutorialActive = true;
            currentPage = 0;
            
            // Show tutorial panel
            tutorialPanel.SetActive(true);
            
            // Update first page
            UpdateTutorialPage();
            
            if (debugMode)
            {
                Debug.Log("[SIMPLE TUTORIAL] Tutorial panel shown");
            }
        }
        
        /// <summary>
        /// Hide tutorial panel
        /// </summary>
        public void HideTutorial()
        {
            if (tutorialPanel != null)
            {
                tutorialPanel.SetActive(false);
            }
            
            isTutorialActive = false;
            
            if (debugMode)
            {
                Debug.Log("[SIMPLE TUTORIAL] Tutorial panel hidden");
            }
        }
        
        /// <summary>
        /// Show character description panel
        /// </summary>
        public void ShowCharacterPanel(CharacterData characterData = null)
        {
            if (characterPanel == null) return;
            
            isCharacterPanelActive = true;
            
            // Use provided character or current character
            CharacterData targetCharacter = characterData;
            if (targetCharacter == null && availableCharacters != null && availableCharacters.Length > 0)
            {
                targetCharacter = availableCharacters[currentCharacterIndex];
            }
            
            if (targetCharacter == null)
            {
                Debug.LogWarning("[SIMPLE TUTORIAL] No character data available!");
                return;
            }
            
            // Update character information
            UpdateCharacterPanel(targetCharacter);
            
            // Update character navigation buttons
            UpdateCharacterNavigationButtons();
            
            // Show character panel
            characterPanel.SetActive(true);
            
            if (debugMode)
            {
                Debug.Log($"[SIMPLE TUTORIAL] Character panel shown for {targetCharacter.characterName}");
            }
        }
        
        /// <summary>
        /// Hide character panel
        /// </summary>
        public void HideCharacterPanel()
        {
            if (characterPanel != null)
            {
                characterPanel.SetActive(false);
            }
            
            isCharacterPanelActive = false;
            
            if (debugMode)
            {
                Debug.Log("[SIMPLE TUTORIAL] Character panel hidden");
            }
        }
        
        /// <summary>
        /// Spawn character preview
        /// </summary>
        void SpawnCharacterPreview(CharacterData characterData)
        {
            // Destroy current character first
            DestroyCurrentCharacter();
            
            // Get preview prefab for character
            GameObject previewPrefab = GetPreviewPrefabForCharacter(characterData);
            if (previewPrefab == null)
            {
                Debug.LogWarning($"[SIMPLE TUTORIAL] No preview prefab found for {characterData.characterName}");
                return;
            }
            
            // Spawn character preview
            if (characterSpawnParent != null)
            {
                currentSpawnedCharacter = Instantiate(previewPrefab, characterSpawnParent);
            }
            else
            {
                currentSpawnedCharacter = Instantiate(previewPrefab);
            }
            
            // Set position, rotation, and scale
            currentSpawnedCharacter.transform.position = characterSpawnPosition;
            currentSpawnedCharacter.transform.rotation = Quaternion.Euler(characterSpawnRotation);
            currentSpawnedCharacter.transform.localScale = characterSpawnScale;
            
            // Get animator (for reference, but don't control animations)
            currentCharacterAnimator = currentSpawnedCharacter.GetComponent<Animator>();
            
            if (debugMode)
            {
                Debug.Log($"[SIMPLE TUTORIAL] Spawned character preview: {characterData.characterName}");
            }
        }
        
        /// <summary>
        /// Get preview prefab for character
        /// </summary>
        GameObject GetPreviewPrefabForCharacter(CharacterData characterData)
        {
            if (characterPreviewPrefabs == null || characterPreviewPrefabs.Length == 0) return null;
            
            // Try to find by name
            for (int i = 0; i < characterPreviewPrefabs.Length; i++)
            {
                if (characterPreviewPrefabs[i] != null && 
                    characterPreviewPrefabs[i].name.Contains(characterData.characterName))
                {
                    return characterPreviewPrefabs[i];
                }
            }
            
            // Try to find by index
            int characterIndex = GetCharacterIndex(characterData);
            if (characterIndex >= 0 && characterIndex < characterPreviewPrefabs.Length)
            {
                return characterPreviewPrefabs[characterIndex];
            }
            
            return null;
        }
        
        /// <summary>
        /// Get character index
        /// </summary>
        int GetCharacterIndex(CharacterData characterData)
        {
            if (availableCharacters == null) return -1;
            
            for (int i = 0; i < availableCharacters.Length; i++)
            {
                if (availableCharacters[i] == characterData)
                {
                    return i;
                }
            }
            
            return -1;
        }
        
        /// <summary>
        /// Spawn first character automatically
        /// </summary>
        void SpawnFirstCharacter()
        {
            if (availableCharacters == null || availableCharacters.Length == 0) return;
            
            // Spawn first character
            SpawnCharacterPreview(availableCharacters[0]);
            
            if (debugMode)
            {
                Debug.Log($"[SIMPLE TUTORIAL] Spawned first character: {availableCharacters[0].characterName}");
            }
        }
        
        /// <summary>
        /// Destroy current spawned character
        /// </summary>
        void DestroyCurrentCharacter()
        {
            if (currentSpawnedCharacter != null)
            {
                DestroyImmediate(currentSpawnedCharacter);
                currentSpawnedCharacter = null;
                currentCharacterAnimator = null;
                
                if (debugMode)
                {
                    Debug.Log("[SIMPLE TUTORIAL] Destroyed current character");
                }
            }
        }
        
        /// <summary>
        /// Update tutorial page content
        /// </summary>
        void UpdateTutorialPage()
        {
            if (tutorialPages == null || currentPage >= tutorialPages.Length) return;
            
            TutorialPage page = tutorialPages[currentPage];
            
            // Update image
            if (tutorialImage != null && page.tutorialImage != null)
            {
                tutorialImage.sprite = page.tutorialImage;
            }
            
            // Update title
            if (tutorialTitle != null)
            {
                tutorialTitle.text = page.title;
            }
            
            // Update description
            if (tutorialDescription != null)
            {
                tutorialDescription.text = page.description;
            }
            
            // Update page counter
            if (pageCounter != null)
            {
                pageCounter.text = $"{currentPage + 1} / {tutorialPages.Length}";
            }
            
            // Update button states
            UpdateButtonStates();
        }
        
        /// <summary>
        /// Update character panel content
        /// </summary>
        void UpdateCharacterPanel(CharacterData characterData)
        {
            // Update character icon
            if (characterIcon != null && characterData.characterIcon != null)
            {
                characterIcon.sprite = characterData.characterIcon;
            }
            
            // Update character name
            if (characterName != null)
            {
                characterName.text = characterData.characterName;
            }
            
            // Update character tagline
            if (characterTagline != null)
            {
                characterTagline.text = characterData.characterTagline;
            }
            
            // Update character description
            if (characterDescription != null)
            {
                characterDescription.text = characterData.characterDescription;
            }
            
            // Update character lore
            if (characterLore != null)
            {
                characterLore.text = characterData.characterLore;
            }
            
            // Update ultimate description
            if (ultimateDescription != null)
            {
                ultimateDescription.text = GetUltimateDescription(characterData);
            }
            
            // Update trick description
            if (trickDescription != null)
            {
                trickDescription.text = GetTrickDescription(characterData);
            }
            
            // Update treat description
            if (treatDescription != null)
            {
                treatDescription.text = GetTreatDescription(characterData);
            }
        }
        
        /// <summary>
        /// Get ultimate ability description
        /// </summary>
        string GetUltimateDescription(CharacterData characterData)
        {
            switch (characterData.ultimateType)
            {
                case UltimateType.PowerThrow:
                    return $"<b>Power Throw</b>\n{characterData.ultimateDescription}\n\nPress <color=yellow>Q</color> to use when charged!";
                case UltimateType.MultiThrow:
                    return $"<b>Multi Throw</b>\n{characterData.ultimateDescription}\n\nPress <color=yellow>Q</color> to use when charged!";
                case UltimateType.Curveball:
                    return $"<b>Curveball</b>\n{characterData.ultimateDescription}\n\nPress <color=yellow>Q</color> to use when charged!";
                default:
                    return "Ultimate ability description not available.";
            }
        }
        
        /// <summary>
        /// Get trick ability description
        /// </summary>
        string GetTrickDescription(CharacterData characterData)
        {
            switch (characterData.trickType)
            {
                case TrickType.SlowSpeed:
                    return $"<b>Slow Speed</b>\n{characterData.trickDescription}\n\nPress <color=yellow>W</color> to use on opponent!";
                case TrickType.Freeze:
                    return $"<b>Freeze</b>\n{characterData.trickDescription}\n\nPress <color=yellow>W</color> to use on opponent!";
                case TrickType.InstantDamage:
                    return $"<b>Instant Damage</b>\n{characterData.trickDescription}\n\nPress <color=yellow>W</color> to use on opponent!";
                default:
                    return "Trick ability description not available.";
            }
        }
        
        /// <summary>
        /// Get treat ability description
        /// </summary>
        string GetTreatDescription(CharacterData characterData)
        {
            switch (characterData.treatType)
            {
                case TreatType.Shield:
                    return $"<b>Shield</b>\n{characterData.treatDescription}\n\nPress <color=yellow>E</color> to use on yourself!";
                case TreatType.Teleport:
                    return $"<b>Teleport</b>\n{characterData.treatDescription}\n\nPress <color=yellow>E</color> to use on yourself!";
                case TreatType.SpeedBoost:
                    return $"<b>Speed Boost</b>\n{characterData.treatDescription}\n\nPress <color=yellow>E</color> to use on yourself!";
                default:
                    return "Treat ability description not available.";
            }
        }
        
        /// <summary>
        /// Update button states based on current page
        /// </summary>
        void UpdateButtonStates()
        {
            if (backButton != null)
            {
                backButton.interactable = currentPage > 0;
            }
            
            if (nextButton != null)
            {
                nextButton.interactable = currentPage < tutorialPages.Length - 1;
            }
        }
        
        /// <summary>
        /// Go to next page
        /// </summary>
        public void NextPage()
        {
            if (currentPage < tutorialPages.Length - 1)
            {
                currentPage++;
                UpdateTutorialPage();
                
                if (debugMode)
                {
                    Debug.Log($"[SIMPLE TUTORIAL] Next page: {currentPage + 1}");
                }
            }
        }
        
        /// <summary>
        /// Go to previous page
        /// </summary>
        public void PreviousPage()
        {
            if (currentPage > 0)
            {
                currentPage--;
                UpdateTutorialPage();
                
                if (debugMode)
                {
                    Debug.Log($"[SIMPLE TUTORIAL] Previous page: {currentPage + 1}");
                }
            }
        }
        
        /// <summary>
        /// Close tutorial
        /// </summary>
        public void CloseTutorial()
        {
            HideTutorial();
            
            // Mark tutorial as seen
            PlayerPrefs.SetInt(tutorialSeenKey, 1);
            PlayerPrefs.Save();
            
            // Trigger completion event
            OnTutorialCompleted?.Invoke();
            
            if (debugMode)
            {
                Debug.Log("[SIMPLE TUTORIAL] Tutorial closed");
            }
        }
        
        /// <summary>
        /// Skip tutorial
        /// </summary>
        public void SkipTutorial()
        {
            HideTutorial();
            
            // Mark tutorial as seen
            PlayerPrefs.SetInt(tutorialSeenKey, 1);
            PlayerPrefs.Save();
            
            // Trigger skip event
            OnTutorialSkipped?.Invoke();
            
            if (debugMode)
            {
                Debug.Log("[SIMPLE TUTORIAL] Tutorial skipped");
            }
        }
        
        /// <summary>
        /// Close character panel
        /// </summary>
        public void CloseCharacterPanel()
        {
            HideCharacterPanel();
            
            if (debugMode)
            {
                Debug.Log("[SIMPLE TUTORIAL] Character panel closed");
            }
        }
        
        /// <summary>
        /// Go to previous character
        /// </summary>
        public void PreviousCharacter()
        {
            if (availableCharacters == null || availableCharacters.Length == 0) return;
            
            currentCharacterIndex--;
            if (currentCharacterIndex < 0)
            {
                currentCharacterIndex = availableCharacters.Length - 1;
            }
            
            // Spawn new character preview
            SpawnCharacterPreview(availableCharacters[currentCharacterIndex]);
            
            UpdateCharacterPanel(availableCharacters[currentCharacterIndex]);
            UpdateCharacterNavigationButtons();
            
            if (debugMode)
            {
                Debug.Log($"[SIMPLE TUTORIAL] Previous character: {availableCharacters[currentCharacterIndex].characterName}");
            }
        }
        
        /// <summary>
        /// Go to next character
        /// </summary>
        public void NextCharacter()
        {
            if (availableCharacters == null || availableCharacters.Length == 0) return;
            
            currentCharacterIndex++;
            if (currentCharacterIndex >= availableCharacters.Length)
            {
                currentCharacterIndex = 0;
            }
            
            // Spawn new character preview
            SpawnCharacterPreview(availableCharacters[currentCharacterIndex]);
            
            UpdateCharacterPanel(availableCharacters[currentCharacterIndex]);
            UpdateCharacterNavigationButtons();
            
            if (debugMode)
            {
                Debug.Log($"[SIMPLE TUTORIAL] Next character: {availableCharacters[currentCharacterIndex].characterName}");
            }
        }
        
        /// <summary>
        /// Update character navigation buttons
        /// </summary>
        void UpdateCharacterNavigationButtons()
        {
            if (availableCharacters == null || availableCharacters.Length == 0) return;
            
            // Update character counter
            if (characterCounter != null)
            {
                characterCounter.text = $"{currentCharacterIndex + 1} / {availableCharacters.Length}";
            }
            
            // Navigation buttons are always enabled for cycling
            if (previousCharacterButton != null)
            {
                previousCharacterButton.interactable = true;
            }
            
            if (nextCharacterButton != null)
            {
                nextCharacterButton.interactable = true;
            }
        }
        
        /// <summary>
        /// Check if tutorial is active
        /// </summary>
        public bool IsTutorialActive()
        {
            return isTutorialActive;
        }
        
        /// <summary>
        /// Check if character panel is active
        /// </summary>
        public bool IsCharacterPanelActive()
        {
            return isCharacterPanelActive;
        }
        
        /// <summary>
        /// Check if tutorial has been seen
        /// </summary>
        public bool IsTutorialSeen()
        {
            return PlayerPrefs.GetInt(tutorialSeenKey, 0) == 1;
        }
        
        /// <summary>
        /// Reset tutorial status (for testing)
        /// </summary>
        public void ResetTutorialStatus()
        {
            PlayerPrefs.DeleteKey(tutorialSeenKey);
            PlayerPrefs.DeleteKey(newPlayerKey);
            PlayerPrefs.Save();
            
            if (debugMode)
            {
                Debug.Log("[SIMPLE TUTORIAL] Tutorial status reset");
            }
        }
        
        /// <summary>
        /// Get current character data
        /// </summary>
        public CharacterData GetCurrentCharacter()
        {
            if (availableCharacters == null || availableCharacters.Length == 0) return null;
            return availableCharacters[currentCharacterIndex];
        }
        
        /// <summary>
        /// Set available characters
        /// </summary>
        public void SetAvailableCharacters(CharacterData[] characters)
        {
            availableCharacters = characters;
            currentCharacterIndex = 0;
            
            if (debugMode)
            {
                Debug.Log($"[SIMPLE TUTORIAL] Set {characters?.Length ?? 0} available characters");
            }
        }
        
        /// <summary>
        /// Show character panel with specific character
        /// </summary>
        public void ShowCharacterPanelAtIndex(int characterIndex)
        {
            if (availableCharacters == null || characterIndex < 0 || characterIndex >= availableCharacters.Length) return;
            
            currentCharacterIndex = characterIndex;
            ShowCharacterPanel();
        }
        
        /// <summary>
        /// Get current spawned character
        /// </summary>
        public GameObject GetCurrentSpawnedCharacter()
        {
            return currentSpawnedCharacter;
        }
        
        /// <summary>
        /// Check if character is currently spawned
        /// </summary>
        public bool IsCharacterSpawned()
        {
            return currentSpawnedCharacter != null;
        }
    }
    
    /// <summary>
    /// Tutorial page data structure
    /// </summary>
    [System.Serializable]
    public class TutorialPage
    {
        [Header("Page Content")]
        public string title;
        public string description;
        public Sprite tutorialImage;
        
        [Header("Page Settings")]
        public bool showNextButton = true;
        public bool showBackButton = true;
        public bool showSkipButton = true;
    }
}
