using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Fighting Game Camera Controller - KOF/Street Fighter Style
/// Follows both players, maintains competitive view, works in offline and online modes
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private Vector3 basePosition = new Vector3(0, 8, -12);
    [SerializeField] private Quaternion fixedRotation = Quaternion.Euler(20f, 0f, 0f);
    [SerializeField] private float followSpeed = 8f;
    [SerializeField] private float zoomSpeed = 5f;

    [Header("Player Tracking")]
    [SerializeField] private float minDistance = 8f;
    [SerializeField] private float maxDistance = 20f;
    [SerializeField] private float idealDistance = 12f;
    [SerializeField] private float distanceMultiplier = 0.8f;

    [Header("Camera Bounds")]
    [SerializeField] private float leftBound = -12f;
    [SerializeField] private float rightBound = 12f;
    [SerializeField] private float topBound = 6f;
    [SerializeField] private float bottomBound = 2f;
    [SerializeField] private bool enableBounds = true;

    [Header("Smooth Movement")]
    [SerializeField] private float positionSmoothing = 0.1f;
    [SerializeField] private float zoomSmoothing = 0.1f;
    [SerializeField] private float deadZone = 1f;

    [Header("Camera Shake")]
    [SerializeField] private float baseShakeIntensity = 0.5f;
    [SerializeField] private float baseShakeDuration = 0.3f;
    [SerializeField] private float maxShakeIntensity = 10.0f; // Increased for testing
    [SerializeField] private bool allowShakeStacking = true;

    // Player references
    private Transform player1;
    private Transform player2;
    private Vector3 midpoint;
    private float playerDistance;
    private Vector3 targetPosition;
    private float targetZoom;
    private Vector3 currentVelocity;
    private float currentZoomVelocity;

    // Camera shake - improved system
    private List<ShakeData> activeShakes = new List<ShakeData>();
    private Vector3 shakeOffset = Vector3.zero;

    [System.Serializable]
    private class ShakeData
    {
        public float intensity;
        public float duration;
        public float elapsed;
        public Vector3 randomSeed;

        public ShakeData(float intensity, float duration)
        {
            this.intensity = intensity;
            this.duration = duration;
            this.elapsed = 0f;
            this.randomSeed = new Vector3(Random.Range(0f, 1000f), Random.Range(0f, 1000f), Random.Range(0f, 1000f));
        }
    }

    // Debug
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    void Start()
    {
        // Set fixed camera rotation (fighting game style)
        transform.rotation = fixedRotation;
        
        // Initialize camera position
        transform.position = basePosition;
        targetPosition = basePosition;
        targetZoom = idealDistance;

        // Find players after a short delay to ensure they're spawned
        StartCoroutine(FindPlayersDelayed());
    }

    IEnumerator FindPlayersDelayed()
    {
        yield return new WaitForSeconds(0.5f);
        FindPlayers();
    }

    void FindPlayers()
    {
        // Find all PlayerCharacter objects
        var players = FindObjectsOfType<PlayerCharacter>();
        
        if (players.Length >= 2)
        {
            // Sort by X position to determine left/right players
            System.Array.Sort(players, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
            
            player1 = players[0].transform; // Left player
            player2 = players[1].transform; // Right player
            
            if (showDebugInfo)
            {
                Debug.Log($"[CAMERA] Found players: {player1.name} (left), {player2.name} (right)");
            }
        }
        else if (players.Length == 1)
        {
            // Single player mode - center on player
            player1 = players[0].transform;
            player2 = players[0].transform; // Same player for midpoint calculation
        }
        else
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("[CAMERA] No players found, using default position");
            }
        }
    }

    void LateUpdate()
    {
        if (player1 == null || player2 == null)
        {
            // Try to find players again if we don't have them
            FindPlayers();
            return;
        }

        UpdateCameraPosition();
    }

    void UpdateCameraPosition()
    {
        // Calculate midpoint between players
        midpoint = (player1.position + player2.position) * 0.5f;
        
        // Calculate distance between players
        playerDistance = Vector3.Distance(player1.position, player2.position);
        
        // Calculate target zoom based on player distance
        targetZoom = Mathf.Clamp(playerDistance * distanceMultiplier, minDistance, maxDistance);
        
        // Calculate target position
        Vector3 desiredPosition = new Vector3(
            midpoint.x,
            basePosition.y,
            basePosition.z - targetZoom
        );

        // Apply bounds if enabled
        if (enableBounds)
        {
            desiredPosition.x = Mathf.Clamp(desiredPosition.x, leftBound, rightBound);
            desiredPosition.y = Mathf.Clamp(desiredPosition.y, bottomBound, topBound);
        }

        // Smooth position movement
        targetPosition = Vector3.SmoothDamp(
            targetPosition,
            desiredPosition,
            ref currentVelocity,
            positionSmoothing
        );

        // Update camera shake
        UpdateCameraShake();
        
        // Apply camera shake if active
        Vector3 finalPosition = targetPosition + shakeOffset;
        
        // Smooth camera movement (but preserve shake)
        Vector3 smoothPosition = Vector3.Lerp(
            transform.position,
            targetPosition, // Don't smooth the shake offset
            followSpeed * Time.deltaTime
        );
        
        // Apply shake directly to the smooth position
        transform.position = smoothPosition + shakeOffset;

        // Maintain fixed rotation
        transform.rotation = fixedRotation;

        // Debug info
        if (showDebugInfo)
        {
            Debug.DrawLine(player1.position, player2.position, Color.yellow);
            Debug.DrawLine(transform.position, midpoint, Color.cyan);
        }
    }

    /// <summary>
    /// Trigger camera shake effect - improved system with stacking
    /// </summary>
    public void ShakeCamera(float intensity = 0.5f, float duration = 0.3f)
    {
        Debug.Log($"[CAMERA CONTROLLER] ShakeCamera called: intensity={intensity}, duration={duration}, activeShakes={activeShakes.Count}");
        
        // Clamp intensity to prevent excessive shaking
        intensity = Mathf.Clamp(intensity, 0f, maxShakeIntensity);
        
        if (allowShakeStacking || activeShakes.Count == 0)
        {
            activeShakes.Add(new ShakeData(intensity, duration));
            Debug.Log($"[CAMERA CONTROLLER] Added shake. Total active: {activeShakes.Count}");
        }
        else
        {
            // Replace existing shake with stronger one
            if (intensity > activeShakes[0].intensity)
            {
                activeShakes.Clear();
                activeShakes.Add(new ShakeData(intensity, duration));
                Debug.Log($"[CAMERA CONTROLLER] Replaced with stronger shake. Total active: {activeShakes.Count}");
            }
            else
            {
                Debug.Log($"[CAMERA CONTROLLER] Shake rejected - weaker than existing");
            }
        }
    }

    /// <summary>
    /// Update all active camera shakes
    /// </summary>
    void UpdateCameraShake()
    {
        if (activeShakes.Count == 0)
        {
            shakeOffset = Vector3.zero;
            return;
        }

        Vector3 totalShake = Vector3.zero;
        
        // Process all active shakes
        for (int i = activeShakes.Count - 1; i >= 0; i--)
        {
            ShakeData shake = activeShakes[i];
            shake.elapsed += Time.deltaTime;
            
            if (shake.elapsed >= shake.duration)
            {
                // Remove finished shake
                activeShakes.RemoveAt(i);
                continue;
            }
            
            // Calculate shake intensity with falloff
            float progress = shake.elapsed / shake.duration;
            float falloff = 1f - (progress * progress); // Quadratic falloff
            float currentIntensity = shake.intensity * falloff;
            
            // Generate shake offset using both random and Perlin noise for more noticeable effect
            float time = Time.time * 30f; // Speed up noise
            float randomFactor = 0.7f; // Mix of random and Perlin
            float perlinFactor = 0.3f;
            
            // Random shake (more aggressive)
            float randomX = (Random.Range(-1f, 1f) * randomFactor + 
                           (Mathf.PerlinNoise(time + shake.randomSeed.x, 0) - 0.5f) * 2f * perlinFactor) * currentIntensity;
            float randomY = (Random.Range(-1f, 1f) * randomFactor + 
                           (Mathf.PerlinNoise(0, time + shake.randomSeed.y) - 0.5f) * 2f * perlinFactor) * currentIntensity;
            float randomZ = (Random.Range(-1f, 1f) * randomFactor + 
                           (Mathf.PerlinNoise(time + shake.randomSeed.z, time + shake.randomSeed.z) - 0.5f) * 2f * perlinFactor) * currentIntensity * 0.5f;
            
            Vector3 shakeVector = new Vector3(randomX, randomY, randomZ);
            totalShake += shakeVector;
        }
        
        // Apply total shake with some smoothing
        shakeOffset = Vector3.Lerp(shakeOffset, totalShake, Time.deltaTime * 15f);
        
        // Debug logging
        if (showDebugInfo && totalShake.magnitude > 0.01f)
        {
            Debug.Log($"[CAMERA SHAKE] Total shake: {totalShake}, Magnitude: {totalShake.magnitude}, ShakeOffset: {shakeOffset}");
        }
    }

    /// <summary>
    /// Clear all active shakes
    /// </summary>
    public void ClearAllShakes()
    {
        activeShakes.Clear();
        shakeOffset = Vector3.zero;
    }
    
    /// <summary>
    /// Test method to verify shake system is working
    /// </summary>
    [ContextMenu("Test Shake")]
    public void TestShake()
    {
        Debug.Log("[CAMERA CONTROLLER] Testing shake with intensity 5.0");
        ShakeCamera(5.0f, 2.0f);
    }

    /// <summary>
    /// Set camera bounds dynamically
    /// </summary>
    public void SetBounds(float left, float right, float top, float bottom)
    {
        leftBound = left;
        rightBound = right;
        topBound = top;
        bottomBound = bottom;
    }

    /// <summary>
    /// Force camera to find players again (useful when players respawn)
    /// </summary>
    public void RefreshPlayers()
    {
        FindPlayers();
    }

    /// <summary>
    /// Set camera to follow specific players
    /// </summary>
    public void SetPlayers(Transform p1, Transform p2)
    {
        player1 = p1;
        player2 = p2;
    }

    /// <summary>
    /// Get current midpoint between players
    /// </summary>
    public Vector3 GetMidpoint()
    {
        return midpoint;
    }

    /// <summary>
    /// Get current distance between players
    /// </summary>
    public float GetPlayerDistance()
    {
        return playerDistance;
    }

    // Debug visualization
    void OnDrawGizmosSelected()
    {
        if (!enableBounds) return;

        // Draw camera bounds
        Gizmos.color = Color.yellow;
        Vector3 center = new Vector3((leftBound + rightBound) * 0.5f, (topBound + bottomBound) * 0.5f, basePosition.z);
        Vector3 size = new Vector3(rightBound - leftBound, topBound - bottomBound, 0.1f);
        Gizmos.DrawWireCube(center, size);

        // Draw ideal distance range
        Gizmos.color = Color.cyan;
        Vector3 leftPoint = new Vector3(leftBound, basePosition.y, basePosition.z - idealDistance);
        Vector3 rightPoint = new Vector3(rightBound, basePosition.y, basePosition.z - idealDistance);
        Gizmos.DrawLine(leftPoint, rightPoint);

        // Draw current camera position
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        // Draw midpoint if players are found
        if (player1 != null && player2 != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(midpoint, 0.3f);
            
            // Draw line between players
            Gizmos.color = Color.white;
            Gizmos.DrawLine(player1.position, player2.position);
        }
    }

    // Public properties for external access
    public bool HasPlayers => player1 != null && player2 != null;
    public Vector3 Midpoint => midpoint;
    public float PlayerDistance => playerDistance;
    public bool IsShaking => activeShakes.Count > 0;
    public int ActiveShakeCount => activeShakes.Count;
}