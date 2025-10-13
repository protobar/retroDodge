using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using RetroDodge.AI;

namespace RetroDodge
{
	public class AIControllerBrain : MonoBehaviour
	{
		[SerializeField] private PlayerCharacter controlledCharacter;
		[SerializeField] private PlayerCharacter targetOpponent;
		[SerializeField] private AIDifficulty difficulty = AIDifficulty.Normal;
		[SerializeField] private bool debugDraw;
	[SerializeField] private bool debugMode = false;

		private AIDifficultyParams parms;
		private PlayerInputHandler input;
		private BallController ball;
		private float lastThrowDecisionTime;
		private float thinkAccumulator;

	private enum AIState { SeekBall, ApproachAndPickup, EngageWithBall, Evade }
	private AIState state = AIState.SeekBall;
		
		// Echo-specific teleport strategies
		private bool isEcho = false;
		private float lastTeleportTime;
		private float teleportCooldown = 3f;
		private float lastDodgeTime;
		private float dodgeCooldown = 1.2f;
        private float lastStateSwitch;
		
		// AI Balancing - Human-like constraints
		private float lastJumpTime;
		private float jumpCooldown = 0.8f; // Prevent jump spam
		private float lastActionTime;
		private float actionBudget = 0f; // Action economy system
		private int consecutiveJumps = 0;
		private float lastMovementTime;
		private Vector3 lastKnownTargetPos;
		
		// Boundary awareness system (simplified)
		private ArenaMovementRestrictor movementRestrictor;
		private bool isNearCenterLine = false;
		
		// Advanced AI tactics
		private float lastBallPositionCheck = 0f;
		private Vector3 lastBallPosition;
		private bool ballMovingTowardUs = false;
		private float predictionAccuracy = 0.8f;

		void Awake()
		{
			input = GetComponent<PlayerInputHandler>();
			controlledCharacter = GetComponent<PlayerCharacter>();
		}

		void Start()
		{
			parms = AIDifficultyPresets.Get(difficulty);
			FindContext();
			
			// Get movement restrictor for boundary awareness
			movementRestrictor = GetComponent<ArenaMovementRestrictor>();
			if (movementRestrictor == null)
			{
				Debug.LogWarning($"[AI] {gameObject.name} missing ArenaMovementRestrictor component!");
			}
			
			// Ensure local control in OfflineMode
			var ih = GetComponent<PlayerInputHandler>();
			if (ih != null) ih.isPUN2Enabled = false;
			// Disable human inputs for AI
			ih?.ConfigureForAI();
			
			// Check if this is Echo character
			var characterData = controlledCharacter?.GetCharacterData();
			if (characterData != null && characterData.characterName.ToLower().Contains("echo"))
			{
				isEcho = true;
				teleportCooldown = 2.5f; // Shorter cooldown for Echo
			}
		}

		void FindContext()
		{
			if (targetOpponent == null)
			{
				var all = FindObjectsOfType<PlayerCharacter>();
				foreach (var pc in all)
				{
					if (pc != controlledCharacter)
					{
						targetOpponent = pc;
						break;
					}
				}
			}
			ball = BallManager.Instance != null ? BallManager.Instance.GetCurrentBall() : FindObjectOfType<BallController>();
		}

		void Update()
		{
			if (input == null || controlledCharacter == null) return;

			// Update action budget (restore over time, like stamina)
			actionBudget = Mathf.Min(actionBudget + Time.deltaTime * 2f, 3f);

			thinkAccumulator += Time.deltaTime;
			if (thinkAccumulator >= 0.1f)
			{
				Think();
				thinkAccumulator = 0f;
			}

			// Apply frame to input via external override
			var frame = BuildInputFrame();
			input.ApplyExternalInput(frame);
		}

		void Think()
		{
			FindContext();
			UpdateTargetTracking();
			CheckBoundaryStatus();
			UpdateAdvancedTactics();

			bool hasBall = controlledCharacter.HasBall();
			
			// BALANCED: Occasionally make suboptimal decisions
			bool makeMistake = Random.value < parms.mistakeChance;
			
			if (hasBall)
			{
				state = AIState.EngageWithBall;
			}
			else
			{
				// ADVANCED: Intelligent threat assessment
				bool opponentThreat = false;
				if (targetOpponent != null && targetOpponent.HasBall())
				{
					float distance = Vector3.Distance(targetOpponent.transform.position, controlledCharacter.transform.position);
					bool facingMe = Vector3.Dot(targetOpponent.GetThrowDirection(), (controlledCharacter.transform.position - targetOpponent.transform.position).normalized) > 0.3f;
					
					// ADVANCED: More intelligent threat detection based on difficulty
					float threatRange = difficulty switch
					{
						AIDifficulty.Easy => 6f,
						AIDifficulty.Normal => 8f,
						AIDifficulty.Hard => 10f,
						AIDifficulty.Nightmare => 12f,
						_ => 8f
					};
					
					opponentThreat = distance < threatRange && (facingMe || distance < threatRange * 0.5f);
					
					// ADVANCED: Nightmare AI predicts throws
					if (difficulty == AIDifficulty.Nightmare && !opponentThreat)
					{
						// Predict if opponent is about to throw
						Vector3 opponentToUs = (controlledCharacter.transform.position - targetOpponent.transform.position).normalized;
						bool opponentFacingUs = Vector3.Dot(targetOpponent.transform.right, opponentToUs) > 0.3f;
						if (opponentFacingUs && distance < 15f)
						{
							opponentThreat = true; // Pre-emptive threat detection
						}
					}
					
					// BALANCED: Sometimes fail to recognize threat (mistake)
					if (makeMistake) opponentThreat = false;
				}
				
				// ADVANCED: Enhanced ball threat detection
				bool ballThreat = ball != null && ball.GetBallState() == BallController.BallState.Thrown;
				if (ballThreat)
				{
					float ballDistance = Vector3.Distance(ball.transform.position, controlledCharacter.transform.position);
					
					// ADVANCED: Ball prediction based on difficulty
					bool ballWillHitUs = ballDistance < 4f;
					if (difficulty >= AIDifficulty.Hard && ballMovingTowardUs)
					{
						// Predict ball trajectory
						Vector3 ballToUs = (controlledCharacter.transform.position - ball.transform.position).normalized;
						Vector3 ballVelocity = ball.GetVelocity();
						if (ballVelocity.magnitude > 0.1f)
						{
							float timeToHit = ballDistance / ballVelocity.magnitude;
							Vector3 predictedPosition = ball.transform.position + ballVelocity * timeToHit;
							float distanceToPredicted = Vector3.Distance(predictedPosition, controlledCharacter.transform.position);
							ballWillHitUs = distanceToPredicted < 2f;
						}
					}
					
					if (ballWillHitUs) opponentThreat = true;
					
					// BALANCED: Sometimes fail to see incoming ball (mistake)
					if (makeMistake && Random.value < 0.5f) opponentThreat = false;
				}
				
				if (opponentThreat)
				{
					state = AIState.Evade;
				}
				else
				{
					// NATURAL: Always try to seek ball, let movement restrictions handle boundaries
					state = AIState.SeekBall;
				}
			}
            lastStateSwitch = Time.time;
		}

		PlayerInputHandler.ExternalInputFrame BuildInputFrame()
		{
			var frame = new PlayerInputHandler.ExternalInputFrame();
			frame.horizontal = 0f;

			Vector3 selfPos = controlledCharacter.transform.position;
			Vector3 targetPos = targetOpponent != null ? targetOpponent.transform.position : selfPos + Vector3.right;

			switch (state)
			{
				case AIState.SeekBall:
					if (ball != null)
					{
						// INTELLIGENT: Check if ball is on our side of the arena
						bool ballOnOurSide = IsBallOnOurSide();
						
						if (ballOnOurSide)
						{
							// ADVANCED: Intelligent movement toward ball
							Vector3 dir = (ball.transform.position - selfPos).normalized;
							
							// ADVANCED: Predict ball movement for better positioning
							if (difficulty >= AIDifficulty.Hard && ball.GetBallState() == BallController.BallState.Free)
							{
								// Predict where ball will be
								Vector3 ballVelocity = ball.GetVelocity();
								if (ballVelocity.magnitude > 0.1f)
								{
									float predictionTime = Vector3.Distance(selfPos, ball.transform.position) / GetEffectiveMoveSpeed();
									Vector3 predictedBallPos = ball.transform.position + ballVelocity * predictionTime;
									dir = (predictedBallPos - selfPos).normalized;
								}
							}
							
							// ADVANCED: More aggressive movement based on difficulty
							float movementSpeed = difficulty switch
							{
								AIDifficulty.Easy => 0.7f,
								AIDifficulty.Normal => 1.0f,
								AIDifficulty.Hard => 1.2f,
								AIDifficulty.Nightmare => 1.5f,
								_ => 1.0f
							};
							
							frame.horizontal = Mathf.Abs(dir.x) > 0.1f ? Mathf.Sign(dir.x) * movementSpeed : 0f;
							
							// BALANCED: Much more restrictive jumping
							bool ballHighAbove = ball.transform.position.y - selfPos.y > 2.0f;
							bool canJump = Time.time - lastJumpTime > jumpCooldown && actionBudget > 1f;
							bool shouldJump = ballHighAbove && Random.value < (parms.jumpProbability * 0.3f); // Reduced by 70%
							
							if (canJump && shouldJump && consecutiveJumps < 1)
							{
								frame.jumpPressed = true;
								ConsumeAction("jump");
							}
							
							// NATURAL: Always try to pickup when close enough
							bool ballIsFree = ball.GetBallState() == BallController.BallState.Free;
							if (ballIsFree && Vector3.Distance(selfPos, ball.transform.position) < 1.25f)
							{
								frame.pickupPressed = true;
								if (debugMode) Debug.Log($"[AI] {controlledCharacter.name} attempting to pickup ball");
							}
							
							// IMPROVED: Try to catch incoming balls even when seeking
							if (ball.GetBallState() == BallController.BallState.Thrown)
							{
								var catcher = controlledCharacter.GetComponent<CatchSystem>();
								if (catcher != null && catcher.IsBallInRange())
								{
									frame.catchPressed = true;
									if (debugMode) Debug.Log($"[AI] {controlledCharacter.name} attempting to catch thrown ball while seeking");
								}
							}
						}
						else
						{
							// INTELLIGENT: Don't move if ball is on opponent's side
							frame.horizontal = 0f;
							if (debugMode) Debug.Log($"[AI] {controlledCharacter.name} not moving - ball on opponent's side");
						}
					}
					break;

				case AIState.Evade:
					{
						// Priority 1: Try to catch incoming thrown balls
						bool attemptingCatch = false;
						if (ball != null && ball.GetBallState() == BallController.BallState.Thrown)
						{
							var catcher = controlledCharacter.GetComponent<CatchSystem>();
							if (catcher != null && catcher.IsBallInRange())
							{
								frame.catchPressed = true;
								attemptingCatch = true;
								// Reduce movement when trying to catch
								frame.horizontal = 0f;
								if (debugMode) Debug.Log($"[AI] {controlledCharacter.name} attempting to catch thrown ball");
							}
						}
						
						// Priority 2: Evasive movement if not catching
						if (!attemptingCatch)
						{
							Vector3 dir = Mathf.Sign(selfPos.x - targetPos.x) * Vector3.right;
							frame.horizontal = dir.x * 0.8f; // Slightly slower movement
							
							// BALANCED: Very limited evasive jumping
							bool incomingBallThreat = ball != null && ball.GetBallState() == BallController.BallState.Thrown && 
								Vector3.Distance(ball.transform.position, selfPos) < 4f;
							bool canJump = Time.time - lastJumpTime > jumpCooldown && actionBudget > 1.5f;
							bool shouldJump = incomingBallThreat && Random.value < (parms.jumpProbability * 0.4f); // Much less jumping
							
							if (canJump && shouldJump && consecutiveJumps < 1)
							{
								frame.jumpPressed = true;
								ConsumeAction("jump");
							}
								
							// Dash away from danger (also limited)
							if (Time.time - lastDodgeTime > dodgeCooldown && Random.value < parms.dodgeProbability && actionBudget > 1f)
							{
								frame.dashPressed = true;
								lastDodgeTime = Time.time;
								ConsumeAction("dash");
							}
							
							// IMPROVED: Crouch to avoid low throws
							bool shouldCrouch = incomingBallThreat && Random.value < (parms.dodgeProbability * 0.8f);
							if (shouldCrouch && actionBudget > 0.5f)
							{
								frame.duckHeld = true;
								ConsumeAction("duck");
								if (debugMode) Debug.Log($"[AI] {controlledCharacter.name} crouching to avoid ball");
							}
						}
					}
					break;

				case AIState.EngageWithBall:
					{
						// ADVANCED: Intelligent positioning and throwing
						float side = Mathf.Sign(targetPos.x - selfPos.x);
						
						// ADVANCED: More aggressive positioning based on difficulty
						float positioningAggression = difficulty switch
						{
							AIDifficulty.Easy => 0.3f,
							AIDifficulty.Normal => 0.5f,
							AIDifficulty.Hard => 0.8f,
							AIDifficulty.Nightmare => 1.2f,
							_ => 0.5f
						};
						
						frame.horizontal = side * positioningAggression;

						// ADVANCED: Faster throw decisions based on difficulty
						float throwCooldown = parms.throwDecisionCooldown;
						if (difficulty == AIDifficulty.Nightmare)
						{
							throwCooldown *= 0.5f; // Nightmare AI throws twice as fast
						}
						
						if (Time.time - lastThrowDecisionTime > throwCooldown)
						{
							lastThrowDecisionTime = Time.time;
							
							// ADVANCED: Intelligent throw timing
							bool shouldThrow = true;
							
							// Nightmare AI has perfect timing
							if (difficulty == AIDifficulty.Nightmare)
							{
								// Always throw when optimal
								shouldThrow = true;
							}
							else
							{
								// BALANCED: Sometimes hesitate or delay throws
								if (Random.value < parms.mistakeChance * 0.5f)
								{
									shouldThrow = false;
									if (debugMode) Debug.Log($"[AI] {controlledCharacter.name} hesitating on throw");
								}
							}
							
							if (shouldThrow && actionBudget > 0.5f)
							{
								frame.throwPressed = true;
								ConsumeAction("throw");
							}
						}
						// If an incoming ball is nearby and we don't have one, try catch
						if (!controlledCharacter.HasBall())
						{
							var catcher = controlledCharacter.GetComponent<CatchSystem>();
							if (catcher != null && catcher.IsBallInRange())
							{
								frame.catchPressed = true;
							}
						}
						
						// IMPROVED: Crouch while holding ball to avoid incoming throws
						bool incomingThreat = targetOpponent != null && targetOpponent.HasBall() && 
							Vector3.Distance(targetOpponent.transform.position, selfPos) < 6f;
						bool shouldCrouchWhileHolding = incomingThreat && Random.value < (parms.dodgeProbability * 0.6f);
						if (shouldCrouchWhileHolding && actionBudget > 0.5f)
						{
							frame.duckHeld = true;
							ConsumeAction("duck");
							if (debugMode) Debug.Log($"[AI] {controlledCharacter.name} crouching while holding ball");
						}
					}
					break;

				case AIState.ApproachAndPickup:
					// Not used in this simplified heuristic; covered by SeekBall
					break;
					
			}

			// Use abilities when appropriate
			if (controlledCharacter.HasBall())
			{
				// Ultimate - only when charged and holding ball (high damage potential)
				if (controlledCharacter.GetUltimateChargePercentage() >= 1f && Random.value < 0.08f)
				{
					frame.ultimatePressed = true;
				}
			}

			// Trick - use on opponent when charged (offensive/defensive utility)
			if (controlledCharacter.GetTrickChargePercentage() >= 1f && Random.value < 0.12f)
			{
				frame.trickPressed = true;
			}

			// Echo-specific teleport strategies
			if (isEcho)
			{
				if (ShouldTeleportDodge() || ShouldTeleportSteal() || ShouldTeleportAttack())
				{
					frame.treatPressed = true;
					ExecuteTeleportStrategy();
				}
			}
			else
			{
				// Regular treat usage for non-Echo characters
				if (controlledCharacter.GetTreatChargePercentage() >= 1f)
				{
					var health = controlledCharacter.GetComponent<PlayerHealth>();
					bool lowHealth = health != null && health.GetCurrentHealth() < health.GetMaxHealth() * 0.4f;
					bool underPressure = targetOpponent != null && targetOpponent.HasBall() && 
						Vector3.Distance(targetOpponent.transform.position, controlledCharacter.transform.position) < 4f;
					
					if ((lowHealth || underPressure) && Random.value < 0.15f)
					{
						frame.treatPressed = true;
					}
				}
			}

			return frame;
		}

		public void SetDifficulty(AIDifficulty newDifficulty)
		{
			difficulty = newDifficulty;
			parms = AIDifficultyPresets.Get(difficulty);
			
			if (debugMode)
			{
				Debug.Log($"[AI] {controlledCharacter.name} difficulty set to {difficulty}");
			}
		}
		
		// AI Balancing - Action Economy System
		void ConsumeAction(string actionType)
		{
			switch (actionType)
			{
				case "jump":
					lastJumpTime = Time.time;
					consecutiveJumps++;
					actionBudget = Mathf.Max(0f, actionBudget - 1.5f);
					// Reset consecutive jumps after a delay
					StartCoroutine(ResetJumpCounter());
					break;
				case "dash":
					actionBudget = Mathf.Max(0f, actionBudget - 1f);
					break;
				case "throw":
					actionBudget = Mathf.Max(0f, actionBudget - 0.5f);
					break;
				case "duck":
					actionBudget = Mathf.Max(0f, actionBudget - 0.3f);
					break;
			}
			lastActionTime = Time.time;
		}
		
		System.Collections.IEnumerator ResetJumpCounter()
		{
			yield return new WaitForSeconds(2f);
			consecutiveJumps = 0;
		}
		
		// Update target tracking for better prediction
		void UpdateTargetTracking()
		{
			if (targetOpponent != null)
			{
				lastKnownTargetPos = targetOpponent.transform.position;
				lastMovementTime = Time.time;
			}
		}
		
		// ═══════════════════════════════════════════════════════════════
		// ECHO TELEPORT STRATEGIES
		// ═══════════════════════════════════════════════════════════════
		
		bool CanTeleport()
		{
			return isEcho && 
				   Time.time - lastTeleportTime > teleportCooldown && 
				   controlledCharacter.GetTreatChargePercentage() >= 1f;
		}
		
		// Strategy 1: Teleport + Surprise Attack (when Echo has ball)
		bool ShouldTeleportAttack()
		{
			if (!CanTeleport() || !controlledCharacter.HasBall()) return false;
			
			// Use when opponent is far away for surprise attack
			if (targetOpponent != null)
			{
				float distance = Vector3.Distance(controlledCharacter.transform.position, targetOpponent.transform.position);
				return distance > 8f && Random.value < 0.25f; // 25% chance when far
			}
			return false;
		}
		
		// Strategy 2: Ball Steal Teleport (when opponent is going for ball)
		bool ShouldTeleportSteal()
		{
			if (!CanTeleport() || ball == null) return false;
			
			// Only when ball is free and opponent is closer to it
			if (ball.GetBallState() != BallController.BallState.Free) return false;
			
			if (targetOpponent != null)
			{
				float opponentToBall = Vector3.Distance(targetOpponent.transform.position, ball.transform.position);
				float ourToBall = Vector3.Distance(controlledCharacter.transform.position, ball.transform.position);
				
				// Teleport if opponent is closer and moving toward ball
				return opponentToBall < ourToBall && opponentToBall < 6f && Random.value < 0.35f;
			}
			return false;
		}
		
		// Strategy 3: Dodge Teleport (when ball is thrown at Echo)
		bool ShouldTeleportDodge()
		{
			if (!CanTeleport() || ball == null) return false;
			
			// Only when ball is thrown toward us
			if (ball.GetBallState() != BallController.BallState.Thrown) return false;
			
			// Check if ball is coming toward us
			Vector3 ballPos = ball.transform.position;
			Vector3 ballVel = ball.GetVelocity();
			Vector3 ourPos = controlledCharacter.transform.position;
			
			// Simple prediction: will ball hit us?
			float timeToReach = Vector3.Distance(ballPos, ourPos) / ballVel.magnitude;
			Vector3 predictedBallPos = ballPos + ballVel * timeToReach;
			float distanceToUs = Vector3.Distance(predictedBallPos, ourPos);
			
			// Teleport if ball is close and coming toward us
			return distanceToUs < 3f && Vector3.Dot(ballVel.normalized, (ourPos - ballPos).normalized) > 0.5f;
		}
		
		void ExecuteTeleportStrategy()
		{
			lastTeleportTime = Time.time;
			controlledCharacter.ActivateTreat(); // Trigger teleport
			
			if (debugMode)
			{
				Debug.Log($"[ECHO AI] Executed teleport strategy");
			}
		}
		
		// ═══════════════════════════════════════════════════════════════
		// ADVANCED AI TACTICS
		// ═══════════════════════════════════════════════════════════════
		
		void UpdateAdvancedTactics()
		{
			if (ball == null) return;
			
			// Track ball movement for prediction
			if (Time.time - lastBallPositionCheck > 0.1f)
			{
				Vector3 currentBallPos = ball.transform.position;
				if (lastBallPositionCheck > 0f)
				{
					Vector3 ballVelocity = (currentBallPos - lastBallPosition) / (Time.time - lastBallPositionCheck);
					ballMovingTowardUs = Vector3.Dot(ballVelocity.normalized, 
						(controlledCharacter.transform.position - currentBallPos).normalized) > 0.3f;
				}
				lastBallPosition = currentBallPos;
				lastBallPositionCheck = Time.time;
			}
			
			// Adjust prediction accuracy based on difficulty
			predictionAccuracy = difficulty switch
			{
				AIDifficulty.Easy => 0.4f,
				AIDifficulty.Normal => 0.6f,
				AIDifficulty.Hard => 0.8f,
				AIDifficulty.Nightmare => 0.95f,
				_ => 0.6f
			};
		}
		
		// ═══════════════════════════════════════════════════════════════
		// ARENA BOUNDARY AWARENESS (CORRECT DIMENSIONS)
		// ═══════════════════════════════════════════════════════════════
		
		void CheckBoundaryStatus()
		{
			if (movementRestrictor == null) return;
			
			// Check if we're near the center line (X = 0)
			float currentX = controlledCharacter.transform.position.x;
			float distanceToCenter = Mathf.Abs(currentX);
			isNearCenterLine = distanceToCenter < 2f; // Within 2 units of center line
			
			if (debugMode && isNearCenterLine)
			{
				Debug.Log($"[AI] {controlledCharacter.name} near center line - PosX: {currentX}, DistanceToCenter: {distanceToCenter}");
			}
		}
		
		bool IsBallOnOurSide()
		{
			if (ball == null || movementRestrictor == null) return false;
			
			// Get player bounds from restrictor
			movementRestrictor.GetPlayerBounds(out float minX, out float maxX);
			float ballX = ball.transform.position.x;
			
			// Check if ball is within our allowed bounds
			bool ballOnOurSide = ballX >= minX && ballX <= maxX;
			
			if (debugMode)
			{
				Debug.Log($"[AI] Ball position check - BallX: {ballX}, OurBounds: [{minX}, {maxX}], OnOurSide: {ballOnOurSide}");
			}
			
			return ballOnOurSide;
		}
		
		// Helper method to get effective move speed
		float GetEffectiveMoveSpeed()
		{
			if (controlledCharacter?.GetCharacterData() != null)
			{
				return controlledCharacter.GetCharacterData().moveSpeed;
			}
			return 5f; // Default speed
		}
		
	}
}


