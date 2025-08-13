using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private Vector3 basePosition = new Vector3(0, 5, -10);
    [SerializeField] private float followSpeed = 5f;
    [SerializeField] private Quaternion fixedRotation = Quaternion.Euler(15f, 0f, 0f);

    [Header("Bounds")]
    [SerializeField] private float leftBound = -15f;
    [SerializeField] private float rightBound = 15f;
    [SerializeField] private bool enableBounds = true;

    [Header("Horizontal Panning")]
    [SerializeField] private float panFollowStrength = 0.3f;
    [SerializeField] private float maxPanOffset = 3f;

    // Target to follow (player)
    private Transform target;
    private Vector3 targetPosition;

    void Start()
    {
        // Find the player character
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            target = player.transform;
        }

        // Set fixed camera rotation (KOF/Street Fighter style)
        transform.rotation = fixedRotation;

        // Set base camera position
        if (transform.position == Vector3.zero)
        {
            transform.position = basePosition;
        }
        else
        {
            basePosition = transform.position;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        FollowTarget();
    }

    void FollowTarget()
    {
        // Calculate horizontal panning based on player position
        Vector3 playerPos = target.position;
        float panOffset = playerPos.x * panFollowStrength;

        // Clamp the panning movement
        panOffset = Mathf.Clamp(panOffset, -maxPanOffset, maxPanOffset);

        // Calculate target position (only pan horizontally)
        targetPosition = new Vector3(
            basePosition.x + panOffset,
            basePosition.y,
            basePosition.z
        );

        // Apply bounds if enabled
        if (enableBounds)
        {
            targetPosition.x = Mathf.Clamp(targetPosition.x,
                basePosition.x + leftBound,
                basePosition.x + rightBound);
        }

        // Smooth camera movement (only horizontal)
        Vector3 smoothedPosition = Vector3.Lerp(
            transform.position,
            targetPosition,
            followSpeed * Time.deltaTime
        );

        transform.position = smoothedPosition;

        // Maintain fixed rotation (no looking at player)
        transform.rotation = fixedRotation;
    }

    // Public methods for camera control
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void SetCameraPosition(Vector3 newPosition)
    {
        basePosition = newPosition;
        targetPosition = newPosition;
        transform.position = newPosition;
        transform.rotation = fixedRotation;
    }

    public void ShakeCamera(float intensity = 0.5f, float duration = 0.3f)
    {
        // Simple camera shake - can be enhanced later
        StartCoroutine(CameraShakeCoroutine(intensity, duration));
    }

    private System.Collections.IEnumerator CameraShakeCoroutine(float intensity, float duration)
    {
        Vector3 originalPos = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * intensity;
            float y = Random.Range(-1f, 1f) * intensity;

            transform.position = originalPos + new Vector3(x, y, 0);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = originalPos;
    }

    // Debug visualization
    void OnDrawGizmosSelected()
    {
        // Draw camera bounds
        if (enableBounds)
        {
            Gizmos.color = Color.yellow;
            Vector3 leftPoint = new Vector3(basePosition.x + leftBound, basePosition.y, basePosition.z);
            Vector3 rightPoint = new Vector3(basePosition.x + rightBound, basePosition.y, basePosition.z);

            Gizmos.DrawLine(leftPoint + Vector3.up * 3, leftPoint - Vector3.up * 3);
            Gizmos.DrawLine(rightPoint + Vector3.up * 3, rightPoint - Vector3.up * 3);

            // Draw pan range
            Gizmos.color = Color.cyan;
            Vector3 panLeft = new Vector3(basePosition.x - maxPanOffset, basePosition.y, basePosition.z);
            Vector3 panRight = new Vector3(basePosition.x + maxPanOffset, basePosition.y, basePosition.z);
            Gizmos.DrawLine(panLeft, panRight);
        }

        // Draw base position
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(basePosition, Vector3.one);
    }
}