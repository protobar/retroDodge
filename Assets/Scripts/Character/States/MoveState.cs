using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== MOVE STATE ====================
public class MoveState : ICharacterState
{
    public void EnterState(CharacterBase character)
    {
        // Set movement animation
    }

    public void UpdateState(CharacterBase character)
    {
        float horizontal = InputManager.GetHorizontal();

        if (Mathf.Abs(horizontal) < 0.1f)
        {
            character.GetComponent<StateMachine>().ChangeState(StateType.Idle);
        }
    }

    public void ExitState(CharacterBase character)
    {
        // Cleanup movement state
    }
}