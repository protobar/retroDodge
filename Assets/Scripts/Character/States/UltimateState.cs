using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== ULTIMATE STATE ====================
public class UltimateState : ICharacterState
{
    private float ultimateDuration = 1.0f;
    private float ultimateStartTime;

    public void EnterState(CharacterBase character)
    {
        ultimateStartTime = Time.time;

        // Disable movement during ultimate
        character.GetComponent<CharacterBase>().canMove = false;

        // Play ultimate animation
        Animator animator = character.GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetTrigger("Ultimate");
        }

        // Ultimate visual effects
        StartUltimateEffects(character);

        // Camera effects
        CameraShake.Instance?.Shake(0.3f, ultimateDuration);
        DynamicCamera.Instance?.OnUltimateActivated();

        AudioManager.Instance?.PlaySound("UltimateStart");
    }

    public void UpdateState(CharacterBase character)
    {
        // Check if ultimate is complete
        if (Time.time - ultimateStartTime >= ultimateDuration)
        {
            // Return to appropriate state
            float horizontal = InputManager.GetHorizontal();
            StateType nextState = Mathf.Abs(horizontal) > 0.1f ? StateType.Moving : StateType.Idle;
            character.GetComponent<StateMachine>().ChangeState(nextState);
        }
    }

    public void ExitState(CharacterBase character)
    {
        // Re-enable movement
        character.GetComponent<CharacterBase>().canMove = true;

        // Stop ultimate effects
        StopUltimateEffects(character);
    }

    private void StartUltimateEffects(CharacterBase character)
    {
        // Activate ultimate particle effects
        GameObject ultimateEffect = character.transform.Find("UltimateEffect")?.gameObject;
        if (ultimateEffect != null)
        {
            ParticleSystem particles = ultimateEffect.GetComponent<ParticleSystem>();
            particles?.Play();
        }

        // Screen effects
        // TODO: Add screen distortion, color grading, etc.
    }

    private void StopUltimateEffects(CharacterBase character)
    {
        // Stop ultimate particle effects
        GameObject ultimateEffect = character.transform.Find("UltimateEffect")?.gameObject;
        if (ultimateEffect != null)
        {
            ParticleSystem particles = ultimateEffect.GetComponent<ParticleSystem>();
            particles?.Stop();
        }
    }
}