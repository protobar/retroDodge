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
		
		// Character-specific strategy flags
		private bool isEcho = false;
		private bool isGrudge = false;
		private bool isNova = false;
		
		// Echo-specific teleport strategies
		private float lastTeleportTime;
		private float teleportCooldown = 3f;
		
		// Grudge-specific aggressive strategies
		private float lastShieldTime;
		private float shieldCooldown = 4f;
		private float aggression = 1.0f; // Grudge is naturally more aggressive
		
		// Nova-specific positioning strategies
		private float lastSpeedBoostTime;
		private float speedBoostCooldown = 5f;
		private bool isPositioningForMultiBall = false;
		
		// Shared
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
			
			// Check character type for specialized strategies
			var characterData = controlledCharacter?.GetCharacterData();
			if (characterData != null)
			{
				string charName = characterData.characterName.ToLower();
				
				if (charName.Contains("echo"))
				{
					isEcho = true;
					teleportCooldown = 2.5f; // Shorter cooldown for Echo
					if (debugMode) Debug.Log($"[AI] {gameObject.name} identified as ECHO - Teleport strategies active");
				}
				else if (charName.Contains("grudge"))
				{
					isGrudge = true;
					aggression = 1.5f; // Grudge is 50% more aggressive
					if (debugMode) Debug.Log($"[AI] {gameObject.name} identified as GRUDGE - Aggressive tank strategies active");
				}
				else if (charName.Contains("nova"))
				{
					isNova = true;
					if (debugMode) Debug.Log($"[AI] {gameObject.name} identified as NOVA - Multi-ball specialist strategies active");
				}
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
							
							// IMPROVED: Intelligent crouch to avoid throws
							bool shouldCrouch = false;
							if (incomingBallThreat && actionBudget > 0.3f)
							{
								// Check ball height to decide if ducking will help
								float ballHeight = ball.transform.position.y - selfPos.y;
								bool ballIsHighEnough = ballHeight > 0.5f && ballHeight < 2.5f; // Between knee and head height
								
								// Higher chance to duck if ball is at right height
								float duckChance = ballIsHighEnough ? (parms.dodgeProbability * 1.2f) : (parms.dodgeProbability * 0.4f);
								
								shouldCrouch = Random.value < duckChance;
								
								if (shouldCrouch)
								{
									frame.duckHeld = true;
									ConsumeAction("duck");
									if (debugMode) Debug.Log($"[AI] {controlledCharacter.name} crouching to avoid ball (height: {ballHeight:F2})");
								}
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
						
						// IMPROVED: Intelligent crouch while holding ball to avoid incoming throws
						bool incomingThreat = targetOpponent != null && targetOpponent.HasBall();
						if (incomingThreat && actionBudget > 0.3f)
						{
							float threatDistance = Vector3.Distance(targetOpponent.transform.position, selfPos);
							bool opponentFacingUs = Vector3.Dot(
								targetOpponent.transform.right, 
								(selfPos - targetOpponent.transform.position).normalized
							) > 0.5f;
							
							// Duck if opponent is close and facing us (likely to throw)
							bool shouldCrouchWhileHolding = threatDistance < 7f && opponentFacingUs && 
								Random.value < (parms.dodgeProbability * 0.7f);
								
							// Grudge is more likely to duck while holding (tank playstyle)
							if (isGrudge) shouldCrouchWhileHolding = threatDistance < 9f && Random.value < 0.85f;
							
							if (shouldCrouchWhileHolding)
							{
								frame.duckHeld = true;
								ConsumeAction("duck");
								if (debugMode) Debug.Log($"[AI] {controlledCharacter.name} crouching while holding ball (threat distance: {threatDistance:F1})");
							}
						}
					}
					break;

				case AIState.ApproachAndPickup:
					// Not used in this simplified heuristic; covered by SeekBall
					break;
					
			}

			// Use abilities when appropriate
			// CRITICAL: Only use ultimate when CONFIRMED to have ball
			bool confirmedHasBall = controlledCharacter.HasBall();
			if (confirmedHasBall)
			{
				// Double-check ball state before ultimate (prevent timing issues)
				var currentBall = BallManager.Instance?.GetCurrentBall();
				bool ballActuallyHeld = currentBall != null && currentBall.GetHolder() == controlledCharacter;
				
				if (ballActuallyHeld)
				{
					// Ultimate - only when charged and holding ball (high damage potential)
					// NOTE: AI just presses ultimate once. The ultimate system will:
					//   1. Play full activation animation (2.3s)
					//   2. Wait for timeout (2s) 
					//   3. Auto-throw (AI doesn't need to release Q)
					if (controlledCharacter.GetUltimateChargePercentage() >= 1f && Random.value < 0.08f)
					{
						frame.ultimatePressed = true;
						if (debugMode) Debug.Log($"[AI] {controlledCharacter.name} activating ultimate (ball confirmed held)");
					}
				}
			}

			// CRITICAL: Can't use abilities during stun or fallback
			bool canUseAbilities = !controlledCharacter.IsStunned() && !controlledCharacter.IsFallen();
			
			if (canUseAbilities)
			{
				// Trick - use on opponent when charged (offensive/defensive utility)
				if (controlledCharacter.GetTrickChargePercentage() >= 1f && Random.value < 0.12f)
				{
					frame.trickPressed = true;
				}

				// ═══════════════════════════════════════════════════════════════
				// CHARACTER-SPECIFIC ABILITY STRATEGIES
				// ═══════════════════════════════════════════════════════════════
				
				// Echo-specific teleport strategies
				if (isEcho)
				{
					if (ShouldTeleportDodge() || ShouldTeleportSteal() || ShouldTeleportAttack())
					{
						frame.treatPressed = true;
						ExecuteTeleportStrategy();
					}
				}
				// Grudge-specific shield strategies
				else if (isGrudge)
				{
					// Shield: Use defensively when under pressure or before aggressive attack
					if (ShouldUseShield())
					{
						frame.treatPressed = true;
						ExecuteShieldStrategy();
					}
				}
				// Nova-specific speed boost strategies
				else if (isNova)
				{
					// Speed Boost: Use for positioning, ball steal, or escape
					if (ShouldUseSpeedBoost())
					{
						frame.treatPressed = true;
						ExecuteSpeedBoostStrategy();
					}
				}
				else
				{
					// Fallback treat usage for unknown characters
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
			} // End canUseAbilities check

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
		// GRUDGE SHIELD STRATEGIES (TANK/AGGRESSIVE PLAYSTYLE)
		// ═══════════════════════════════════════════════════════════════
		
		bool CanUseShield()
		{
			return isGrudge && 
				   Time.time - lastShieldTime > shieldCooldown && 
				   controlledCharacter.GetTreatChargePercentage() >= 1f;
		}
		
		// Scenario 1: Defensive Shield - Under heavy pressure
		bool ShouldUseShieldDefensive()
		{
			if (!CanUseShield()) return false;
			
			// Shield when opponent has ball and is close (imminent throw threat)
			if (targetOpponent != null && targetOpponent.HasBall())
			{
				float distance = Vector3.Distance(controlledCharacter.transform.position, targetOpponent.transform.position);
				bool opponentFacingUs = Vector3.Dot(
					targetOpponent.transform.right,
					(controlledCharacter.transform.position - targetOpponent.transform.position).normalized
				) > 0.6f;
				
				// Activate shield if opponent is close and facing us
				if (distance < 8f && opponentFacingUs && Random.value < 0.4f)
				{
					if (debugMode) Debug.Log($"[GRUDGE AI] Defensive shield - opponent threat at {distance:F1}m");
					return true;
				}
			}
			
			// Shield when ball is thrown at us
			if (ball != null && ball.GetBallState() == BallController.BallState.Thrown)
			{
				Vector3 ballPos = ball.transform.position;
				Vector3 ballVel = ball.GetVelocity();
				Vector3 ourPos = controlledCharacter.transform.position;
				
				// Predict if ball will hit us
				float ballDistance = Vector3.Distance(ballPos, ourPos);
				if (ballDistance < 5f && Vector3.Dot(ballVel.normalized, (ourPos - ballPos).normalized) > 0.5f)
				{
					if (debugMode) Debug.Log($"[GRUDGE AI] Emergency shield - incoming ball at {ballDistance:F1}m");
					return Random.value < 0.5f; // 50% chance to shield instead of dodge
				}
			}
			
			return false;
		}
		
		// Scenario 2: Aggressive Shield - Tank push with ball
		bool ShouldUseShieldAggressive()
		{
			if (!CanUseShield() || !controlledCharacter.HasBall()) return false;
			
			// Shield before aggressive ultimate or when closing distance
			if (targetOpponent != null)
			{
				float distance = Vector3.Distance(controlledCharacter.transform.position, targetOpponent.transform.position);
				
				// Shield when moving toward opponent with ball (tank push)
				bool isMovingToward = Vector3.Dot(
					(targetOpponent.transform.position - controlledCharacter.transform.position).normalized,
					controlledCharacter.transform.right
				) > 0.5f;
				
				// Ultimate is ready - shield and push for guaranteed hit
				bool ultimateReady = controlledCharacter.GetUltimateChargePercentage() >= 1f;
				if (ultimateReady && distance < 12f && Random.value < 0.6f)
				{
					if (debugMode) Debug.Log($"[GRUDGE AI] Aggressive shield - ultimate tank push");
					return true;
				}
				
				// Shield when at medium range for safe positioning
				if (distance > 6f && distance < 10f && isMovingToward && Random.value < 0.25f)
				{
					if (debugMode) Debug.Log($"[GRUDGE AI] Tank shield - safe positioning");
					return true;
				}
			}
			
			return false;
		}
		
		// Scenario 3: Low Health Shield - Survival
		bool ShouldUseShieldSurvival()
		{
			if (!CanUseShield()) return false;
			
			var health = controlledCharacter.GetComponent<PlayerHealth>();
			if (health != null)
			{
				float healthPercent = (float)health.GetCurrentHealth() / health.GetMaxHealth();
				
				// Shield when low on health (below 40%)
				if (healthPercent < 0.4f && Random.value < 0.7f)
				{
					if (debugMode) Debug.Log($"[GRUDGE AI] Survival shield - low health ({healthPercent * 100:F0}%)");
					return true;
				}
			}
			
			return false;
		}
		
		bool ShouldUseShield()
		{
			return ShouldUseShieldDefensive() || ShouldUseShieldAggressive() || ShouldUseShieldSurvival();
		}
		
		void ExecuteShieldStrategy()
		{
			lastShieldTime = Time.time;
			controlledCharacter.ActivateTreat(); // Trigger shield
			
			if (debugMode)
			{
				Debug.Log($"[GRUDGE AI] Executed shield strategy");
			}
		}
		
		// ═══════════════════════════════════════════════════════════════
		// NOVA SPEED BOOST STRATEGIES (MULTI-BALL SPECIALIST)
		// ═══════════════════════════════════════════════════════════════
		
		bool CanUseSpeedBoost()
		{
			return isNova && 
				   Time.time - lastSpeedBoostTime > speedBoostCooldown && 
				   controlledCharacter.GetTreatChargePercentage() >= 1f;
		}
		
		// Scenario 1: Speed Boost for Ball Steal
		bool ShouldSpeedBoostSteal()
		{
			if (!CanUseSpeedBoost() || ball == null) return false;
			
			// Use speed boost when ball is free and we can steal it
			if (ball.GetBallState() == BallController.BallState.Free)
			{
				float ourDistance = Vector3.Distance(controlledCharacter.transform.position, ball.transform.position);
				
				if (targetOpponent != null)
				{
					float opponentDistance = Vector3.Distance(targetOpponent.transform.position, ball.transform.position);
					
					// Boost if opponent is closer and racing for ball
					if (opponentDistance < ourDistance && ourDistance < 10f && Random.value < 0.45f)
					{
						if (debugMode) Debug.Log($"[NOVA AI] Speed boost - ball steal race");
						return true;
					}
				}
				
				// Boost if ball is far but gettable
				if (ourDistance > 6f && ourDistance < 12f && Random.value < 0.3f)
				{
					if (debugMode) Debug.Log($"[NOVA AI] Speed boost - closing distance to ball");
					return true;
				}
			}
			
			return false;
		}
		
		// Scenario 2: Speed Boost for Ultimate Positioning
		bool ShouldSpeedBoostPosition()
		{
			if (!CanUseSpeedBoost() || !controlledCharacter.HasBall()) return false;
			
			// Use speed boost when ultimate is ready to get perfect positioning
			bool ultimateReady = controlledCharacter.GetUltimateChargePercentage() >= 1f;
			if (ultimateReady && targetOpponent != null)
			{
				float distance = Vector3.Distance(controlledCharacter.transform.position, targetOpponent.transform.position);
				
				// Too far - boost to get into optimal multi-ball range (8-12 units)
				if (distance > 12f && Random.value < 0.5f)
				{
					isPositioningForMultiBall = true;
					if (debugMode) Debug.Log($"[NOVA AI] Speed boost - multi-ball positioning");
					return true;
				}
				
				// Good range - prepare for multi-ball ult
				if (distance > 8f && distance < 14f && Random.value < 0.35f)
				{
					isPositioningForMultiBall = true;
					if (debugMode) Debug.Log($"[NOVA AI] Speed boost - optimal multi-ball setup");
					return true;
				}
			}
			
			return false;
		}
		
		// Scenario 3: Speed Boost for Escape/Evasion
		bool ShouldSpeedBoostEscape()
		{
			if (!CanUseSpeedBoost()) return false;
			
			// Use speed boost to escape when opponent has ball and is close
			if (targetOpponent != null && targetOpponent.HasBall())
			{
				float distance = Vector3.Distance(controlledCharacter.transform.position, targetOpponent.transform.position);
				
				// Opponent too close with ball - speed boost to create distance
				if (distance < 6f && Random.value < 0.4f)
				{
					if (debugMode) Debug.Log($"[NOVA AI] Speed boost - escape from threat");
					return true;
				}
			}
			
			// Escape when ball is thrown at us (alternative to dodging)
			if (ball != null && ball.GetBallState() == BallController.BallState.Thrown)
			{
				Vector3 ballPos = ball.transform.position;
				Vector3 ballVel = ball.GetVelocity();
				Vector3 ourPos = controlledCharacter.transform.position;
				
				float ballDistance = Vector3.Distance(ballPos, ourPos);
				bool ballComingToUs = Vector3.Dot(ballVel.normalized, (ourPos - ballPos).normalized) > 0.5f;
				
				if (ballDistance < 6f && ballComingToUs && Random.value < 0.3f)
				{
					if (debugMode) Debug.Log($"[NOVA AI] Speed boost - evade incoming ball");
					return true;
				}
			}
			
			return false;
		}
		
		bool ShouldUseSpeedBoost()
		{
			return ShouldSpeedBoostSteal() || ShouldSpeedBoostPosition() || ShouldSpeedBoostEscape();
		}
		
		void ExecuteSpeedBoostStrategy()
		{
			lastSpeedBoostTime = Time.time;
			controlledCharacter.ActivateTreat(); // Trigger speed boost
			
			if (debugMode)
			{
				Debug.Log($"[NOVA AI] Executed speed boost strategy");
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


