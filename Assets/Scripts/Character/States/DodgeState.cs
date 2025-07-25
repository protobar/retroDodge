using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== DODGE STATE ====================
public class DodgeState : ICharacterState
{
    private float dodgeDuration = 0.3f;
    private float dodgeStartTime;
    private Vector3 dodgeDirection;

    public void EnterState(CharacterBase character)
    {
        dodgeStartTime = Time.time;

        // Determine dodge direction based on input
        float horizontal = InputManager.GetHorizontal();
        dodgeDirection = horizontal != 0 ? (horizontal > 0 ? Vector3.right : Vector3.left) : -character.transform.forward;

        // Make character invulnerable during dodge
        character.GetComponent<CharacterBase>().canMove = false;

        // Play dodge animation
        Animator animator = character.GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetTrigger("Dodge");
        }

        // Add visual effect
        GameObject dodgeEffect = character.transform.Find("DodgeEffect")?.gameObject;
        if (dodgeEffect != null)
        {
            dodgeEffect.SetActive(true);
        }

        AudioManager.Instance?.PlaySound("Dodge");
    }

    public void UpdateState(CharacterBase character)
    {
        float elapsedTime = Time.time - dodgeStartTime;

        // Move character during dodge
        if (elapsedTime < dodgeDuration)
        {
            float dodgeSpeed = 15f;
            Vector3 dodgeMovement = dodgeDirection * dodgeSpeed * Time.deltaTime;
            character.GetComponent<CharacterController>().Move(dodgeMovement);
        }
        else
        {
            // Dodge complete
            float horizontal = InputManager.GetHorizontal();
            StateType nextState = Mathf.Abs(horizontal) > 0.1f ? StateType.Moving : StateType.Idle;
            character.GetComponent<StateMachine>().ChangeState(nextState);
        }
    }

    public void ExitState(CharacterBase character)
    {
        // Re-enable movement
        character.GetComponent<CharacterBase>().canMove = true;

        // Disable dodge effect
        GameObject dodgeEffect = character.transform.Find("DodgeEffect")?.gameObject;
        if (dodgeEffect != null)
        {
            dodgeEffect.SetActive(false);
        }
    }
}