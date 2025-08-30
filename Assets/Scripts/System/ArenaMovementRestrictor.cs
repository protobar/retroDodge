using UnityEngine;
using System.Collections;

/// <summary>
/// Optimized ArenaMovementRestrictor with streamlined teleport override system
/// Removed verbose debug features and redundant state management
/// </summary>
public class ArenaMovementRestrictor : MonoBehaviour
{
    [Header("Arena Settings")]
    [SerializeField] private float arenaLeftBound = -10f;
    [SerializeField] private float arenaRightBound = 10f;
    [SerializeField] private float centerLine = 0f;
    [SerializeField] private bool enableRestriction = true;

    [Header("Teleport Override")]
    [SerializeField] private float teleportGracePeriod = 0.8f;
    [SerializeField] private float teleportPushbackSpeed = 3f;

    public enum PlayerSide { Left, Right, Unassigned }
    public enum OverrideState { Normal, TeleportOverride, GracePeriod, ForcedReturn }

    [Header("Player Assignment")]
    [SerializeField] private PlayerSide playerSide = PlayerSide.Unassigned;
    [SerializeField] private bool autoDetectSide = true;

    // Core state
    private float playerMinX, playerMaxX;
    private bool boundsInitialized = false;
    private OverrideState currentOverrideState = OverrideState.Normal;
    private Coroutine teleportCoroutine;
    private Vector3 homePosition;
    private Vector3 lastValidPosition;

    // Events
    public System.Action OnTeleportGracePeriodStarted;
    public System.Action OnForcedReturnStarted;

    void Start()
    {
        InitializePlayerBounds();
    }

    void InitializePlayerBounds()
    {
        if (autoDetectSide) DetectPlayerSide();
        SetupBounds();

        lastValidPosition = transform.position;
        homePosition = transform.position;
    }

    void DetectPlayerSide()
    {
        float currentX = transform.position.x;

        if (currentX < centerLine)
            playerSide = PlayerSide.Left;
        else if (currentX > centerLine)
            playerSide = PlayerSide.Right;
        else
            AssignAlternativeSide();
    }

    void AssignAlternativeSide()
    {
        var restrictors = FindObjectsOfType<ArenaMovementRestrictor>();
        foreach (var restrictor in restrictors)
        {
            if (restrictor != this && restrictor.playerSide != PlayerSide.Unassigned)
            {
                playerSide = (restrictor.playerSide == PlayerSide.Left) ? PlayerSide.Right : PlayerSide.Left;
                return;
            }
        }
        playerSide = PlayerSide.Left; // Default
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
                break;
        }
        boundsInitialized = true;
    }

    public Vector3 ApplyMovementRestriction(Vector3 newPosition)
    {
        if (!enableRestriction || !boundsInitialized || playerSide == PlayerSide.Unassigned)
            return newPosition;

        Vector3 restrictedPosition = newPosition;

        switch (currentOverrideState)
        {
            case OverrideState.Normal:
                restrictedPosition = ApplyNormalBounds(newPosition);
                break;
            case OverrideState.TeleportOverride:
            case OverrideState.GracePeriod:
                // Allow free movement
                break;
            case OverrideState.ForcedReturn:
                restrictedPosition = ApplyForcedReturn(newPosition);
                break;
        }

        // Update valid position cache
        if (IsPositionInHomeSide(restrictedPosition))
        {
            lastValidPosition = restrictedPosition;
            homePosition = restrictedPosition;
        }

        return restrictedPosition;
    }

    Vector3 ApplyNormalBounds(Vector3 newPosition)
    {
        Vector3 restricted = newPosition;
        restricted.x = Mathf.Clamp(newPosition.x, playerMinX, playerMaxX);
        return restricted;
    }

    Vector3 ApplyForcedReturn(Vector3 newPosition)
    {
        Vector3 homeCenterX = new Vector3((playerMinX + playerMaxX) * 0.5f, newPosition.y, newPosition.z);
        Vector3 pushDirection = (homeCenterX - newPosition).normalized;
        Vector3 returnPosition = newPosition + pushDirection * teleportPushbackSpeed * Time.deltaTime;

        if (Vector3.Distance(returnPosition, homeCenterX) < 0.5f)
        {
            returnPosition = homeCenterX;
            EndTeleportOverride();
        }

        return returnPosition;
    }

    bool IsPositionInHomeSide(Vector3 position)
    {
        return position.x >= playerMinX && position.x <= playerMaxX;
    }

    // Teleport override system - streamlined
    public void StartTeleportOverride()
    {
        if (teleportCoroutine != null) StopCoroutine(teleportCoroutine);
        currentOverrideState = OverrideState.TeleportOverride;
        homePosition = transform.position;
    }

    public void BeginTeleportGracePeriod()
    {
        currentOverrideState = OverrideState.GracePeriod;
        teleportCoroutine = StartCoroutine(TeleportGracePeriodCoroutine());
        OnTeleportGracePeriodStarted?.Invoke();
    }

    IEnumerator TeleportGracePeriodCoroutine()
    {
        yield return new WaitForSeconds(teleportGracePeriod);

        if (!IsPositionInHomeSide(transform.position))
        {
            currentOverrideState = OverrideState.ForcedReturn;
            OnForcedReturnStarted?.Invoke();
        }
        else
        {
            EndTeleportOverride();
        }
    }

    public void EndTeleportOverride()
    {
        if (teleportCoroutine != null)
        {
            StopCoroutine(teleportCoroutine);
            teleportCoroutine = null;
        }
        currentOverrideState = OverrideState.Normal;
    }

    // Public API
    public PlayerSide GetPlayerSide() => playerSide;
    public void GetPlayerBounds(out float minX, out float maxX)
    {
        minX = playerMinX;
        maxX = playerMaxX;
    }

    public bool IsInTeleportOverride() => currentOverrideState != OverrideState.Normal;
    public OverrideState GetCurrentOverrideState() => currentOverrideState;

    public bool CanMoveInDirection(Vector3 direction, float distance = 1f)
    {
        if (!enableRestriction || !boundsInitialized || currentOverrideState != OverrideState.Normal)
            return true;

        Vector3 testPosition = transform.position + direction.normalized * distance;
        return IsPositionInHomeSide(testPosition);
    }

    public void SetPlayerSide(PlayerSide side)
    {
        playerSide = side;
        SetupBounds();
    }

    public void UpdateArenaBounds(float leftBound, float rightBound, float center)
    {
        arenaLeftBound = leftBound;
        arenaRightBound = rightBound;
        centerLine = center;
        SetupBounds();
    }

    public void ResetToValidPosition()
    {
        transform.position = lastValidPosition;
    }
}