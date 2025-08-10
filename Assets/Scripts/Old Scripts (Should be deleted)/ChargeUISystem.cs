using UnityEngine;
using UnityEngine.UI;

public class ChargeUISystem : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Canvas uiCanvas;
    [SerializeField] private GameObject chargeMeterPanel;
    [SerializeField] private Image chargeFillImage;
    [SerializeField] private Text chargeText;
    [SerializeField] private RectTransform chargeBarTransform;

    [Header("Visual Settings")]
    [SerializeField] private Color lowChargeColor = Color.white;
    [SerializeField] private Color mediumChargeColor = Color.yellow;
    [SerializeField] private Color highChargeColor = Color.red;
    [SerializeField] private Color maxChargeColor = Color.cyan;
    [SerializeField] private float pulseIntensity = 0.3f;

    [Header("Positioning")]
    [SerializeField] private Vector3 uiOffset = new Vector3(0, 100, 0);
    [SerializeField] private bool followPlayer = true;

    // References
    private ChargedThrowSystem chargedThrowSystem;
    private CharacterController player;
    private Camera mainCamera;

    // State
    private bool isVisible = false;

    void Awake()
    {
        mainCamera = Camera.main;

        // Find player and charged throw system
        player = FindObjectOfType<CharacterController>();
        if (player != null)
        {
            chargedThrowSystem = player.GetComponent<ChargedThrowSystem>();
        }

        CreateUI();
    }

    void CreateUI()
    {
        if (uiCanvas == null)
        {
            // Create canvas if not assigned
            GameObject canvasObj = new GameObject("ChargeUI_Canvas");
            uiCanvas = canvasObj.AddComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            uiCanvas.sortingOrder = 100;

            // Add CanvasScaler
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        if (chargeMeterPanel == null)
        {
            CreateChargeMeter();
        }

        // Start hidden
        SetVisible(false);
    }

    void CreateChargeMeter()
    {
        // Create charge meter panel
        GameObject panel = new GameObject("ChargeMeterPanel");
        panel.transform.SetParent(uiCanvas.transform);
        chargeMeterPanel = panel;

        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(200, 30);
        chargeBarTransform = panelRect;

        // Background
        Image bgImage = panel.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.7f);

        // Fill bar
        GameObject fillObj = new GameObject("ChargeFill");
        fillObj.transform.SetParent(panel.transform);

        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        chargeFillImage = fillObj.AddComponent<Image>();
        chargeFillImage.color = lowChargeColor;
        chargeFillImage.type = Image.Type.Filled;
        chargeFillImage.fillMethod = Image.FillMethod.Horizontal;

        // Charge text
        GameObject textObj = new GameObject("ChargeText");
        textObj.transform.SetParent(panel.transform);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        chargeText = textObj.AddComponent<Text>();
        chargeText.text = "CHARGING";
        chargeText.color = Color.white;
        chargeText.alignment = TextAnchor.MiddleCenter;
        chargeText.fontSize = 14;

        // Try to find a font
        //chargeText.font = Resources.GetBuiltinResource<Font>("LiberationSans.ttf");
    }

    void Update()
    {
        UpdateChargeDisplay();
        UpdateUIPosition();
    }

    void UpdateChargeDisplay()
    {
        if (chargedThrowSystem == null) return;

        bool shouldShow = chargedThrowSystem.IsCharging();

        if (shouldShow != isVisible)
        {
            SetVisible(shouldShow);
        }

        if (shouldShow)
        {
            float chargeProgress = chargedThrowSystem.GetChargeProgress();
            float chargePower = chargedThrowSystem.GetChargePower();

            // Update fill amount
            chargeFillImage.fillAmount = chargeProgress;

            // Update color based on charge level
            Color chargeColor = GetChargeColor(chargeProgress);
            chargeFillImage.color = chargeColor;

            // Add pulsing effect when near max
            if (chargeProgress >= 0.8f)
            {
                float pulse = Mathf.Sin(Time.time * 10f) * pulseIntensity;
                chargeFillImage.color = Color.Lerp(chargeColor, Color.white, pulse);
            }

            // Update text
            chargeText.text = $"POWER: {chargePower:F1}x";

            // Scale effect when maxed
            if (chargeProgress >= 0.99f)
            {
                float scale = 1f + Mathf.Sin(Time.time * 8f) * 0.1f;
                chargeBarTransform.localScale = Vector3.one * scale;
            }
            else
            {
                chargeBarTransform.localScale = Vector3.one;
            }
        }
    }

    Color GetChargeColor(float progress)
    {
        if (progress >= 0.99f)
            return maxChargeColor;
        else if (progress >= 0.66f)
            return Color.Lerp(mediumChargeColor, highChargeColor, (progress - 0.66f) / 0.33f);
        else if (progress >= 0.33f)
            return Color.Lerp(lowChargeColor, mediumChargeColor, (progress - 0.33f) / 0.33f);
        else
            return lowChargeColor;
    }

    void UpdateUIPosition()
    {
        if (!followPlayer || player == null || mainCamera == null || chargeBarTransform == null) return;

        // Convert world position to screen position
        Vector3 worldPos = player.transform.position + uiOffset;
        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

        // Update UI position
        chargeBarTransform.position = screenPos;
    }

    void SetVisible(bool visible)
    {
        isVisible = visible;
        if (chargeMeterPanel != null)
        {
            chargeMeterPanel.SetActive(visible);
        }
    }

    // Public methods
    public void SetPlayer(CharacterController newPlayer)
    {
        player = newPlayer;
        if (player != null)
        {
            chargedThrowSystem = player.GetComponent<ChargedThrowSystem>();
        }
    }
}