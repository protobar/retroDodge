using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== CHARACTER STATE INTERFACE ====================
public interface ICharacterState
{
    void EnterState(CharacterBase character);
    void UpdateState(CharacterBase character);
    void ExitState(CharacterBase character);
}