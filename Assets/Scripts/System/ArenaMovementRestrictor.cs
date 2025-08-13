using UnityEngine;
using System.Collections;

/// <summary>
/// Enhanced Arena Movement Restriction System with Teleport Override
/// Supports temporary bounds disabling for special abilities like teleport
/// </summary>
public class ArenaMovementRestrictor : MonoBehaviour
{
    [Header("Arena Settings")]
    [SerializeField] private float arenaLeftBound = -10f;
    [SerializeField] private float arenaRightBound = 10f;
    [SerializeField] private float centerLine = 0f;
    [SerializeField] private bool enableRestriction = true;

    [Header("Teleport Override Settings")]
    [SerializeField] private float teleportGracePeriod = 0.8f; // Time allowed in opponent's side
    [SerializeField] private float teleportPushbackSpeed = 3f; // Speed of automatic return
    [SerializeField] private bool smoothTeleportReturn = true;

    [Header("Smoothing")]
    [SerializeField] private bool useSmoothClamping = true;
    [SerializeField] private float clampSmoothness = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private bool showBounds = true;

    // Player side determination
    public enum PlayerSide { Left, Right, Unassigned }
    public enum OverrideState { Normal, TeleportOverride, GracePeriod, ForcedReturn }

    [Header("Player Assignment")]
    [SerializeField] private PlayerSide playerSide = PlayerSide.Unassigned;
    [SerializeField] private bool autoDetectSide = true;

    // Bounds for this player
    private float playerMinX;
    private float playerMaxX;
    private bool boundsInitialized = false;

    // Teleport override system
    private OverrideState currentOverrideState = OverrideState.Normal;
    private Coroutine teleportOverrideCoroutine;
    private Vector3 homePosition; // Position to return to after teleport

    // Smooth clamping
    private Vector3 lastValidPosition;
    private bool hasValidPosition = false;

    // Events for teleport feedback
    public System.Action OnTeleportGracePeriodStarted;
    public System.Action OnTeleportGracePeriodEnded;
    public System.Action OnForcedReturnStarted;

    void Start()
    {
        InitializePlayerBounds();
    }

    void InitializePlayerBounds()
    {
        if (autoDetectSide)
        {
            DetectPlayerSide();
        }

        SetupBounds();

        lastValidPosition = transform.position;
        homePosition = transform.position;
        hasValidPosition = true;

        if (debugMode)
        {
            Debug.Log($"{gameObject.name} assigned to {playerSide} side. Bounds: [{playerMinX:F1}, {playerMaxX:F1}]");
        }
    }

    void DetectPlayerSide()
    {
        float currentX = transform.position.x;

        if (currentX < centerLine)
        {
            playerSide = PlayerSide.Left;
        }
        else if (currentX > centerLine)
        {
            playerSide = PlayerSide.Right;
        }
        else
        {
            AssignAlternativeSide();
        }
    }

    void AssignAlternativeSide()
    {
        ArenaMovementRestrictor[] allRestrictors = FindObjectsOfType<ArenaMovementRestrictor>();

        foreach (var restrictor in allRestrictors)
        {
            if (restrictor != this && restrictor.playerSide != PlayerSide.Unassigned)
            {
                playerSide = (restrictor.playerSide == PlayerSide.Left) ? PlayerSide.Right : PlayerSide.Left;
                return;
            }
        }

        playerSide = PlayerSide.Left;

        if (debugMode)
        {
            Debug.Log($"{gameObject.name} was on center line, assigned to {playerSide} by default");
        }
    }

    void SetupBounds()
    {
        switch (playerSide)
        {
            case PlayerSide.Left:
                playerMinX = arenaLeftBound;
                playerMaxX = centerLine;
                break;

            case PlayerSide.Right:
                playerMinX = centerLine;
                playerMaxX = arenaRightBound;
                break;

            default:
                playerMinX = arenaLeftBound;
                playerMaxX = arenaRightBound;
                if (debugMode)
                {
                    Debug.LogWarning($"{gameObject.name} has unassigned side - no movement restriction!");
                }
                break;
        }

        boundsInitialized = true;
    }

    /// <summary>
    /// Apply movement restriction with teleport override support
    /// </summary>
    public Vector3 ApplyMovementRestriction(Vector3 newPosition)
    {
        if (!enableRestriction || !boundsInitialized || playerSide == PlayerSide.Unassigned)
        {
            return newPosition;
        }

        Vector3 restrictedPosition = newPosition;

        // Handle different override states
        switch (currentOverrideState)
        {
            case OverrideState.Normal:
                // Normal bounds enforcement
                restrictedPosition = ApplyNormalBounds(newPosition);
                break;

            case OverrideState.TeleportOverride:
                // No bounds during teleport - allow free movement
                restrictedPosition = newPosition;
                break;

            case OverrideState.GracePeriod:
                // Allow movement anywhere, but track if we need forced return
                restrictedPosition = newPosition;
                break;

            case OverrideState.ForcedReturn:
                // Gradually push back to home side
                restrictedPosition = ApplyForcedReturn(newPosition);
                break;
        }

        // Update positions
        if (IsPositionInHomeSide(restrictedPosition))
        {
            lastValidPosition = restrictedPosition;
            homePosition = restrictedPosition;
            hasValidPosition = true;
        }

        return restrictedPosition;
    }

    Vector3 ApplyNormalBounds(Vector3 newPosition)
    {
        Vector3 restrictedPosition = newPosition;

        if (useSmoothClamping && hasValidPosition)
        {
            restrictedPosition = ApplySmoothClamp(newPosition);
        }
        else
        {
            restrictedPosition.x = Mathf.Clamp(newPosition.x, playerMinX, playerMaxX);
        }

        // Debug feedback
        if (debugMode && Mathf.Abs(newPosition.x - restrictedPosition.x) > 0.001f)
        {
            Debug.Log($"{gameObject.name} hit {playerSide} boundary at X={newPosition.x:F2}, clamped to X={restrictedPosition.x:F2}");
        }

        return restrictedPosition;
    }

    Vector3 ApplySmoothClamp(Vector3 newPosition)
    {
        Vector3 clampedPosition = newPosition;

        if (newPosition.x < playerMinX)
        {
            float overshoot = playerMinX - newPosition.x;
            float pushback = overshoot * (1f - clampSmoothness);
            clampedPosition.x = playerMinX - pushback;
        }
        else if (newPosition.x > playerMaxX)
        {
            float overshoot = newPosition.x - playerMaxX;
            float pushback = overshoot * (1f - clampSmoothness);
            clampedPosition.x = playerMaxX + pushback;
        }

        return clampedPosition;
    }

    Vector3 ApplyForcedReturn(Vector3 newPosition)
    {
        // Calculate direction back to home side
        Vector3 targetPosition = GetNearestValidPosition(newPosition);
        Vector3 pushDirection = (targetPosition - newPosition).normalized;

        // Apply pushback force
        Vector3 pushback = pushDirection * teleportPushbackSpeed * Time.deltaTime;
        Vector3 returnPosition = newPosition + pushback;

        // If we're close enough to valid area, snap to it
        if (Vector3.Distance(returnPosition, targetPosition) < 0.5f)
        {
            returnPosition = targetPosition;
            EndTeleportOverride(); // Return to normal state
        }

        return returnPosition;
    }

    Vector3 GetNearestValidPosition(Vector3 currentPos)
    {
        // Return to the center of player's home side
        float homeCenterX = (playerMinX + playerMaxX) * 0.5f;
        return new Vector3(homeCenterX, currentPos.y, currentPos.z);
    }

    /// <summary>
    /// Start teleport override - called when teleport ability begins
    /// </summary>
    public void StartTeleportOverride()
    {
        if (teleportOverrideCoroutine != null)
        {
            StopCoroutine(teleportOverrideCoroutine);
        }

        currentOverrideState = OverrideState.TeleportOverride;
        homePosition = transform.position; // Store current position as home

        if (debugMode)
        {
            Debug.Log($"🌀 {gameObject.name} TELEPORT OVERRIDE STARTED - Bounds disabled");
        }
    }

    /// <summary>
    /// Begin grace period - called when teleport completes
    /// </summary>
    public void BeginTeleportGracePeriod()
    {
        currentOverrideState = OverrideState.GracePeriod;
        teleportOverrideCoroutine = StartCoroutine(TeleportGracePeriodCoroutine());

        OnTeleportGracePeriodStarted?.Invoke();

        if (debugMode)
        {
            Debug.Log($"🌀 {gameObject.name} GRACE PERIOD STARTED - {teleportGracePeriod}s free movement");
        }
    }

    IEnumerator TeleportGracePeriodCoroutine()
    {
        yield return new WaitForSeconds(teleportGracePeriod);

        // Check if player is in opponent's side
        if (!IsPositionInHomeSide(transform.position))
        {
            // Start forced return
            currentOverrideState = OverrideState.ForcedReturn;
            OnForcedReturnStarted?.Invoke();

            if (debugMode)
            {
                Debug.Log($"🌀 {gameObject.name} FORCED RETURN STARTED - Pushing back to home side");
            }
        }
        else
        {
            // Player returned to home side naturally
            EndTeleportOverride();
        }

        OnTeleportGracePeriodEnded?.Invoke();
    }

    /// <summary>
    /// End teleport override and return to normal bounds
    /// </summary>
    public void EndTeleportOverride()
    {
        if (teleportOverrideCoroutine != null)
        {
            StopCoroutine(teleportOverrideCoroutine);
            teleportOverrideCoroutine = null;
        }

        currentOverrideState = OverrideState.Normal;

        if (debugMode)
        {
            Debug.Log($"🌀 {gameObject.name} TELEPORT OVERRIDE ENDED - Normal bounds restored");
        }
    }

    /// <summary>
    /// Check if position is in player's home side
    /// </summary>
    bool IsPositionInHomeSide(Vector3 position)
    {
        return position.x >= playerMinX && position.x <= playerMaxX;
    }

    /// <summary>
    /// Check if position is anywhere in the arena (for teleport validation)
    /// </summary>
    bool IsPositionInArena(Vector3 position)
    {
        return position.x >= arenaLeftBound && position.x <= arenaRightBound;
    }

    // Existing methods remain the same...
    public void SetPlayerSide(PlayerSide side)
    {
        playerSide = side;
        SetupBounds();

        if (debugMode)
        {
            Debug.Log($"{gameObject.name} manually assigned to {playerSide} side");
        }
    }

    public PlayerSide GetPlayerSide() => playerSide;

    public void GetPlayerBounds(out float minX, out float maxX)
    {
        minX = playerMinX;
        maxX = playerMaxX;
    }

    public bool CanMoveInDirection(Vector3 direction, float distance = 1f)
    {
        if (!enableRestriction || !boundsInitialized || currentOverrideState != OverrideState.Normal)
            return true;

        Vector3 testPosition = transform.position + direction.normalized * distance;
        return IsPositionInHomeSide(testPosition);
    }

    public void ResetToValidPosition()
    {
        if (hasValidPosition)
        {
            transform.position = lastValidPosition;

            if (debugMode)
            {
                Debug.Log($"{gameObject.name} reset to last valid position: {lastValidPosition}");
            }
        }
    }

    public void UpdateArenaBounds(float leftBound, float rightBound, float center)
    {
        arenaLeftBound = leftBound;
        arenaRightBound = rightBound;
        centerLine = center;

        SetupBounds();

        if (debugMode)
        {
            Debug.Log($"Arena bounds updated: [{leftBound}, {rightBound}], center: {center}");
        }
    }

    // Public getters for teleport state
    public bool IsInTeleportOverride() => currentOverrideState != OverrideState.Normal;
    public OverrideState GetCurrentOverrideState() => currentOverrideState;

    // Debug visualization
    void OnDrawGizmosSelected()
    {
        if (!showBounds) return;

        // Draw arena bounds
        Gizmos.color = Color.white;
        Vector3 leftPos = new Vector3(arenaLeftBound, transform.position.y, transform.position.z);
        Vector3 rightPos = new Vector3(arenaRightBound, transform.position.y, transform.position.z);
        Vector3 centerPos = new Vector3(centerLine, transform.position.y, transform.position.z);

        Gizmos.DrawLine(leftPos + Vector3.up * 3f, leftPos - Vector3.up * 0.5f);
        Gizmos.DrawLine(rightPos + Vector3.up * 3f, rightPos - Vector3.up * 0.5f);

        // Draw center line
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(centerPos + Vector3.up * 3f, centerPos - Vector3.up * 0.5f);

        if (boundsInitialized && playerSide != PlayerSide.Unassigned)
        {
            // Draw player's home area
            Color sideColor = (playerSide == PlayerSide.Left) ? Color.blue : Color.red;

            // Modify color based on teleport state
            switch (currentOverrideState)
            {
                case OverrideState.TeleportOverride:
                    sideColor = Color.cyan;
                    break;
                case OverrideState.GracePeriod:
                    sideColor = Color.magenta;
                    break;
                case OverrideState.ForcedReturn:
                    sideColor = Color.white;
                    break;
            }

            sideColor.a = 0.3f;
            Gizmos.color = sideColor;

            Vector3 playerLeftPos = new Vector3(playerMinX, transform.position.y, transform.position.z);
            Vector3 playerRightPos = new Vector3(playerMaxX, transform.position.y, transform.position.z);
            Vector3 playerCenter = new Vector3((playerMinX + playerMaxX) * 0.5f, transform.position.y + 1f, transform.position.z);

            Gizmos.DrawLine(playerLeftPos + Vector3.up * 2f, playerLeftPos);
            Gizmos.DrawLine(playerRightPos + Vector3.up * 2f, playerRightPos);
            Gizmos.DrawCube(playerCenter, new Vector3(playerMaxX - playerMinX, 0.2f, 2f));

            // Draw current position indicator
            Gizmos.color = sideColor;
            Gizmos.DrawWireSphere(transform.position, 0.5f);

            // Draw home position during teleport
            if (currentOverrideState != OverrideState.Normal)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(homePosition, Vector3.one * 0.3f);
                Gizmos.DrawLine(transform.position, homePosition);
            }
        }
    }

    void OnGUI()
    {
        if (!debugMode) return;

        float yOffset = (playerSide == PlayerSide.Right) ? 120f : 20f;

        GUILayout.BeginArea(new Rect(10, yOffset, 350, 120));
        GUILayout.BeginVertical("box");

        GUILayout.Label($"{gameObject.name} - {playerSide} Side");
        GUILayout.Label($"Override State: {currentOverrideState}");

        if (boundsInitialized)
        {
            GUILayout.Label($"Bounds: [{playerMinX:F1}, {playerMaxX:F1}]");
            GUILayout.Label($"Position: X={transform.position.x:F2}");

            bool inHomeSide = IsPositionInHomeSide(transform.position);
            string statusText = currentOverrideState != OverrideState.Normal ?
                $"In Home Side: {inHomeSide} (TELEPORT ACTIVE)" :
                $"In Bounds: {inHomeSide}";

            GUILayout.Label(statusText, inHomeSide ? GUI.skin.label : GUI.skin.box);
        }
        else
        {
            GUILayout.Label("Bounds not initialized");
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}