using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

// ==================== ECHO CHARACTER - THE ILLUSIONIST ====================
public class EchoCharacter : CharacterBase
{
    [Header("Echo Specific")]
    public GameObject fakeBallPrefab;
    public GameObject mirrorMazePrefab;
    public ParticleSystem illusionAura;
    public Material echoMaterial;

    // Illusion tracking
    private bool isUsingPhantomBall = false;
    private BallController phantomBall;
    private Vector3 lastDirectionChange;

    protected override void Start()
    {
        base.Start();

        // Set Echo-specific stats if not using ScriptableObject
        if (stats == null)
        {
            stats = ScriptableObject.CreateInstance<CharacterStats>();
            stats.maxHealth = 100f;
            stats.movementSpeed = 5.0f;
            stats.jumpHeight = 4.0f;
            stats.damageMultiplier = 1.1f;
            stats.characterName = "Echo";
            stats.ultimateDamage = 25;
        }

        currentHealth = stats.maxHealth;
    }

    protected override void Update()
    {
        base.Update();

        // Handle phantom ball control during charged throw
        if (isUsingPhantomBall && phantomBall != null)
        {
            HandlePhantomBallControl();
        }
    }

    void HandlePhantomBallControl()
    {
        if (!photonView.IsMine) return;

        // Allow player to control phantom ball direction mid-flight
        float horizontal = InputManager.GetHorizontal();

        if (Mathf.Abs(horizontal) > 0.5f && Vector3.Distance(lastDirectionChange, phantomBall.transform.position) > 2f)
        {
            Vector3 newDirection = horizontal > 0 ? Vector3.right : Vector3.left;
            ChangePhantomBallDirection(newDirection);
            lastDirectionChange = phantomBall.transform.position;
        }
    }

    public override void OnBasicThrow()
    {
        // Curves 15 degrees left or right randomly
        BallController ball = BallPool.Instance.GetBall();
        if (ball == null) return;

        // Random curve direction
        bool curveLeft = Random.Range(0f, 1f) > 0.5f;
        float curveAngle = curveLeft ? -15f : 15f;

        Vector3 throwDirection = Quaternion.Euler(0, curveAngle, 0) * transform.forward;

        ball.LaunchBall(throwDirection, 1f * stats.throwPowerMultiplier,
            BallController.BallType.Basic, photonView.Owner.ActorNumber);

        ball.damage = Mathf.RoundToInt(10 * stats.damageMultiplier);

        // Add curve physics to the ball
        Vector3 curveDirection = curveLeft ? Vector3.left : Vector3.right;
        ball.SetCurve(curveDirection, 5f);

        // Visual effect for curve
        CreateCurveEffect(ball, curveLeft);

        AudioManager.Instance?.PlaySound("EchoThrow");
    }

    public override void OnChargedThrow()
    {
        // Phantom Ball: Player controls direction change mid-flight
        BallController ball = BallPool.Instance.GetBall();
        if (ball == null) return;

        Vector3 throwDirection = transform.forward;

        ball.LaunchBall(throwDirection, 1.3f * stats.throwPowerMultiplier,
            BallController.BallType.Charged, photonView.Owner.ActorNumber);

        ball.damage = Mathf.RoundToInt(15 * stats.damageMultiplier);

        // Set up phantom ball control
        phantomBall = ball;
        isUsingPhantomBall = true;
        lastDirectionChange = ball.transform.position;

        // Visual effect for phantom ball
        AttachPhantomEffectToBall(ball);

        // Stop phantom control after 3 seconds
        StartCoroutine(StopPhantomControl());

        AudioManager.Instance?.PlaySound("EchoPhantomThrow");
        CameraShake.Instance?.Shake(0.25f, 0.4f);
    }

    public override void OnJumpThrow()
    {
        // Creates one fake ball (visual only) alongside real ball
        BallController realBall = BallPool.Instance.GetBall();
        if (realBall == null) return;

        Vector3 throwDirection = (transform.forward + Vector3.up * 0.2f).normalized;

        realBall.LaunchBall(throwDirection, 1.1f * stats.throwPowerMultiplier,
            BallController.BallType.Jump, photonView.Owner.ActorNumber);

        realBall.damage = Mathf.RoundToInt(12 * stats.damageMultiplier);

        // Create fake ball
        CreateFakeBall(throwDirection);

        AudioManager.Instance?.PlaySound("EchoJumpThrow");
    }

    public override void OnUltimate()
    {
        // Mirror Maze: Ball splits into 3, only one deals damage (25 HP)
        StartCoroutine(MirrorMaze());
    }

    IEnumerator MirrorMaze()
    {
        // Enter ultimate state
        stateMachine.ChangeState(StateType.Ultimate);

        // Illusion aura effect
        if (illusionAura != null)
        {
            illusionAura.Play();
        }

        AudioManager.Instance?.PlaySound("EchoUltimateStart");

        // Brief wind-up
        yield return new WaitForSeconds(0.3f);

        // Create 3 balls - 1 real, 2 fake
        Vector3 baseDirection = transform.forward;

        // Real ball (center)
        BallController realBall = BallPool.Instance.GetBall();
        if (realBall != null)
        {
            realBall.LaunchBall(baseDirection, 1.5f * stats.throwPowerMultiplier,
                BallController.BallType.Ultimate, photonView.Owner.ActorNumber);
            realBall.damage = stats.ultimateDamage;

            AttachMirrorEffectToBall(realBall, true); // Mark as real
        }

        // Fake ball (left)
        Vector3 leftDirection = Quaternion.Euler(0, -20f, 0) * baseDirection;
        CreateAdvancedFakeBall(leftDirection, false);

        // Fake ball (right)
        Vector3 rightDirection = Quaternion.Euler(0, 20f, 0) * baseDirection;
        CreateAdvancedFakeBall(rightDirection, false);

        // Reset ultimate meter
        ultimateManager.UseUltimate();

        // Screen distortion effect
        ScreenEffects.Instance?.FlashScreen(Color.cyan, 0.4f);
        CameraShake.Instance?.Shake(0.4f, 0.8f);

        AudioManager.Instance?.PlaySound("EchoMirrorMaze");

        // Stop illusion aura
        yield return new WaitForSeconds(1f);
        if (illusionAura != null)
        {
            illusionAura.Stop();
        }
    }

    void ChangePhantomBallDirection(Vector3 newDirection)
    {
        if (phantomBall != null && phantomBall.isActive)
        {
            Rigidbody ballRb = phantomBall.GetComponent<Rigidbody>();
            if (ballRb != null)
            {
                float currentSpeed = ballRb.velocity.magnitude;
                ballRb.velocity = newDirection * currentSpeed;

                // Visual effect at direction change
                CreateDirectionChangeEffect(phantomBall.transform.position);
                AudioManager.Instance?.PlaySound("EchoDirectionChange");
            }
        }
    }

    void CreateFakeBall(Vector3 direction)
    {
        if (fakeBallPrefab != null)
        {
            GameObject fakeBall = Instantiate(fakeBallPrefab);
            fakeBall.transform.position = transform.position + Vector3.up * 1.5f;

            // Launch fake ball with slightly different trajectory
            Vector3 fakeDirection = Quaternion.Euler(0, Random.Range(-10f, 10f), 0) * direction;

            StartCoroutine(MoveFakeBall(fakeBall, fakeDirection));
        }
    }

    void CreateAdvancedFakeBall(Vector3 direction, bool isReal)
    {
        if (fakeBallPrefab != null)
        {
            GameObject fakeBall = Instantiate(fakeBallPrefab);
            fakeBall.transform.position = transform.position + Vector3.up * 1.5f;

            // Add mirror effect
            AttachMirrorEffectToFakeBall(fakeBall, isReal);

            StartCoroutine(MoveFakeBall(fakeBall, direction));
        }
    }

    IEnumerator MoveFakeBall(GameObject fakeBall, Vector3 direction)
    {
        float speed = 20f;
        float lifetime = 3f;
        float elapsed = 0f;

        while (elapsed < lifetime && fakeBall != null)
        {
            // Move fake ball
            fakeBall.transform.Translate(direction * speed * Time.deltaTime, Space.World);

            // Apply gravity
            direction.y += Physics.gravity.y * Time.deltaTime;

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Destroy fake ball
        if (fakeBall != null)
        {
            CreateFakeBallDisappearEffect(fakeBall.transform.position);
            Destroy(fakeBall);
        }
    }

    IEnumerator StopPhantomControl()
    {
        yield return new WaitForSeconds(3f);
        isUsingPhantomBall = false;
        phantomBall = null;
    }

    void AttachPhantomEffectToBall(BallController ball)
    {
        // Add ghostly trail effect
        TrailRenderer trail = ball.GetComponent<TrailRenderer>();
        if (trail != null)
        {
            trail.enabled = true;
            trail.startColor = Color.cyan;
            trail.endColor = Color.clear;
        }

        // Add phantom glow
        Renderer ballRenderer = ball.GetComponent<Renderer>();
        if (ballRenderer != null && echoMaterial != null)
        {
            ballRenderer.material = echoMaterial;
        }
    }

    void AttachMirrorEffectToBall(BallController ball, bool isReal)
    {
        if (mirrorMazePrefab != null)
        {
            GameObject mirrorEffect = Instantiate(mirrorMazePrefab, ball.transform);
            mirrorEffect.transform.localPosition = Vector3.zero;

            // Different colors for real vs fake
            ParticleSystem particles = mirrorEffect.GetComponent<ParticleSystem>();
            if (particles != null)
            {
                var main = particles.main;
                main.startColor = isReal ? Color.white : Color.cyan;
            }

            StartCoroutine(DestroyEffectOnBallReset(mirrorEffect, ball));
        }
    }

    void AttachMirrorEffectToFakeBall(GameObject fakeBall, bool isReal)
    {
        if (mirrorMazePrefab != null)
        {
            GameObject mirrorEffect = Instantiate(mirrorMazePrefab, fakeBall.transform);
            mirrorEffect.transform.localPosition = Vector3.zero;

            // Different colors for real vs fake
            ParticleSystem particles = mirrorEffect.GetComponent<ParticleSystem>();
            if (particles != null)
            {
                var main = particles.main;
                main.startColor = isReal ? Color.white : Color.cyan;
            }
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

    void CreateCurveEffect(BallController ball, bool curveLeft)
    {
        if (EffectsPool.Instance != null)
        {
            string effectName = curveLeft ? "EchoCurveLeft" : "EchoCurveRight";
            GameObject effect = EffectsPool.Instance.GetEffect(effectName);
            if (effect != null)
            {
                effect.transform.position = ball.transform.position;
            }
        }
    }

    void CreateDirectionChangeEffect(Vector3 position)
    {
        if (EffectsPool.Instance != null)
        {
            GameObject effect = EffectsPool.Instance.GetEffect("EchoDirectionChange");
            if (effect != null)
            {
                effect.transform.position = position;
            }
        }
    }

    void CreateFakeBallDisappearEffect(Vector3 position)
    {
        if (EffectsPool.Instance != null)
        {
            GameObject effect = EffectsPool.Instance.GetEffect("EchoFakeDisappear");
            if (effect != null)
            {
                effect.transform.position = position;
            }
        }
    }
}