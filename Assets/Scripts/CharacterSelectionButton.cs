using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Individual character selection button
/// Handles display and selection feedback
/// </summary>
public class CharacterSelectionButton : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Image characterIcon;
    [SerializeField] private TextMeshProUGUI characterName;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image highlightBorder;
    [SerializeField] private GameObject selectedIndicator;

    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightColor = Color.yellow;
    [SerializeField] private Color selectedColor = Color.green;
    [SerializeField] private float highlightScale = 1.1f;

    [Header("Animation")]
    [SerializeField] private float animationDuration = 0.2f;
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 1, 1, 1);

    // Component references
    private Button button;

    // Character data
    private CharacterData character;
    private int characterIndex;

    // State
    private bool isHighlighted = false;
    private bool isSelected = false;
    private Vector3 originalScale;

    void Awake()
    {
        button = GetComponent<Button>();
        originalScale = transform.localScale;
    }

    /// <summary>
    /// Initialize the button with character data
    /// </summary>
    public void Initialize(CharacterData characterData, int index)
    {
        character = characterData;
        characterIndex = index;

        Debug.Log($"=== INITIALIZING BUTTON {index} ===");
        Debug.Log($"Character Data: {(characterData != null ? characterData.name : "NULL")}");
        Debug.Log($"Character Name: {(characterData != null ? characterData.characterName : "NULL")}");
        Debug.Log($"Character Icon Sprite: {(characterData?.characterIcon != null ? characterData.characterIcon.name : "NULL")}");
        Debug.Log($"CharacterIcon Image Component: {(characterIcon != null ? characterIcon.gameObject.name : "NULL")}");

        SetupVisuals();
        SetupButton();
    }

    void SetupVisuals()
    {
        if (character == null)
        {
            Debug.LogError($"❌ Character data is NULL on {gameObject.name}!");
            return;
        }

        Debug.Log($"--- Setting up visuals for {character.characterName} ---");

        // Set character icon - THIS IS THE CRITICAL PART
        if (characterIcon != null)
        {
            Debug.Log($"CharacterIcon Image found on: {characterIcon.gameObject.name}");

            if (character.characterIcon != null)
            {
                characterIcon.sprite = character.characterIcon;
                characterIcon.enabled = true;
                characterIcon.color = Color.white; // Make sure it's visible

                Debug.Log($"✓ SUCCESS! Set sprite '{character.characterIcon.name}' to {characterIcon.gameObject.name}");
                Debug.Log($"   Sprite size: {character.characterIcon.rect.width}x{character.characterIcon.rect.height}");
                Debug.Log($"   Image enabled: {characterIcon.enabled}");
                Debug.Log($"   Image color: {characterIcon.color}");
            }
            else
            {
                Debug.LogError($"❌ Character '{character.characterName}' has NO characterIcon sprite assigned in CharacterData!");
                Debug.LogError($"   Please assign a sprite to the 'characterIcon' field in the CharacterData ScriptableObject!");
            }
        }
        else
        {
            Debug.LogError($"❌ characterIcon Image component is NULL! Assign it in the prefab Inspector!");
        }

        // Set character name
        if (characterName != null && !string.IsNullOrEmpty(character.characterName))
        {
            characterName.text = character.characterName;
            Debug.Log($"✓ Set character name: {character.characterName}");
        }

        // Set character color
        if (backgroundImage != null)
        {
            backgroundImage.color = character.characterColor;
            Debug.Log($"✓ Set background color: {character.characterColor}");
        }

        // Initialize highlight state
        SetHighlighted(false);
        SetSelected(false);

        Debug.Log($"=== SETUP COMPLETE for {character.characterName} ===\n");
    }

    void SetupButton()
    {
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
        }
    }

    /// <summary>
    /// Set highlight state for keyboard navigation
    /// </summary>
    public void SetHighlighted(bool highlighted)
    {
        isHighlighted = highlighted;
        UpdateVisualState();

        if (highlighted)
        {
            StartCoroutine(AnimateHighlight());
        }
    }

    /// <summary>
    /// Set selected state when character is chosen
    /// </summary>
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        UpdateVisualState();

        if (selected)
        {
            StartCoroutine(AnimateSelection());
        }
    }

    void UpdateVisualState()
    {
        // Update border color
        if (highlightBorder != null)
        {
            if (isSelected)
            {
                highlightBorder.color = selectedColor;
                highlightBorder.gameObject.SetActive(true);
            }
            else if (isHighlighted)
            {
                highlightBorder.color = highlightColor;
                highlightBorder.gameObject.SetActive(true);
            }
            else
            {
                highlightBorder.gameObject.SetActive(false);
            }
        }

        // Update selected indicator
        if (selectedIndicator != null)
        {
            selectedIndicator.SetActive(isSelected);
        }

        // Update background color
        if (backgroundImage != null && character != null)
        {
            Color targetColor = character.characterColor;

            if (isSelected)
            {
                targetColor = Color.Lerp(targetColor, selectedColor, 0.3f);
            }
            else if (isHighlighted)
            {
                targetColor = Color.Lerp(targetColor, highlightColor, 0.2f);
            }

            backgroundImage.color = targetColor;
        }
    }

    System.Collections.IEnumerator AnimateHighlight()
    {
        if (!isHighlighted) yield break;

        float elapsed = 0f;
        Vector3 targetScale = originalScale * highlightScale;

        while (elapsed < animationDuration && isHighlighted)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            float curveValue = scaleCurve.Evaluate(t);

            transform.localScale = Vector3.Lerp(originalScale, targetScale, curveValue);
            yield return null;
        }

        if (!isHighlighted)
        {
            transform.localScale = originalScale;
        }
    }

    System.Collections.IEnumerator AnimateSelection()
    {
        if (!isSelected) yield break;

        // Pulse animation for selection
        float elapsed = 0f;
        Vector3 pulseScale = originalScale * 1.2f;

        // Scale up
        while (elapsed < animationDuration / 2)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (animationDuration / 2);
            transform.localScale = Vector3.Lerp(originalScale, pulseScale, t);
            yield return null;
        }

        elapsed = 0f;

        // Scale back down
        while (elapsed < animationDuration / 2)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (animationDuration / 2);
            transform.localScale = Vector3.Lerp(pulseScale, originalScale, t);
            yield return null;
        }

        transform.localScale = originalScale;
    }

    /// <summary>
    /// Get the character data associated with this button
    /// </summary>
    public CharacterData GetCharacterData()
    {
        return character;
    }

    /// <summary>
    /// Get the character index
    /// </summary>
    public int GetCharacterIndex()
    {
        return characterIndex;
    }

    void OnDisable()
    {
        // Reset scale when disabled
        if (transform != null)
        {
            transform.localScale = originalScale;
        }
    }
}