using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== THROW STATE ====================
public class ThrowState : ICharacterState
{
    private float throwDuration = 0.5f;
    private float throwStartTime;

    public void EnterState(CharacterBase character)
    {
        throwStartTime = Time.time;

        // Disable movement temporarily
        character.GetComponent<CharacterBase>().canMove = false;

        // Play throw animation
        Animator animator = character.GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetTrigger("Throw");
        }
    }

    public void UpdateState(CharacterBase character)
    {
        // Check if throw animation is complete
        if (Time.time - throwStartTime >= throwDuration)
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
    }
}