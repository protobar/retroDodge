using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ==================== CHARACTER SELECT UI ====================
public class CharacterSelectUI : MonoBehaviour
{
    [Header("Character Buttons")]
    public Button[] characterButtons;
    public GameObject[] characterPreviews;

    [Header("Character Info")]
    public Text characterNameText;
    public Slider healthSlider;
    public Slider speedSlider;
    public Slider damageSlider;
    public Text ultimateDescriptionText;

    [Header("Animation")]
    public Animator characterPreviewAnimator;

    private int selectedCharacterIndex = 0;
    private CharacterStats[] characterStats;

    void Start()
    {
        // Load character stats
        LoadCharacterStats();

        // Initialize UI
        SetupCharacterButtons();
        SelectCharacter(0);
    }

    void LoadCharacterStats()
    {
        characterStats = new CharacterStats[3];
        characterStats[0] = Resources.Load<CharacterStats>("Characters/GrudgeStats");
        characterStats[1] = Resources.Load<CharacterStats>("Characters/NovaStats");
        characterStats[2] = Resources.Load<CharacterStats>("Characters/EchoStats");
    }

    void SetupCharacterButtons()
    {
        for (int i = 0; i < characterButtons.Length; i++)
        {
            int index = i; // Capture for closure
            characterButtons[i].onClick.AddListener(() => SelectCharacter(index));
        }
    }

    void SelectCharacter(int index)
    {
        selectedCharacterIndex = index;

        // Update character preview
        UpdateCharacterPreview();

        // Update character info
        UpdateCharacterInfo();

        // Update button visuals
        UpdateButtonVisuals();

        AudioManager.Instance?.PlaySound("CharacterHover");
    }

    void UpdateCharacterPreview()
    {
        // Hide all previews
        foreach (var preview in characterPreviews)
        {
            preview.SetActive(false);
        }

        // Show selected preview
        if (selectedCharacterIndex < characterPreviews.Length)
        {
            characterPreviews[selectedCharacterIndex].SetActive(true);
        }

        // Trigger animation
        if (characterPreviewAnimator != null)
        {
            characterPreviewAnimator.SetTrigger("CharacterSelected");
        }
    }

    void UpdateCharacterInfo()
    {
        if (selectedCharacterIndex >= characterStats.Length || characterStats[selectedCharacterIndex] == null)
            return;

        CharacterStats stats = characterStats[selectedCharacterIndex];

        if (characterNameText != null)
            characterNameText.text = stats.characterName;

        if (healthSlider != null)
        {
            healthSlider.value = stats.maxHealth / 120f; // Normalize to Grudge's health
        }

        if (speedSlider != null)
        {
            speedSlider.value = stats.movementSpeed / 6.5f; // Normalize to Nova's speed
        }

        if (damageSlider != null)
        {
            damageSlider.value = stats.damageMultiplier / 1.3f; // Normalize to Grudge's damage
        }

        if (ultimateDescriptionText != null)
        {
            string ultimateDescription = GetUltimateDescription(selectedCharacterIndex);
            ultimateDescriptionText.text = ultimateDescription;
        }
    }

    string GetUltimateDescription(int characterIndex)
    {
        switch (characterIndex)
        {
            case 0: // Grudge
                return "METEOR STRIKE\nMassive fireball dealing 30 damage with 0.5s wind-up";
            case 1: // Nova
                return "THUNDER BARRAGE\n5 rapid shots dealing 5 damage each";
            case 2: // Echo
                return "MIRROR MAZE\nBall splits into 3 - only one deals 25 damage";
            default:
                return "";
        }
    }

    void UpdateButtonVisuals()
    {
        for (int i = 0; i < characterButtons.Length; i++)
        {
            if (characterButtons[i] != null)
            {
                ColorBlock colors = characterButtons[i].colors;
                colors.normalColor = (i == selectedCharacterIndex) ? Color.yellow : Color.white;
                characterButtons[i].colors = colors;

                // Scale effect
                float scale = (i == selectedCharacterIndex) ? 1.1f : 1f;
                characterButtons[i].transform.localScale = Vector3.one * scale;
            }
        }
    }

    public void ConfirmSelection()
    {
        // Save selection
        string characterName = characterStats[selectedCharacterIndex].characterName;
        PlayerPrefs.SetString("SelectedCharacter", characterName);
        PlayerPrefs.Save();

        AudioManager.Instance?.PlaySound("CharacterConfirm");

        // Continue to matchmaking or return to menu
        // This would be handled by the MenuManager
    }
}