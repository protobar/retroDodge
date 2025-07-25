using UnityEngine;
using Photon.Pun;
using System.Collections;

// ==================== GRUDGE CHARACTER - THE INFERNO BRUISER ====================
public class GrudgeCharacter : CharacterBase
{
    [Header("Grudge Specific")]
    public GameObject fireballPrefab;
    public GameObject meteorPrefab;
    public ParticleSystem fireAura;
    public ParticleSystem groundPoundEffect;

    private float fireballChargeTime = 0f;

    protected override void Start()
    {
        base.Start();

        // Set Grudge-specific stats if not using ScriptableObject
        if (stats == null)
        {
            // Create default stats for Grudge
            stats = ScriptableObject.CreateInstance<CharacterStats>();
            stats.maxHealth = 120f;
            stats.movementSpeed = 4.0f;
            stats.jumpHeight = 3.5f;
            stats.damageMultiplier = 1.3f;
            stats.characterName = "Grudge";
            stats.ultimateDamage = 30;
        }

        currentHealth = stats.maxHealth;
    }

    public override void OnBasicThrow()
    {
        // Heavy straight shot with slight downward arc
        BallController ball = BallPool.Instance.GetBall();
        if (ball == null) return;

        Vector3 throwDirection = transform.forward + Vector3.down * 0.1f; // Slight downward arc

        ball.LaunchBall(throwDirection, 1f * stats.throwPowerMultiplier,
            BallController.BallType.Basic, photonView.Owner.ActorNumber);

        // Apply damage multiplier
        ball.damage = Mathf.RoundToInt(ball.damage * stats.damageMultiplier);

        // Visual effects
        CreateThrowEffect("Basic");

        AudioManager.Instance?.PlaySound("GrudgeThrow");

        // Screen shake for heavy throw
        CameraShake.Instance?.Shake(0.2f, 0.3f);
    }

    public override void OnChargedThrow()
    {
        // Blazing fastball with fire particle trail
        BallController ball = BallPool.Instance.GetBall();
        if (ball == null) return;

        // Add fire effects to the ball
        AttachFireEffectToBall(ball);

        Vector3 throwDirection = transform.forward;
        ball.LaunchBall(throwDirection, 1.5f * stats.throwPowerMultiplier,
            BallController.BallType.Charged, photonView.Owner.ActorNumber);

        ball.damage = Mathf.RoundToInt(15 * stats.damageMultiplier);

        // Visual effects
        CreateThrowEffect("Charged");

        // Screen shake for power
        CameraShake.Instance?.Shake(0.3f, 0.5f);
        AudioManager.Instance?.PlaySound("GrudgeChargedThrow");

        // Fire aura briefly activates
        if (fireAura != null)
        {
            fireAura.Play();
            StartCoroutine(StopFireAura());
        }
    }

    public override void OnJumpThrow()
    {
        // Ground-pound throw that bounces once before continuing
        BallController ball = BallPool.Instance.GetBall();
        if (ball == null) return;

        // Special trajectory - aimed downward then bounces
        Vector3 throwDirection = (transform.forward + Vector3.down * 0.5f).normalized;

        ball.LaunchBall(throwDirection, 1.2f * stats.throwPowerMultiplier,
            BallController.BallType.Jump, photonView.Owner.ActorNumber);

        ball.damage = Mathf.RoundToInt(12 * stats.damageMultiplier);

        // Ground pound visual effect
        if (groundPoundEffect != null)
        {
            groundPoundEffect.transform.position = transform.position;
            groundPoundEffect.Play();
        }

        CreateThrowEffect("Jump");

        AudioManager.Instance?.PlaySound("GrudgeJumpThrow");
        CameraShake.Instance?.Shake(0.4f, 0.6f);
    }

    public override void OnUltimate()
    {
        // Meteor Strike - Massive fireball with wind-up
        StartCoroutine(MeteorStrike());
    }

    IEnumerator MeteorStrike()
    {
        // Enter ultimate state
        stateMachine.ChangeState(StateType.Ultimate);

        // Wind-up phase with fire aura
        if (fireAura != null)
        {
            fireAura.Play();
        }

        // Wind-up animation and effects
        AudioManager.Instance?.PlaySound("GrudgeUltimateWindup");

        // Charge effect for 0.5 seconds
        yield return new WaitForSeconds(0.5f);

        // Create meteor ball
        BallController ball = BallPool.Instance.GetBall();
        if (ball != null)
        {
            // Attach meteor effect
            AttachMeteorEffectToBall(ball);

            // Launch with massive force
            Vector3 throwDirection = transform.forward;
            ball.LaunchBall(throwDirection, 2f * stats.throwPowerMultiplier,
                BallController.BallType.Ultimate, photonView.Owner.ActorNumber);

            ball.damage = stats.ultimateDamage;

            // Ultimate visual and audio effects
            CreateUltimateEffect();

            // Camera effects
            CameraShake.Instance?.Shake(0.5f, 1f);
            DynamicCamera.Instance?.OnUltimateActivated();

            AudioManager.Instance?.PlaySound("GrudgeUltimate");
        }

        // Reset ultimate meter
        ultimateManager.UseUltimate();

        // Stop fire aura after a delay
        yield return new WaitForSeconds(1f);
        if (fireAura != null)
        {
            fireAura.Stop();
        }
    }

    void AttachFireEffectToBall(BallController ball)
    {
        if (fireballPrefab != null)
        {
            GameObject fireEffect = Instantiate(fireballPrefab, ball.transform);
            fireEffect.transform.localPosition = Vector3.zero;

            // Destroy effect when ball is reset
            StartCoroutine(DestroyEffectOnBallReset(fireEffect, ball));
        }
    }

    void AttachMeteorEffectToBall(BallController ball)
    {
        if (meteorPrefab != null)
        {
            GameObject meteorEffect = Instantiate(meteorPrefab, ball.transform);
            meteorEffect.transform.localPosition = Vector3.zero;

            // Destroy effect when ball is reset
            StartCoroutine(DestroyEffectOnBallReset(meteorEffect, ball));
        }
    }

    IEnumerator DestroyEffectOnBallReset(GameObject effect, BallController ball)
    {
        // Wait until ball is no longer active
        while (ball != null && ball.isActive)
        {
            yield return new WaitForSeconds(0.1f);
        }

        if (effect != null)
        {
            Destroy(effect);
        }
    }

    IEnumerator StopFireAura()
    {
        yield return new WaitForSeconds(2f);
        if (fireAura != null)
        {
            fireAura.Stop();
        }
    }

    void CreateThrowEffect(string throwType)
    {
        // Create throw-specific visual effects
        if (EffectsPool.Instance != null)
        {
            GameObject effect = EffectsPool.Instance.GetEffect($"Grudge{throwType}Throw");
            if (effect != null)
            {
                effect.transform.position = transform.position;
            }
        }
    }

    void CreateUltimateEffect()
    {
        // Screen flash effect
        ScreenEffects.Instance?.FlashScreen(Color.red, 0.3f);

        // Particle explosion at character position
        if (EffectsPool.Instance != null)
        {
            GameObject effect = EffectsPool.Instance.GetEffect("GrudgeUltimate");
            if (effect != null)
            {
                effect.transform.position = transform.position;
            }
        }
    }
}