using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== HIT STATE ====================
public class HitState : ICharacterState
{
    private float hitStunDuration = 0.4f;
    private float hitStartTime;

    public void EnterState(CharacterBase character)
    {
        hitStartTime = Time.time;

        // Disable movement during hitstun
        character.GetComponent<CharacterBase>().canMove = false;

        // Play hit animation
        Animator animator = character.GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetTrigger("Hit");
        }

        // Visual feedback
        StartHitEffect(character);

        AudioManager.Instance?.PlaySound("Hit");
    }

    public void UpdateState(CharacterBase character)
    {
        // Check if hitstun is over
        if (Time.time - hitStartTime >= hitStunDuration)
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

        // Stop hit effects
        StopHitEffect(character);
    }

    private void StartHitEffect(CharacterBase character)
    {
        // Flash red effect
        Renderer renderer = character.GetComponent<Renderer>();
        if (renderer != null)
        {
            // Start coroutine for flashing effect
            character.StartCoroutine(FlashEffect(renderer));
        }
    }

    private void StopHitEffect(CharacterBase character)
    {
        // Reset color
        Renderer renderer = character.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = Color.white;
        }
    }

    private System.Collections.IEnumerator FlashEffect(Renderer renderer)
    {
        float flashDuration = 0.1f;
        int flashCount = 4;

        for (int i = 0; i < flashCount; i++)
        {
            renderer.material.color = Color.red;
            yield return new WaitForSeconds(flashDuration);
            renderer.material.color = Color.white;
            yield return new WaitForSeconds(flashDuration);
        }
    }
}