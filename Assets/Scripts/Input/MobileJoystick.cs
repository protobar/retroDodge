using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== MOBILE JOYSTICK ====================
public class MobileJoystick : MonoBehaviour
{
    [Header("Joystick Settings")]
    public float handleRange = 50f;
    public float deadZone = 0.1f;
    public bool snapToOrigin = true;

    // UI Components
    public RectTransform background;
    public RectTransform handle;

    private Vector2 inputVector = Vector2.zero;
    private bool isDragging = false;
    private Vector2 centerPosition;

    // Visual feedback
    [Header("Visual Settings")]
    public Color normalColor = Color.white;
    public Color activeColor = Color.cyan;
    private UnityEngine.UI.Image backgroundImage;
    private UnityEngine.UI.Image handleImage;

    void Awake()
    {
        if (background == null)
            background = GetComponent<RectTransform>();

        if (handle == null)
            handle = transform.GetChild(0).GetComponent<RectTransform>();

        backgroundImage = background.GetComponent<UnityEngine.UI.Image>();
        handleImage = handle.GetComponent<UnityEngine.UI.Image>();

        centerPosition = background.anchoredPosition;

        // Setup event triggers
        SetupEventTriggers();
    }

    void SetupEventTriggers()
    {
        var eventTrigger = background.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
        if (eventTrigger == null)
        {
            eventTrigger = background.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        }

        // Drag begin
        var dragBegin = new UnityEngine.EventSystems.EventTrigger.Entry();
        dragBegin.eventID = UnityEngine.EventSystems.EventTriggerType.BeginDrag;
        dragBegin.callback.AddListener((data) => OnDragBegin((UnityEngine.EventSystems.PointerEventData)data));
        eventTrigger.triggers.Add(dragBegin);

        // Drag
        var drag = new UnityEngine.EventSystems.EventTrigger.Entry();
        drag.eventID = UnityEngine.EventSystems.EventTriggerType.Drag;
        drag.callback.AddListener((data) => OnDrag((UnityEngine.EventSystems.PointerEventData)data));
        eventTrigger.triggers.Add(drag);

        // Drag end
        var dragEnd = new UnityEngine.EventSystems.EventTrigger.Entry();
        dragEnd.eventID = UnityEngine.EventSystems.EventTriggerType.EndDrag;
        dragEnd.callback.AddListener((data) => OnDragEnd((UnityEngine.EventSystems.PointerEventData)data));
        eventTrigger.triggers.Add(dragEnd);
    }

    void OnDragBegin(UnityEngine.EventSystems.PointerEventData eventData)
    {
        isDragging = true;

        // Visual feedback
        if (backgroundImage != null)
            backgroundImage.color = activeColor;
    }

    void OnDrag(UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (!isDragging) return;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background, eventData.position, eventData.pressEventCamera, out localPoint);

        // Clamp to handle range
        localPoint = Vector2.ClampMagnitude(localPoint, handleRange);

        // Update handle position
        handle.anchoredPosition = localPoint;

        // Calculate input vector
        inputVector = localPoint / handleRange;

        // Apply dead zone
        if (inputVector.magnitude < deadZone)
        {
            inputVector = Vector2.zero;
        }
    }

    void OnDragEnd(UnityEngine.EventSystems.PointerEventData eventData)
    {
        isDragging = false;
        inputVector = Vector2.zero;

        // Snap handle back to center
        if (snapToOrigin)
        {
            handle.anchoredPosition = Vector2.zero;
        }

        // Visual feedback
        if (backgroundImage != null)
            backgroundImage.color = normalColor;
    }

    public float GetHorizontalInput()
    {
        return inputVector.x;
    }

    public float GetVerticalInput()
    {
        return inputVector.y;
    }

    public Vector2 GetInputVector()
    {
        return inputVector;
    }

    public bool IsActive()
    {
        return isDragging;
    }
}

// ==================== INPUT SETTINGS ====================
[System.Serializable]
public class InputSettings
{
    [Header("General Settings")]
    public bool inputBufferEnabled = true;
    public float inputBufferTime = 0.1f;

    [Header("PC Controls")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode dodgeKey = KeyCode.LeftShift;
    public KeyCode throwKey = KeyCode.Mouse0;
    public KeyCode catchKey = KeyCode.Mouse1;
    public KeyCode ultimateKey = KeyCode.Q;

    [Header("Mobile Settings")]
    public float mobileInputSensitivity = 1.5f;
    public bool enableVibration = true;
    public bool enableHapticFeedback = true;

    [Header("Accessibility")]
    public bool largeButtons = false;
    public bool highContrast = false;
    public float buttonOpacity = 1f;
}