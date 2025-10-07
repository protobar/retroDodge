using UnityEngine;

/// <summary>
/// Centralized system for managing character scale and physics parameters
/// Dynamically calculates values based on actual character dimensions
/// </summary>
public class CharacterScaleManager : MonoBehaviour
{
    public static CharacterScaleManager Instance { get; private set; }

    [Header("Reference Character Settings")]
    [Tooltip("The base character height that all original values were designed for")]
    [SerializeField] private float referenceCharacterHeight = 2.0f; // Original capsule height

    [Header("Detection Settings")]
    [SerializeField] private LayerMask characterLayer;
    [SerializeField] private bool autoDetectOnStart = true;
    [SerializeField] private bool debugMode = true;

    [Header("Calculated Scale Info")]
    [SerializeField] private float detectedCharacterHeight = 0f;
    [SerializeField] private float scaleFactor = 1f;
    [SerializeField] private Vector3 characterCenterOffset = Vector3.zero;
    [SerializeField] private Vector3 characterBoundsSize = Vector3.zero;

    [Header("Dynamic Physics Parameters")]
    [Tooltip("Collision range scales with character size")]
    public float baseCollisionRange = 1.0f;
    public float scaledCollisionRange = 1.0f;

    [Tooltip("Height at which to check collisions (character center)")]
    public float baseCollisionHeight = 1.0f;
    public float scaledCollisionHeight = 1.0f;

    [Tooltip("Height threshold for ducking detection")]
    public float baseDuckingHeight = 1.0f;
    public float scaledDuckingHeight = 1.0f;

    [Tooltip("Ball hold offset from character position")]
    public Vector3 baseHoldOffset = new Vector3(0.5f, 1.5f, 0f);
    public Vector3 scaledHoldOffset = Vector3.zero;

    [Header("Trajectory Settings")]
    [Tooltip("Target height offset (0.0 = feet, 0.5 = center, 1.0 = head)")]
    [Range(0f, 1f)]
    public float targetHeightRatio = 0.6f; // Target upper torso/chest area

    [Tooltip("Arc angle for ball trajectory")]
    [Range(0f, 45f)]
    public float trajectoryArcAngle = 15f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (autoDetectOnStart)
        {
            DetectCharacterScale();
        }
    }

    /// <summary>
    /// Detect actual character dimensions from scene
    /// </summary>
    public void DetectCharacterScale()
    {
        PlayerCharacter[] characters = FindObjectsOfType<PlayerCharacter>();

        if (characters.Length == 0)
        {
            Debug.LogWarning("CharacterScaleManager: No PlayerCharacters found in scene!");
            SetDefaultScale();
            return;
        }

        // Use first character as reference
        PlayerCharacter referenceChar = characters[0];

        // Get all renderers to calculate actual character bounds
        Renderer[] renderers = referenceChar.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            Debug.LogWarning("CharacterScaleManager: No renderers found on character!");
            SetDefaultScale();
            return;
        }

        // Calculate combined bounds of all renderers
        Bounds combinedBounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers)
        {
            combinedBounds.Encapsulate(renderer.bounds);
        }

        // Store detected values
        detectedCharacterHeight = combinedBounds.size.y;
        characterBoundsSize = combinedBounds.size;

        // Calculate center offset from character root position
        characterCenterOffset = combinedBounds.center - referenceChar.transform.position;

        // Calculate scale factor
        scaleFactor = detectedCharacterHeight / referenceCharacterHeight;

        // Update all scaled parameters
        RecalculateScaledParameters();

        if (debugMode)
        {
            Debug.Log($"═══ CHARACTER SCALE DETECTION ═══");
            Debug.Log($"Detected Height: {detectedCharacterHeight:F2}");
            Debug.Log($"Reference Height: {referenceCharacterHeight:F2}");
            Debug.Log($"Scale Factor: {scaleFactor:F2}");
            Debug.Log($"Bounds Size: {characterBoundsSize}");
            Debug.Log($"Center Offset: {characterCenterOffset}");
            Debug.Log($"Scaled Collision Range: {scaledCollisionRange:F2}");
            Debug.Log($"Scaled Collision Height: {scaledCollisionHeight:F2}");
            Debug.Log($"═══════════════════════════════");
        }
    }

    void SetDefaultScale()
    {
        detectedCharacterHeight = referenceCharacterHeight;
        scaleFactor = 1f;
        characterCenterOffset = Vector3.up * (referenceCharacterHeight * 0.5f);
        characterBoundsSize = new Vector3(1f, referenceCharacterHeight, 1f);
        RecalculateScaledParameters();
    }

    void RecalculateScaledParameters()
    {
        // Scale all physics parameters
        scaledCollisionRange = baseCollisionRange * scaleFactor;
        scaledCollisionHeight = baseCollisionHeight * scaleFactor;
        scaledDuckingHeight = baseDuckingHeight * scaleFactor;
        scaledHoldOffset = baseHoldOffset * scaleFactor;
    }

    /// <summary>
    /// Get the center position of a character (for targeting)
    /// </summary>
    public Vector3 GetCharacterCenter(Transform character)
    {
        return character.position + characterCenterOffset;
    }

    /// <summary>
    /// Get the target position for throwing at a character
    /// Uses targetHeightRatio to aim at specific body part
    /// </summary>
    public Vector3 GetCharacterTargetPosition(Transform character)
    {
        // Start at character feet
        Vector3 targetPos = character.position;

        // Add height based on ratio (0 = feet, 0.5 = center, 1 = head)
        targetPos.y += detectedCharacterHeight * targetHeightRatio;

        return targetPos;
    }

    [Header("2.5D Game Settings")]
    [SerializeField] private bool lock2DMode = true;
    [SerializeField] private float fixedZPosition = 0f;

    /// <summary>
    /// Calculate throw direction with arc to a target
    /// </summary>
    public Vector3 CalculateArcDirection(Vector3 from, Vector3 to)
    {
        // NEW: Lock Z positions in 2.5D mode
        if (lock2DMode)
        {
            from.z = fixedZPosition;
            to.z = fixedZPosition;
        }

        Vector3 direction = (to - from).normalized;

        // Add upward arc component
        float arcRadians = trajectoryArcAngle * Mathf.Deg2Rad;
        direction.y += Mathf.Tan(arcRadians);

        // NEW: Ensure Z is zero in 2.5D mode
        if (lock2DMode)
        {
            direction.z = 0f;
        }

        return direction.normalized;
    }

    /// <summary>
    /// Get collision check position for a character
    /// </summary>
    public Vector3 GetCollisionCheckPosition(Transform character)
    {
        return character.position + Vector3.up * scaledCollisionHeight;
    }

    /// <summary>
    /// Check if a position is within collision range of a character
    /// </summary>
    public bool IsInCollisionRange(Vector3 position, Transform character)
    {
        Vector3 checkPos = GetCollisionCheckPosition(character);
        float distance = Vector3.Distance(position, checkPos);
        return distance <= scaledCollisionRange;
    }

    /// <summary>
    /// Get the ducking height threshold
    /// Ball must be below this to hit ducking character
    /// </summary>
    public float GetDuckingThreshold(Transform character)
    {
        return character.position.y + scaledDuckingHeight;
    }

    /// <summary>
    /// Get scaled hold offset for ball attachment
    /// </summary>
    public Vector3 GetHoldOffset()
    {
        return scaledHoldOffset;
    }

    // Public getters
    public float GetScaleFactor() => scaleFactor;
    public float GetCharacterHeight() => detectedCharacterHeight;
    public Vector3 GetCharacterBoundsSize() => characterBoundsSize;
    public float GetCollisionRange() => scaledCollisionRange;
    public float GetCollisionHeight() => scaledCollisionHeight;

    /// <summary>
    /// Manual recalibration - call this if character models change at runtime
    /// </summary>
    public void RecalibrateScale()
    {
        DetectCharacterScale();
    }

    // Editor helper
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            RecalculateScaledParameters();
        }
    }

    // Debug visualization
    private void OnDrawGizmos()
    {
        if (!debugMode || !Application.isPlaying) return;

        PlayerCharacter[] characters = FindObjectsOfType<PlayerCharacter>();
        foreach (PlayerCharacter character in characters)
        {
            if (character == null) continue;

            Transform charTransform = character.transform;

            // Draw character bounds
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(charTransform.position + characterCenterOffset, characterBoundsSize);

            // Draw collision check position
            Gizmos.color = Color.yellow;
            Vector3 collisionPos = GetCollisionCheckPosition(charTransform);
            Gizmos.DrawWireSphere(collisionPos, scaledCollisionRange);

            // Draw target position
            Gizmos.color = Color.red;
            Vector3 targetPos = GetCharacterTargetPosition(charTransform);
            Gizmos.DrawWireSphere(targetPos, 0.2f);

            // Draw ducking threshold
            Gizmos.color = Color.green;
            float duckThreshold = GetDuckingThreshold(charTransform);
            Vector3 duckPos = new Vector3(charTransform.position.x, duckThreshold, charTransform.position.z);
            Gizmos.DrawWireCube(duckPos, new Vector3(characterBoundsSize.x, 0.1f, characterBoundsSize.z));
        }
    }
}