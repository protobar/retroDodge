using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

/// <summary>
/// AFK UI Manager using RPCs for proper network synchronization
/// Based on Photon best practices for UI synchronization
/// </summary>
public class AFKUIManager : MonoBehaviourPun
{
    [Header("UI References - Assign these in Inspector")]
    [SerializeField] private TextMeshProUGUI afkMessageText;
    [SerializeField] private GameObject afkMessagePanel;
    [SerializeField] private TextMeshProUGUI afkStatusText;
    
    [Header("Settings")]
    [SerializeField] private float messageDuration = 3f;
    [SerializeField] private Color afkMessageColor = Color.red;
    [SerializeField] private Color normalMessageColor = Color.green;
    
    // Message tracking
    private string currentMessage = "";
    private bool isShowingMessage = false;
    
    void Start()
    {
        // Hide message panel initially
        if (afkMessagePanel != null)
        {
            afkMessagePanel.SetActive(false);
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // RPC METHODS - These are called across the network
    // ═══════════════════════════════════════════════════════════════
    
    [PunRPC]
    void ShowAFKMessageRPC(string playerName, bool isAFK)
    {
        string message = isAFK ? 
            $"{playerName} is AFK and taking damage!" : 
            $"{playerName} is active again!";
            
        Color color = isAFK ? afkMessageColor : normalMessageColor;
        
        ShowMessageLocally(message, color);
        
        Debug.Log($"[AFK UI RPC] {message}");
    }
    
    [PunRPC]
    void UpdateAFKStatusRPC(string playerName, bool isAFK)
    {
        if (afkStatusText != null)
        {
            if (isAFK)
            {
                afkStatusText.text = $"{playerName} - AFK (Taking Damage)";
                afkStatusText.color = afkMessageColor;
            }
            else
            {
                afkStatusText.text = $"{playerName} - Active";
                afkStatusText.color = normalMessageColor;
            }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // PUBLIC METHODS - Called by AFK detectors
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Show AFK message for a specific player (called by AFK detector)
    /// </summary>
    public void ShowAFKMessage(string playerName, bool isAFK)
    {
        // Send RPC to all clients
        photonView.RPC("ShowAFKMessageRPC", RpcTarget.All, playerName, isAFK);
    }
    
    /// <summary>
    /// Update AFK status for a specific player (called by AFK detector)
    /// </summary>
    public void UpdateAFKStatus(string playerName, bool isAFK)
    {
        // Send RPC to all clients
        photonView.RPC("UpdateAFKStatusRPC", RpcTarget.All, playerName, isAFK);
    }
    
    // ═══════════════════════════════════════════════════════════════
    // LOCAL UI METHODS
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Show message locally
    /// </summary>
    void ShowMessageLocally(string message, Color color)
    {
        if (afkMessageText == null || afkMessagePanel == null) 
        {
            Debug.LogWarning("[AFK UI Manager] Missing UI references! Check afkMessageText and afkMessagePanel");
            return;
        }
        
        // Cancel previous message if showing
        if (isShowingMessage)
        {
            CancelInvoke(nameof(HideMessage));
        }
        
        afkMessageText.text = message;
        afkMessageText.color = color;
        afkMessagePanel.SetActive(true);
        isShowingMessage = true;
        
        // Hide message after duration
        Invoke(nameof(HideMessage), messageDuration);
        
        Debug.Log($"[AFK UI Manager] Showing message: {message}");
    }
    
    /// <summary>
    /// Hide the message panel
    /// </summary>
    void HideMessage()
    {
        if (afkMessagePanel != null)
        {
            afkMessagePanel.SetActive(false);
            isShowingMessage = false;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // PUBLIC API
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Set UI references manually
    /// </summary>
    public void SetUIReferences(TextMeshProUGUI messageText, GameObject messagePanel, TextMeshProUGUI statusText)
    {
        afkMessageText = messageText;
        afkMessagePanel = messagePanel;
        afkStatusText = statusText;
    }
    
    /// <summary>
    /// Show custom message
    /// </summary>
    public void ShowCustomMessage(string message, Color color)
    {
        ShowMessageLocally(message, color);
    }
}
