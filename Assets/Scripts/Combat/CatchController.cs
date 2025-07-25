using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

// ==================== CATCH CONTROLLER ====================
public class CatchController : MonoBehaviourPun
{
    [Header("Catch Settings")]
    public float catchRadius = 1.5f;
    public float catchWindow = 0.5f;
    public LayerMask ballLayer;

    private CharacterBase character;
    private bool isCatchWindowActive = false;
    private float catchWindowTimer = 0f;

    // Visual feedback
    public GameObject catchIndicator;
    public ParticleSystem catchSuccessEffect;

    void Awake()
    {
        character = GetComponent<CharacterBase>();
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        // Update catch window timer
        if (isCatchWindowActive)
        {
            catchWindowTimer -= Time.deltaTime;
            if (catchWindowTimer <= 0f)
            {
                EndCatchWindow();
            }
        }

        // Handle catch input
        if (InputManager.GetCatch() && !character.hasBall)
        {
            StartCatchWindow();
        }
    }

    void StartCatchWindow()
    {
        isCatchWindowActive = true;
        catchWindowTimer = catchWindow;

        if (catchIndicator != null)
        {
            catchIndicator.SetActive(true);
        }

        AudioManager.Instance?.PlaySound("CatchReady");
    }

    void EndCatchWindow()
    {
        isCatchWindowActive = false;

        if (catchIndicator != null)
        {
            catchIndicator.SetActive(false);
        }
    }

    public bool TryToCatch(BallController ball)
    {
        if (!isCatchWindowActive || character.hasBall || ball == null)
        {
            return false;
        }

        // Check if ball is within catch radius
        float distance = Vector3.Distance(transform.position, ball.transform.position);
        if (distance > catchRadius)
        {
            return false;
        }

        // Check if ball is moving towards player
        Vector3 ballDirection = ball.GetComponent<Rigidbody>().velocity.normalized;
        Vector3 toPlayer = (transform.position - ball.transform.position).normalized;
        float dot = Vector3.Dot(ballDirection, toPlayer);

        if (dot < 0.5f) // Ball not moving towards player
        {
            return false;
        }

        // Successful catch!
        ExecuteCatch(ball);
        return true;
    }

    void ExecuteCatch(BallController ball)
    {
        // Visual and audio feedback
        if (catchSuccessEffect != null)
        {
            catchSuccessEffect.Play();
        }

        AudioManager.Instance?.PlaySound("CatchSuccess");

        // Add ultimate charge for successful catch
        UltimateManager ultimateManager = character.GetComponent<UltimateManager>();
        ultimateManager?.AddCharge(0.20f);

        // End catch window
        EndCatchWindow();

        // Notify ball of pickup
        ball.OnPickup(photonView.Owner.ActorNumber);

        // Screen effect
        CameraShake.Instance?.Shake(0.1f, 0.2f);
    }

    void OnDrawGizmosSelected()
    {
        // Visualize catch radius in editor
        Gizmos.color = isCatchWindowActive ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, catchRadius);
    }
}