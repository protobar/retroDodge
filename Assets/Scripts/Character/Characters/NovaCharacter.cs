using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

// ==================== NOVA CHARACTER - THE LIGHTNING DANCER ====================
public class NovaCharacter : CharacterBase
{
    [Header("Nova Specific")]
    public GameObject lightningTrailPrefab;
    public GameObject thunderBarragePrefab;
    public ParticleSystem lightningAura;
    public Transform[] airDashTrailPoints;

    // Air dash ability
    private bool canAirDash = true;
    private float airDashCooldown = 2f;
    private float lastAirDashTime = 0f;
    private int jumpCount = 0;
    private int maxJumps = 1;

    protected override void Start()
    {
        base.Start();

        // Set Nova-specific stats if not using ScriptableObject
        if (stats == null)
        {
            stats = ScriptableObject.CreateInstance<CharacterStats>();
            stats.maxHealth = 80f;
            stats.movementSpeed = 6.5f;
            stats.jumpHeight = 5.0f;
            stats.damageMultiplier = 1.0f;
            stats.hasAirDash = true;
            stats.airDashCooldown = 2.0f;
            stats.characterName = "Nova";
            stats.ultimateDamage = 25; // 5 HP x 5 shots
        }

        currentHealth = stats.maxHealth;
        airDashCooldown = stats.airDashCooldown;
    }

    protected override void Update()
    {
        base.Update();

        // Handle air dash cooldown
        UpdateAirDashCooldown();

        // Handle triple jump for jump throws
        HandleMultipleJumps();

        // Handle air dash input
        if (photonView.IsMine && !isGrounded && canAirDash)
        {
            HandleAirDash();
        }
    }

    void UpdateAirDashCooldown()
    {
        if (!canAirDash && Time.time - lastAirDashTime >= airDashCooldown)
        {
            canAirDash = true;

            // Visual feedback that air dash is ready
            if (lightningAura != null)
            {
                lightningAura.Play();
                StartCoroutine(StopLightningAura());
            }
        }
    }

    void HandleMultipleJumps()
    {
        if (isGrounded)
        {
            jumpCount = 0;
        }
    }

    void HandleAirDash()
    {
        float horizontal = InputManager.GetHorizontal();

        // Double-tap detection for air dash
        if (Mathf.Abs(horizontal) > 0.8f)
        {
            ExecuteAirDash(horizontal > 0 ? Vector3.right : Vector3.left);
        }
    }

    void ExecuteAirDash(Vector3 direction)
    {
        canAirDash = false;
        lastAirDashTime = Time.time;

        // Apply air dash velocity
        Vector3 dashVelocity = direction * 15f;
        verticalVelocity = 0f; // Reset fall velocity

        // Move character
        characterController.Move(dashVelocity * 0.2f); // Quick burst movement

        // Visual effects
        CreateAirDashEffect(direction);

        AudioManager.Instance?.PlaySound("NovaAirDash");

        // Network sync
        photonView.RPC("SyncAirDash", RpcTarget.Others, direction);
    }

    public override void OnBasicThrow()
    {
        // Quick straight shot (fastest projectile speed)
        BallController ball = BallPool.Instance.GetBall();
        if (ball == null) return;

        Vector3 throwDirection = transform.forward;

        ball.LaunchBall(throwDirection, 1.2f * stats.throwPowerMultiplier, // Faster than normal
            BallController.BallType.Basic, photonView.Owner.ActorNumber);

        ball.damage = Mathf.RoundToInt(10 * stats.damageMultiplier);

        // Lightning trail effect
        AttachLightningTrailToBall(ball);

        AudioManager.Instance?.PlaySound("NovaThrow");
    }

    public override void OnChargedThrow()
    {
        // Zigzag pattern (3 direction changes)
        BallController ball = BallPool.Instance.GetBall();
        if (ball == null) return;

        Vector3 throwDirection = transform.forward;

        ball.LaunchBall(throwDirection, 1.3f * stats.throwPowerMultiplier,
            BallController.BallType.Charged, photonView.Owner.ActorNumber);

        ball.damage = Mathf.RoundToInt(15 * stats.damageMultiplier);

        // Set zigzag pattern
        StartCoroutine(ApplyZigzagPattern(ball));

        AttachLightningTrailToBall(ball);

        AudioManager.Instance?.PlaySound("NovaChargedThrow");
        CameraShake.Instance?.Shake(0.2f, 0.4f);
    }

    public override void OnJumpThrow()
    {
        // Triple jump enabled, downward angle throw
        BallController ball = BallPool.Instance.GetBall();
        if (ball == null) return;

        // Enable triple jump temporarily
        maxJumps = 3;
        StartCoroutine(ResetJumpCount());

        // Downward angled throw
        Vector3 throwDirection = (transform.forward + Vector3.down * 0.3f).normalized;

        ball.LaunchBall(throwDirection, 1.1f * stats.throwPowerMultiplier,
            BallController.BallType.Jump, photonView.Owner.ActorNumber);

        ball.damage = Mathf.RoundToInt(12 * stats.damageMultiplier);

        AttachLightningTrailToBall(ball);

        AudioManager.Instance?.PlaySound("NovaJumpThrow");
    }

    public override void OnUltimate()
    {
        // Thunder Barrage: 5 rapid balls (5 HP each, 0.2s between shots)
        StartCoroutine(ThunderBarrage());
    }

    IEnumerator ThunderBarrage()
    {
        // Enter ultimate state
        stateMachine.ChangeState(StateType.Ultimate);

        // Lightning aura effect
        if (lightningAura != null)
        {
            lightningAura.Play();
        }

        AudioManager.Instance?.PlaySound("NovaUltimateStart");

        // Fire 5 rapid shots
        for (int i = 0; i < 5; i++)
        {
            BallController ball = BallPool.Instance.GetBall();
            if (ball != null)
            {
                // Slight angle variation for each shot
                float angleOffset = (i - 2) * 15f; // -30, -15, 0, 15, 30 degrees
                Vector3 direction = Quaternion.Euler(0, angleOffset, 0) * transform.forward;

                ball.LaunchBall(direction, 1.0f * stats.throwPowerMultiplier,
                    BallController.BallType.Ultimate, photonView.Owner.ActorNumber);

                ball.damage = 5; // Each shot does 5 HP

                AttachThunderEffectToBall(ball);

                // Visual and audio for each shot
                AudioManager.Instance?.PlaySound("NovaThunderShot");
                CameraShake.Instance?.Shake(0.1f, 0.1f);
            }

            // Wait between shots
            if (i < 4) // Don't wait after the last shot
            {
                yield return new WaitForSeconds(0.2f);
            }
        }

        // Reset ultimate meter
        ultimateManager.UseUltimate();

        // Stop lightning aura
        yield return new WaitForSeconds(0.5f);
        if (lightningAura != null)
        {
            lightningAura.Stop();
        }

        AudioManager.Instance?.PlaySound("NovaUltimateEnd");
    }

    protected override void Jump()
    {
        if (jumpCount < maxJumps)
        {
            base.Jump();
            jumpCount++;

            // Lightning effect on extra jumps
            if (jumpCount > 1)
            {
                CreateJumpLightningEffect();
                AudioManager.Instance?.PlaySound("NovaMultiJump");
            }
        }
    }

    IEnumerator ApplyZigzagPattern(BallController ball)
    {
        if (ball == null) yield break;

        Rigidbody ballRb = ball.GetComponent<Rigidbody>();
        if (ballRb == null) yield break;

        int zigzagCount = 0;
        float zigzagInterval = 0.3f;

        while (zigzagCount < 3 && ball.isActive)
        {
            yield return new WaitForSeconds(zigzagInterval);

            if (ball != null && ball.isActive)
            {
                // Change direction slightly
                Vector3 currentVelocity = ballRb.velocity;
                float zigzagAngle = (zigzagCount % 2 == 0) ? 30f : -30f;
                Vector3 newDirection = Quaternion.Euler(0, zigzagAngle, 0) * currentVelocity.normalized;
                ballRb.velocity = newDirection * currentVelocity.magnitude;

                // Lightning effect at direction change
                CreateLightningZigzagEffect(ball.transform.position);

                zigzagCount++;
            }
        }
    }

    IEnumerator ResetJumpCount()
    {
        yield return new WaitForSeconds(3f);
        maxJumps = 1;
    }

    IEnumerator StopLightningAura()
    {
        yield return new WaitForSeconds(1f);
        if (lightningAura != null)
        {
            lightningAura.Stop();
        }
    }

    void AttachLightningTrailToBall(BallController ball)
    {
        if (lightningTrailPrefab != null)
        {
            GameObject lightningEffect = Instantiate(lightningTrailPrefab, ball.transform);
            lightningEffect.transform.localPosition = Vector3.zero;

            // Destroy effect when ball is reset
            StartCoroutine(DestroyEffectOnBallReset(lightningEffect, ball));
        }
    }

    void AttachThunderEffectToBall(BallController ball)
    {
        if (thunderBarragePrefab != null)
        {
            GameObject thunderEffect = Instantiate(thunderBarragePrefab, ball.transform);
            thunderEffect.transform.localPosition = Vector3.zero;

            // Destroy effect when ball is reset
            StartCoroutine(DestroyEffectOnBallReset(thunderEffect, ball));
        }
    }

    IEnumerator DestroyEffectOnBallReset(GameObject effect, BallController ball)
    {
        while (ball != null && ball.isActive)
        {
            yield return new WaitForSeconds(0.1f);
        }

        if (effect != null)
        {
            Destroy(effect);
        }
    }

    void CreateAirDashEffect(Vector3 direction)
    {
        if (EffectsPool.Instance != null)
        {
            GameObject effect = EffectsPool.Instance.GetEffect("NovaAirDash");
            if (effect != null)
            {
                effect.transform.position = transform.position;
                effect.transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }

    void CreateJumpLightningEffect()
    {
        if (EffectsPool.Instance != null)
        {
            GameObject effect = EffectsPool.Instance.GetEffect("NovaMultiJump");
            if (effect != null)
            {
                effect.transform.position = transform.position;
            }
        }
    }

    void CreateLightningZigzagEffect(Vector3 position)
    {
        if (EffectsPool.Instance != null)
        {
            GameObject effect = EffectsPool.Instance.GetEffect("NovaZigzag");
            if (effect != null)
            {
                effect.transform.position = position;
            }
        }
    }

    [PunRPC]
    void SyncAirDash(Vector3 direction)
    {
        // Sync air dash for other clients
        CreateAirDashEffect(direction);
        AudioManager.Instance?.PlaySound("NovaAirDash");
    }
}