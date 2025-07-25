using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== IDLE STATE ====================
public class IdleState : ICharacterState
{
    public void EnterState(CharacterBase character)
    {
        // Set idle animation
        // Reset any movement-related variables
    }

    public void UpdateState(CharacterBase character)
    {
        // Check for transitions to other states
        float horizontal = InputManager.GetHorizontal();

        if (Mathf.Abs(horizontal) > 0.1f)
        {
            character.GetComponent<StateMachine>().ChangeState(StateType.Moving);
        }
    }

    public void ExitState(CharacterBase character)
    {
        // Cleanup idle state
    }
}