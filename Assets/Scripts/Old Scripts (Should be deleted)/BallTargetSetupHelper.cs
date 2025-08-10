using UnityEngine;

/// <summary>
/// Helper script to debug and fix BallTargetManager registration issues
/// Attach this to any GameObject in your scene for debugging
/// </summary>
public class BallTargetSetupHelper : MonoBehaviour
{
    [Header("Manual Player Assignment (Drag from Scene)")]
    [SerializeField] private Transform player1Transform;
    [SerializeField] private Transform player2Transform;

    [Header("Debug")]
    [SerializeField] private bool autoSetupOnStart = true;
    [SerializeField] private bool showDebugInfo = true;

    void Start()
    {
        if (autoSetupOnStart)
        {
            SetupPlayersManually();
        }
    }

    void Update()
    {
        // Debug keys
        if (Input.GetKeyDown(KeyCode.U))
        {
            SetupPlayersManually();
        }

        if (Input.GetKeyDown(KeyCode.I))
        {
            ShowDebugInfo();
        }

        if (Input.GetKeyDown(KeyCode.O))
        {
            TestTargeting();
        }
    }

    [ContextMenu("Setup Players Manually")]
    public void SetupPlayersManually()
    {
        if (BallTargetManager.Instance == null)
        {
            Debug.LogError("BallTargetManager not found! Make sure it exists in the scene.");
            return;
        }

        // Clear existing registrations
        Debug.Log("=== Setting Up Players Manually ===");

        // Find players if not assigned
        if (player1Transform == null)
        {
            GameObject player1 = GameObject.Find("Player1");
            if (player1 != null) player1Transform = player1.transform;
        }

        if (player2Transform == null)
        {
            GameObject player2 = GameObject.Find("Player2");
            if (player2 != null) player2Transform = player2.transform;
        }

        // Register Player 1
        if (player1Transform != null)
        {
            CharacterController char1 = player1Transform.GetComponent<CharacterController>();
            if (char1 != null)
            {
                BallTargetManager.Instance.RegisterPlayer(1, player1Transform, char1, true);
                Debug.Log($"✅ Registered Player1: {player1Transform.name}");
            }
            else
            {
                Debug.LogError($"❌ Player1 ({player1Transform.name}) missing CharacterController component!");
            }
        }
        else
        {
            Debug.LogError("❌ Player1 Transform not assigned!");
        }

        // Register Player 2
        if (player2Transform != null)
        {
            CharacterController char2 = player2Transform.GetComponent<CharacterController>();
            if (char2 != null)
            {
                BallTargetManager.Instance.RegisterPlayer(2, player2Transform, char2, true);
                Debug.Log($"✅ Registered Player2: {player2Transform.name}");
            }
            else
            {
                Debug.LogError($"❌ Player2 ({player2Transform.name}) missing CharacterController component!");
            }
        }
        else
        {
            Debug.LogError("❌ Player2 Transform not assigned!");
        }

        // Show final status
        ShowDebugInfo();
    }

    void ShowDebugInfo()
    {
        Debug.Log("=== BallTargetManager Debug Info ===");

        if (BallTargetManager.Instance != null)
        {
            Debug.Log(BallTargetManager.Instance.GetDebugInfo());
        }
        else
        {
            Debug.LogError("BallTargetManager.Instance is NULL!");
        }

        // Check BallManager
        if (BallManager.Instance != null)
        {
            BallController ball = BallManager.Instance.GetCurrentBall();
            if (ball != null)
            {
                Debug.Log($"Ball State: {ball.GetBallState()}");
                Debug.Log($"Ball Holder: {(ball.GetHolder() != null ? ball.GetHolder().name : "None")}");
                Debug.Log($"Ball Target: {(ball.GetCurrentTarget() != null ? ball.GetCurrentTarget().name : "None")}");
            }
            else
            {
                Debug.Log("No active ball found");
            }
        }
        else
        {
            Debug.LogError("BallManager.Instance is NULL!");
        }
    }

    void TestTargeting()
    {
        Debug.Log("=== Testing Targeting Logic ===");

        if (BallTargetManager.Instance == null)
        {
            Debug.LogError("BallTargetManager not found!");
            return;
        }

        // Test Player1 targeting
        if (player1Transform != null)
        {
            CharacterController char1 = player1Transform.GetComponent<CharacterController>();
            if (char1 != null)
            {
                Transform target1 = BallTargetManager.Instance.GetOpponent(char1);
                Debug.Log($"Player1 should target: {(target1 ? target1.name : "NULL")}");
            }
        }

        // Test Player2 targeting
        if (player2Transform != null)
        {
            CharacterController char2 = player2Transform.GetComponent<CharacterController>();
            if (char2 != null)
            {
                Transform target2 = BallTargetManager.Instance.GetOpponent(char2);
                Debug.Log($"Player2 should target: {(target2 ? target2.name : "NULL")}");
            }
        }
    }

    void OnGUI()
    {
        if (!showDebugInfo) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.BeginVertical("box");

        GUILayout.Label("Ball Targeting Debug", GUI.skin.box);

        if (GUILayout.Button("Setup Players (U)"))
        {
            SetupPlayersManually();
        }

        if (GUILayout.Button("Show Debug Info (I)"))
        {
            ShowDebugInfo();
        }

        if (GUILayout.Button("Test Targeting (O)"))
        {
            TestTargeting();
        }

        // Show current status
        if (BallTargetManager.Instance != null)
        {
            int playerCount = BallTargetManager.Instance.GetActivePlayerCount();
            GUILayout.Label($"Registered Players: {playerCount}");
        }
        else
        {
            GUILayout.Label("BallTargetManager: NOT FOUND", GUI.skin.box);
        }

        if (BallManager.Instance != null && BallManager.Instance.GetCurrentBall() != null)
        {
            BallController ball = BallManager.Instance.GetCurrentBall();
            GUILayout.Label($"Ball State: {ball.GetBallState()}");

            if (ball.GetCurrentTarget() != null)
            {
                GUILayout.Label($"Current Target: {ball.GetCurrentTarget().name}");
            }
            else
            {
                GUILayout.Label("Current Target: NONE", GUI.skin.box);
            }
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}