using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== DYNAMIC CAMERA ====================
public class DynamicCamera : MonoBehaviour
{
    private static DynamicCamera instance;
    public static DynamicCamera Instance => instance;

    [Header("Camera Settings")]
    public float baseDistance = 15f;
    public float minDistance = 12f;
    public float maxDistance = 20f;
    public float smoothTime = 0.3f;
    public float heightOffset = 8f;
    public AnimationCurve zoomCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Follow Settings")]
    public bool followPlayers = true;
    public float followSmoothness = 5f;

    private Camera cam;
    private Vector3 originalPosition;
    private GameObject[] players;
    private Vector3 velocity;
    private Vector3 targetPosition;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        cam = GetComponent<Camera>();
        originalPosition = transform.position;
        targetPosition = originalPosition;
    }

    void Start()
    {
        // Find players in the scene
        RefreshPlayerReferences();
    }

    void LateUpdate()
    {
        if (followPlayers)
        {
            UpdateCameraPosition();
        }
    }

    void RefreshPlayerReferences()
    {
        players = GameObject.FindGameObjectsWithTag("Player");
    }

    void UpdateCameraPosition()
    {
        if (players == null || players.Length == 0)
        {
            RefreshPlayerReferences();
            return;
        }

        if (players.Length == 1)
        {
            // Single player - follow that player
            Vector3 playerPos = players[0].transform.position;
            targetPosition = new Vector3(playerPos.x, originalPosition.y, originalPosition.z);
        }
        else if (players.Length >= 2)
        {
            // Multiple players - calculate center point and adjust distance
            Vector3 centerPoint = CalculateCenterPoint();
            float playerDistance = CalculateMaxDistance();

            // Adjust camera distance based on player separation
            float targetDistance = Mathf.Lerp(minDistance, maxDistance,
                Mathf.Clamp01(playerDistance / 20f));

            targetPosition = centerPoint + new Vector3(0, heightOffset, -targetDistance);
        }

        // Smooth camera movement
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition,
            ref velocity, smoothTime);

        // Look at center point
        if (players.Length > 0)
        {
            Vector3 lookTarget = CalculateCenterPoint();
            lookTarget.y += 2f; // Look slightly above ground level
            transform.LookAt(lookTarget);
        }
    }

    Vector3 CalculateCenterPoint()
    {
        if (players.Length == 0) return Vector3.zero;

        Vector3 center = Vector3.zero;
        int validPlayers = 0;

        foreach (var player in players)
        {
            if (player != null)
            {
                center += player.transform.position;
                validPlayers++;
            }
        }

        if (validPlayers > 0)
        {
            center /= validPlayers;
            center.y = 0f; // Keep at ground level for calculation
        }

        return center;
    }

    float CalculateMaxDistance()
    {
        if (players.Length < 2) return 0f;

        float maxDistance = 0f;

        for (int i = 0; i < players.Length; i++)
        {
            for (int j = i + 1; j < players.Length; j++)
            {
                if (players[i] != null && players[j] != null)
                {
                    float distance = Vector3.Distance(
                        players[i].transform.position,
                        players[j].transform.position
                    );

                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                    }
                }
            }
        }

        return maxDistance;
    }

    public void OnUltimateActivated()
    {
        StartCoroutine(UltimateZoomEffect());
    }

    IEnumerator UltimateZoomEffect()
    {
        float elapsed = 0f;
        float duration = 0.8f;
        float originalFOV = cam.fieldOfView;
        float targetFOV = originalFOV - 15f;

        // Zoom in
        while (elapsed < duration / 2)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (duration / 2);
            cam.fieldOfView = Mathf.Lerp(originalFOV, targetFOV, zoomCurve.Evaluate(t));
            yield return null;
        }

        // Zoom back out
        elapsed = 0f;
        while (elapsed < duration / 2)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (duration / 2);
            cam.fieldOfView = Mathf.Lerp(targetFOV, originalFOV, zoomCurve.Evaluate(t));
            yield return null;
        }

        cam.fieldOfView = originalFOV;
    }

    public void SetFollowPlayers(bool follow)
    {
        followPlayers = follow;
    }

    public void FocusOnPosition(Vector3 position, float duration = 2f)
    {
        StartCoroutine(FocusCoroutine(position, duration));
    }

    IEnumerator FocusCoroutine(Vector3 position, float duration)
    {
        bool wasFollowing = followPlayers;
        followPlayers = false;

        Vector3 startPos = transform.position;
        Vector3 focusPos = position + new Vector3(0, heightOffset, -baseDistance);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            transform.position = Vector3.Lerp(startPos, focusPos, t);
            transform.LookAt(position);

            yield return null;
        }

        followPlayers = wasFollowing;
    }
}