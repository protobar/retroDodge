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
    private CharacterSelectionManager selectionManager;

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

        // Auto-find components if not assigned
        if (characterIcon == null)
            characterIcon = GetComponentInChildren<Image>();

        if (characterName == null)
            characterName = GetComponentInChildren<TextMeshProUGUI>();
    }

    /// <summary>
    /// Initialize the button with character data
    /// </summary>
    public void Initialize(CharacterData characterData, int index, CharacterSelectionManager manager)
    {
        character = characterData;
        characterIndex = index;
        selectionManager = manager;

        SetupVisuals();
        SetupButton();
    }

    void SetupVisuals()
    {
        if (character == null) return;

        // Set character icon
        if (characterIcon != null && character.characterIcon != null)
        {
            characterIcon.sprite = character.characterIcon;
        }

        // Set character name
        if (characterName != null)
        {
            characterName.text = character.characterName;
        }

        // Set character color
        if (backgroundImage != null)
        {
            backgroundImage.color = character.characterColor;
        }

        // Initialize highlight state
        SetHighlighted(false);
        SetSelected(false);
    }

    void SetupButton()
    {
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnButtonClicked);
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
        if (backgroundImage != null)
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

    void OnButtonClicked()
    {
        if (selectionManager != null)
        {
            // Let the selection manager handle the selection logic
            selectionManager.OnCharacterSelected(characterIndex);
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