using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== MOBILE CONTROL BUTTON ====================
public class MobileControlButton : MonoBehaviour
{
    [Header("Button Settings")]
    public string buttonName = "Button";
    public bool useHoldDetection = false;
    public float holdThreshold = 0.1f;

    private bool isPressed = false;
    private bool wasPressed = false;
    private bool isHeld = false;
    private float pressTime = 0f;

    // UI Components
    private UnityEngine.UI.Button button;
    private UnityEngine.UI.Image buttonImage;

    // Visual feedback
    [Header("Visual Feedback")]
    public Color normalColor = Color.white;
    public Color pressedColor = Color.gray;
    public Color heldColor = Color.green;
    public bool enableScaleEffect = true;
    public float scaleMultiplier = 0.9f;

    private Vector3 originalScale;
    private Color originalColor;

    void Awake()
    {
        button = GetComponent<UnityEngine.UI.Button>();
        buttonImage = GetComponent<UnityEngine.UI.Image>();

        originalScale = transform.localScale;
        originalColor = buttonImage != null ? buttonImage.color : normalColor;

        // Setup button events
        if (button != null)
        {
            button.onClick.AddListener(OnButtonClick);
        }

        // Setup pointer events for hold detection
        var eventTrigger = GetComponent<UnityEngine.EventSystems.EventTrigger>();
        if (eventTrigger == null)
        {
            eventTrigger = gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        }

        // Pointer down
        var pointerDown = new UnityEngine.EventSystems.EventTrigger.Entry();
        pointerDown.eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown;
        pointerDown.callback.AddListener((data) => OnPointerDown());
        eventTrigger.triggers.Add(pointerDown);

        // Pointer up
        var pointerUp = new UnityEngine.EventSystems.EventTrigger.Entry();
        pointerUp.eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp;
        pointerUp.callback.AddListener((data) => OnPointerUp());
        eventTrigger.triggers.Add(pointerUp);
    }

    void Update()
    {
        // Update held state
        if (isPressed && useHoldDetection)
        {
            if (Time.time - pressTime >= holdThreshold && !isHeld)
            {
                isHeld = true;
                OnButtonHeld();
            }
        }

        // Reset wasPressed flag after frame
        if (wasPressed)
        {
            wasPressed = false;
        }
    }

    void OnPointerDown()
    {
        isPressed = true;
        wasPressed = true;
        pressTime = Time.time;

        // Visual feedback
        ApplyPressedVisuals();

        // Haptic feedback
#if UNITY_ANDROID || UNITY_IOS
        if (Application.isMobilePlatform)
        {
            Handheld.Vibrate();
        }
#endif
    }

    void OnPointerUp()
    {
        isPressed = false;
        isHeld = false;

        // Visual feedback
        ApplyNormalVisuals();
    }

    void OnButtonClick()
    {
        // Handle button click logic here if needed
    }

    void OnButtonHeld()
    {
        // Visual feedback for held state
        ApplyHeldVisuals();
    }

    public bool WasPressed()
    {
        return wasPressed;
    }

    public bool IsPressed()
    {
        return isPressed;
    }

    public bool IsHeld()
    {
        return isHeld;
    }

    void ApplyNormalVisuals()
    {
        if (buttonImage != null)
        {
            buttonImage.color = normalColor;
        }

        if (enableScaleEffect)
        {
            transform.localScale = originalScale;
        }
    }

    void ApplyPressedVisuals()
    {
        if (buttonImage != null)
        {
            buttonImage.color = pressedColor;
        }

        if (enableScaleEffect)
        {
            transform.localScale = originalScale * scaleMultiplier;
        }
    }

    void ApplyHeldVisuals()
    {
        if (buttonImage != null)
        {
            buttonImage.color = heldColor;
        }
    }
}