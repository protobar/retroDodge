using UnityEngine;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [Header("Player 1 Controls")]
    public KeyCode p1_Left = KeyCode.A;
    public KeyCode p1_Right = KeyCode.D;
    public KeyCode p1_Jump = KeyCode.W;
    public KeyCode p1_Duck = KeyCode.S;
    public KeyCode p1_Throw = KeyCode.J;
    public KeyCode p1_Catch = KeyCode.K;
    public KeyCode p1_Ultimate = KeyCode.U;

    [Header("Player 2 Controls (Future Use)")]
    public KeyCode p2_Left = KeyCode.LeftArrow;
    public KeyCode p2_Right = KeyCode.RightArrow;
    public KeyCode p2_Jump = KeyCode.UpArrow;
    public KeyCode p2_Duck = KeyCode.DownArrow;
    public KeyCode p2_Throw = KeyCode.Keypad1;
    public KeyCode p2_Catch = KeyCode.Keypad2;
    public KeyCode p2_Ultimate = KeyCode.Keypad3;

    [Header("Input Buffer Settings")]
    [SerializeField] private float inputBufferTime = 0.15f;

    // Input buffer system for responsive controls
    private float lastThrowInput = -1f;
    private float lastCatchInput = -1f;
    private float lastJumpInput = -1f;
    private float lastUltimateInput = -1f;

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        HandleInputBuffering();
    }

    void HandleInputBuffering()
    {
        // Buffer throw input
        if (Input.GetKeyDown(p1_Throw))
            lastThrowInput = Time.time;

        // Buffer catch input
        if (Input.GetKeyDown(p1_Catch))
            lastCatchInput = Time.time;

        // Buffer jump input
        if (Input.GetKeyDown(p1_Jump))
            lastJumpInput = Time.time;

        // Buffer ultimate input
        if (Input.GetKeyDown(p1_Ultimate))
            lastUltimateInput = Time.time;
    }

    // Player 1 Input Methods
    public float GetHorizontalInput()
    {
        float horizontal = 0f;

        if (Input.GetKey(p1_Left))
            horizontal = -1f;
        else if (Input.GetKey(p1_Right))
            horizontal = 1f;

        return horizontal;
    }

    public bool GetJumpInput()
    {
        return Input.GetKeyDown(p1_Jump);
    }

    public bool GetJumpInputBuffered()
    {
        if (Time.time - lastJumpInput <= inputBufferTime)
        {
            lastJumpInput = -1f; // Consume the input
            return true;
        }
        return false;
    }

    public bool GetDuckInput()
    {
        return Input.GetKey(p1_Duck);
    }

    public bool GetThrowInput()
    {
        return Input.GetKeyDown(p1_Throw);
    }

    public bool GetThrowInputBuffered()
    {
        if (Time.time - lastThrowInput <= inputBufferTime)
        {
            lastThrowInput = -1f; // Consume the input
            return true;
        }
        return false;
    }

    public bool GetThrowHeld()
    {
        return Input.GetKey(p1_Throw);
    }

    public bool GetCatchInput()
    {
        return Input.GetKeyDown(p1_Catch);
    }

    public bool GetCatchInputBuffered()
    {
        if (Time.time - lastCatchInput <= inputBufferTime)
        {
            lastCatchInput = -1f; // Consume the input
            return true;
        }
        return false;
    }

    public bool GetUltimateInput()
    {
        return Input.GetKeyDown(p1_Ultimate);
    }

    public bool GetUltimateInputBuffered()
    {
        if (Time.time - lastUltimateInput <= inputBufferTime)
        {
            lastUltimateInput = -1f; // Consume the input
            return true;
        }
        return false;
    }

    // Debug display
    void OnGUI()
    {
        if (Application.isEditor)
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("=== INPUT DEBUG ===");
            GUILayout.Label($"Horizontal: {GetHorizontalInput()}");
            GUILayout.Label($"Duck: {GetDuckInput()}");
            GUILayout.Label($"Throw Held: {GetThrowHeld()}");

            // Show buffered inputs
            GUILayout.Label("=== BUFFERED INPUTS ===");
            GUILayout.Label($"Jump Buffer: {(Time.time - lastJumpInput <= inputBufferTime)}");
            GUILayout.Label($"Throw Buffer: {(Time.time - lastThrowInput <= inputBufferTime)}");
            GUILayout.Label($"Catch Buffer: {(Time.time - lastCatchInput <= inputBufferTime)}");
            GUILayout.EndArea();
        }
    }
}