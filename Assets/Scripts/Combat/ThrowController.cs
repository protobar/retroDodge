using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

// ==================== THROW CONTROLLER ====================
public class ThrowController : MonoBehaviourPun
{
    [Header("Throw Settings")]
    public Transform throwOrigin;
    public LineRenderer aimLine;
    public LayerMask obstacleLayer;

    private CharacterBase character;
    private BallController heldBall;
    private bool isCharging = false;
    private float chargeTime = 0f;
    private Vector3 throwDirection;

    void Awake()
    {
        character = GetComponent<CharacterBase>();

        if (throwOrigin == null)
        {
            // Create throw origin point
            GameObject throwPoint = new GameObject("ThrowOrigin");
            throwPoint.transform.SetParent(transform);
            throwPoint.transform.localPosition = new Vector3(0, 1.5f, 0.5f);
            throwOrigin = throwPoint.transform;
        }
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        UpdateAiming();

        if (isCharging)
        {
            chargeTime += Time.deltaTime;
            UpdateChargeEffects();
        }
    }

    void UpdateAiming()
    {
        if (character.hasBall && aimLine != null)
        {
            // Calculate throw direction (simplified for 2D-style gameplay)
            float horizontal = InputManager.GetHorizontal();
            throwDirection = horizontal >= 0 ? Vector3.right : Vector3.left;

            // Show aim line
            Vector3 startPos = throwOrigin.position;
            Vector3 endPos = startPos + throwDirection * 10f;

            // Check for obstacles
            RaycastHit hit;
            if (Physics.Raycast(startPos, throwDirection, out hit, 10f, obstacleLayer))
            {
                endPos = hit.point;
            }

            aimLine.enabled = true;
            aimLine.SetPosition(0, startPos);
            aimLine.SetPosition(1, endPos);

            // Color code based on charge
            Color aimColor = Color.Lerp(Color.white, Color.red, chargeTime / 1.5f);
            aimLine.startColor = aimColor;
            aimLine.endColor = aimColor;
        }
        else if (aimLine != null)
        {
            aimLine.enabled = false;
        }
    }

    public void StartCharging()
    {
        if (!character.hasBall) return;

        isCharging = true;
        chargeTime = 0f;

        // Get the held ball
        heldBall = FindHeldBall();

        AudioManager.Instance?.PlaySound("ChargeStart");
    }

    public void ExecuteThrow()
    {
        if (!character.hasBall || heldBall == null) return;

        isCharging = false;

        // Determine throw type
        BallController.BallType throwType;
        if (character.GetComponent<UltimateManager>().CanUseUltimate() && InputManager.GetUltimate())
        {
            throwType = BallController.BallType.Ultimate;
        }
        else if (!character.GetComponent<CharacterController>().isGrounded)
        {
            throwType = BallController.BallType.Jump;
        }
        else if (chargeTime >= 1.5f)
        {
            throwType = BallController.BallType.Charged;
        }
        else
        {
            throwType = BallController.BallType.Basic;
        }

        // Calculate throw power
        float throwPower = CalculateThrowPower(throwType);

        // Launch the ball
        heldBall.LaunchBall(throwDirection, throwPower, throwType, photonView.Owner.ActorNumber);

        // Character-specific throw behavior
        ExecuteCharacterSpecificThrow(throwType);

        // Release ball from character
        character.ReleaseBall();
        heldBall = null;
        chargeTime = 0f;

        // Add ultimate charge
        UltimateManager ultimateManager = character.GetComponent<UltimateManager>();
        float chargeBonus = character.eligibleForQuickThrowBonus ? 0.05f : 0f;
        ultimateManager?.AddCharge(0.15f + chargeBonus);

        AudioManager.Instance?.PlaySound("Throw");
    }

    float CalculateThrowPower(BallController.BallType throwType)
    {
        float basePower = character.stats.throwPowerMultiplier;

        switch (throwType)
        {
            case BallController.BallType.Basic:
                return basePower;
            case BallController.BallType.Charged:
                return basePower * 1.5f;
            case BallController.BallType.Jump:
                return basePower * 1.2f;
            case BallController.BallType.Ultimate:
                return basePower * 2.0f;
            default:
                return basePower;
        }
    }

    void ExecuteCharacterSpecificThrow(BallController.BallType throwType)
    {
        switch (throwType)
        {
            case BallController.BallType.Basic:
                character.OnBasicThrow();
                break;
            case BallController.BallType.Charged:
                character.OnChargedThrow();
                break;
            case BallController.BallType.Jump:
                character.OnJumpThrow();
                break;
            case BallController.BallType.Ultimate:
                character.OnUltimate();
                break;
        }
    }

    void UpdateChargeEffects()
    {
        // Visual charging effects
        if (heldBall != null)
        {
            float chargePercent = Mathf.Clamp01(chargeTime / 1.5f);

            // Ball glow effect
            Renderer ballRenderer = heldBall.GetComponent<Renderer>();
            if (ballRenderer != null)
            {
                Color glowColor = Color.Lerp(Color.white, Color.red, chargePercent);
                ballRenderer.material.SetColor("_EmissionColor", glowColor * chargePercent);
            }
        }

        // Haptic feedback for mobile
        if (chargeTime >= 1.5f && Time.time % 0.2f < Time.deltaTime)
        {
            // Vibrate controller/phone
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }
    }

    BallController FindHeldBall()
    {
        // Find ball attached to this character
        BallController[] balls = FindObjectsOfType<BallController>();
        foreach (var ball in balls)
        {
            if (ball.currentHolderID == photonView.Owner.ActorNumber)
            {
                return ball;
            }
        }
        return null;
    }
}