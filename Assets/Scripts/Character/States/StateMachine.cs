using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StateMachine : MonoBehaviour
{
    private CharacterBase character;
    private ICharacterState currentState;
    private Dictionary<StateType, ICharacterState> states;

    public void Initialize(CharacterBase characterBase)
    {
        character = characterBase;
        InitializeStates();
        ChangeState(StateType.Idle);
    }

    void InitializeStates()
    {
        states = new Dictionary<StateType, ICharacterState>
        {
            { StateType.Idle, new IdleState() },
            { StateType.Moving, new MoveState() },
            { StateType.Jumping, new JumpState() },
            { StateType.Charging, new ChargingState() },
            { StateType.Throwing, new ThrowState() },
            { StateType.Dodging, new DodgeState() },
            { StateType.Hit, new HitState() },
            { StateType.Ultimate, new UltimateState() }
        };
    }

    public void ChangeState(StateType newStateType)
    {
        currentState?.ExitState(character);
        currentState = states[newStateType];
        currentState?.EnterState(character);
    }

    void Update()
    {
        currentState?.UpdateState(character);
    }
}