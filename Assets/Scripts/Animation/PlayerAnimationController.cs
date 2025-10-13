using UnityEngine;

namespace RetroDodgeRumble.Animation
{
    /// <summary>
    /// Simple animation bridge between PlayerCharacter and Animator
    /// No automatic detection - PlayerCharacter calls methods directly
    /// </summary>
    public class PlayerAnimationController : MonoBehaviour
    {
        [Header("Animation References")]
        [SerializeField] private Animator animator;

        #region Animator Parameter Hashes

        // Movement Parameters
        private static readonly int SPEED_HASH = Animator.StringToHash("Speed");

        // Bool Parameters
        private static readonly int IS_GROUNDED_HASH = Animator.StringToHash("IsGrounded");
        private static readonly int IS_DUCKING_HASH = Animator.StringToHash("IsDucking");
        private static readonly int HAS_BALL_HASH = Animator.StringToHash("HasBall");
        private static readonly int IS_DASHING_HASH = Animator.StringToHash("IsDashing");

        // Trigger Parameters
        private static readonly int JUMP_HASH = Animator.StringToHash("Jump");
        private static readonly int DOUBLE_JUMP_HASH = Animator.StringToHash("DoubleJump");
        private static readonly int DASH_HASH = Animator.StringToHash("Dash");
        private static readonly int THROW_HASH = Animator.StringToHash("Throw");
        private static readonly int CATCH_HASH = Animator.StringToHash("Catch");
        private static readonly int PICKUP_HASH = Animator.StringToHash("Pickup");
        private static readonly int ULTIMATE_HASH = Animator.StringToHash("Ultimate");
        private static readonly int TRICK_HASH = Animator.StringToHash("Trick");
        private static readonly int TREAT_HASH = Animator.StringToHash("Treat");
        private static readonly int HIT_HASH = Animator.StringToHash("Hit");
        private static readonly int DEATH_HASH = Animator.StringToHash("Death");
        private static readonly int VICTORY_HASH = Animator.StringToHash("Victory");
        private static readonly int DEFEAT_HASH = Animator.StringToHash("Defeat");

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Get Animator component
            if (!animator)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (!animator)
            {
                Debug.LogWarning($"{gameObject.name}: No Animator found!", this);
                enabled = false;
            }
        }

        #endregion

        #region Movement API

        /// <summary>
        /// Set movement speed (call from PlayerCharacter)
        /// </summary>
        public void SetSpeed(float speed)
        {
            if (!animator) return;
            animator.SetFloat(SPEED_HASH, speed);
        }

        

        #endregion

        #region State Setters

        /// <summary>
        /// Set grounded state
        /// </summary>
        public void SetGrounded(bool grounded)
        {
            if (!animator) return;
            animator.SetBool(IS_GROUNDED_HASH, grounded);
        }

        /// <summary>
        /// Set ducking state
        /// </summary>
        public void SetDucking(bool ducking)
        {
            if (!animator) return;
            animator.SetBool(IS_DUCKING_HASH, ducking);
        }

        /// <summary>
        /// Set ball possession state
        /// </summary>
        public void SetHasBall(bool hasBall)
        {
            if (!animator) return;
            animator.SetBool(HAS_BALL_HASH, hasBall);
        }

        /// <summary>
        /// Set dashing state
        /// </summary>
        public void SetDashing(bool dashing)
        {
            if (!animator) return;
            animator.SetBool(IS_DASHING_HASH, dashing);
        }

        #endregion

        #region Trigger Methods

        /// <summary>
        /// Trigger jump animation
        /// </summary>
        public void TriggerJump()
        {
            if (!animator) return;
            animator.SetTrigger(JUMP_HASH);
        }

        /// <summary>
        /// Trigger double jump animation
        /// </summary>
        public void TriggerDoubleJump()
        {
            if (!animator) return;
            animator.SetTrigger(DOUBLE_JUMP_HASH);
        }

        /// <summary>
        /// Trigger hit/damaged animation when player takes damage
        /// </summary>
        public void TriggerHit()
        {
            if (!animator) return;
            animator.SetTrigger(HIT_HASH);
        }

        /// <summary>
        /// Trigger death animation when player dies
        /// </summary>
        public void TriggerDeath()
        {
            if (!animator) return;
            animator.SetTrigger(DEATH_HASH);
        }

        /// <summary>
        /// Trigger victory animation when player wins
        /// </summary>
        public void TriggerVictory()
        {
            if (!animator) return;
            animator.SetTrigger(VICTORY_HASH);
        }

        /// <summary>
        /// Trigger defeat animation when player loses
        /// </summary>
        public void TriggerDefeat()
        {
            if (!animator) return;
            animator.SetTrigger(DEFEAT_HASH);
        }

        /// <summary>
        /// Reset all animations to idle state
        /// </summary>
        public void ResetToIdle()
        {
            if (!animator) return;
            
            // Reset all bool parameters
            animator.SetBool(IS_GROUNDED_HASH, true);
            animator.SetBool(IS_DUCKING_HASH, false);
            animator.SetBool(HAS_BALL_HASH, false);
            animator.SetBool(IS_DASHING_HASH, false);
            
            // Reset speed to 0
            animator.SetFloat(SPEED_HASH, 0f);
        }


        /// <summary>
        /// Trigger dash animation
        /// </summary>
        public void TriggerDash()
        {
            if (!animator) return;
            animator.SetTrigger(DASH_HASH);
        }

        /// <summary>
        /// Trigger ball throw animation
        /// </summary>
        public void TriggerThrow()
        {
            if (!animator) return;
            animator.SetTrigger(THROW_HASH);
        }

        /// <summary>
        /// Trigger ball catch animation
        /// </summary>
        public void TriggerCatch()
        {
            if (!animator) return;
            animator.SetTrigger(CATCH_HASH);
        }

        /// <summary>
        /// Trigger ball pickup animation
        /// </summary>
        public void TriggerPickup()
        {
            if (!animator) return;
            animator.SetTrigger(PICKUP_HASH);
        }

        /// <summary>
        /// Trigger ultimate ability animation
        /// </summary>
        public void TriggerUltimate()
        {
            if (!animator) return;
            animator.SetTrigger(ULTIMATE_HASH);
        }

        /// <summary>
        /// Trigger trick ability animation
        /// </summary>
        public void TriggerTrick()
        {
            if (!animator) return;
            animator.SetTrigger(TRICK_HASH);
        }

        /// <summary>
        /// Trigger treat ability animation
        /// </summary>
        public void TriggerTreat()
        {
            if (!animator) return;
            animator.SetTrigger(TREAT_HASH);
        }

        #endregion
    }
}
