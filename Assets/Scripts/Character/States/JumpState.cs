using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== JUMP STATE ====================
public class JumpState : ICharacterState
{
    private float jumpStartTime;

    public void EnterState(CharacterBase character)
    {
        // Set jump animation
        jumpStartTime = Time.time;
        AudioManager.Instance?.PlaySound("Jump");
    }

    public void UpdateState(CharacterBase character)
    {
        // Check if landed
        if (character.GetComponent<CharacterController>().isGrounded && Time.time - jumpStartTime > 0.1f)
        {
            // Transition back to appropriate state
            float horizontal = InputManager.GetHorizontal();
            StateType nextState = Mathf.Abs(horizontal) > 0.1f ? StateType.Moving : StateType.Idle;
            character.GetComponent<StateMachine>().ChangeState(nextState);
        }
    }

    public void ExitState(CharacterBase character)
    {
        // Cleanup jump state
    }
}