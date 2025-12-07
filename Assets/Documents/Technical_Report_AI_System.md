# Technical Report: AI System Implementation
## Retro Dodge Rumble - Final Year Project

---

## Executive Summary

The AI system in Retro Dodge Rumble implements a sophisticated state-based artificial intelligence controller that provides challenging and human-like opponents for single-player practice modes. The system utilizes a modular architecture with configurable difficulty parameters, behavioral constraints, and character-specific adaptations to create engaging AI opponents that scale appropriately with player skill levels.

---

## 1. System Architecture Overview

### 1.1 Core Components

The AI system consists of three primary components:

1. **AIControllerBrain** - Main AI decision-making engine
2. **AIDifficulty** - Configuration system for difficulty scaling
3. **AIDifficultyPresets** - Predefined difficulty parameters

### 1.2 Design Philosophy

The AI system is designed with the following principles:
- **Human-like Behavior**: AI makes mistakes and has reaction delays
- **Scalable Difficulty**: Four distinct difficulty levels (Easy, Normal, Hard & Nightmare)
- **Character-Specific Adaptations**: Special behaviors for unique characters
- **Action Economy**: Prevents AI from performing actions faster than humanly possible
- **Balanced Decision Making**: AI occasionally makes suboptimal choices

---

## 2. Technical Implementation

### 2.1 State Machine Architecture

The AI operates using a finite state machine with four primary states:

```csharp
private enum AIState { 
    SeekBall,           // Searching for the ball
    ApproachAndPickup,  // Moving toward free ball
    EngageWithBall,     // Has ball, planning attack
    Evade               // Avoiding incoming threats
}
```

#### State Transitions:
- **SeekBall → EngageWithBall**: When ball is picked up
- **SeekBall → Evade**: When opponent threat is detected
- **Evade → SeekBall**: When threat is no longer present
- **EngageWithBall → SeekBall**: When ball is lost

### 2.2 Decision-Making Process

The AI's decision-making occurs in discrete "thinking" cycles:

```csharp
void Update()
{
    thinkAccumulator += Time.deltaTime;
    if (thinkAccumulator >= 0.1f)  // 10Hz decision rate
    {
        Think();
        thinkAccumulator = 0f;
    }
}
```

This 10Hz decision rate simulates human reaction times and prevents the AI from making decisions faster than humanly possible.

### 2.3 Threat Assessment System

The AI employs sophisticated threat detection:

```csharp
bool opponentThreat = false;
if (targetOpponent != null && targetOpponent.HasBall())
{
    float distance = Vector3.Distance(targetOpponent.transform.position, 
                                    controlledCharacter.transform.position);
    bool facingMe = Vector3.Dot(targetOpponent.GetThrowDirection(), 
                              (controlledCharacter.transform.position - 
                               targetOpponent.transform.position).normalized) > 0.3f;
    opponentThreat = distance < 8f && (facingMe || distance < 4f);
}
```

**Threat Detection Factors:**
- Distance to opponent
- Opponent's facing direction
- Ball trajectory analysis
- Incoming projectile detection

---

## 3. Difficulty System

### 3.1 Difficulty Parameters

Each difficulty level modifies multiple behavioral parameters:

| Parameter | Easy | Normal | Hard |
|-----------|------|--------|------|
| Reaction Delay | 0.4s | 0.2s | 0.1s |
| Aim Inaccuracy | 25° | 15° | 8° |
| Throw Decision Cooldown | 1.8s | 1.1s | 0.7s |
| Dodge Probability | 10% | 20% | 30% |
| Jump Probability | 6% | 8% | 12% |
| Aggression Level | 0.3 | 0.45 | 0.6 |
| Mistake Chance | 25% | 15% | 5% |
| Reaction Speed | 0.7x | 0.9x | 1.2x |
| Max Actions/Second | 1.2 | 1.8 | 2.5 |

### 3.2 Human-Like Constraints

The AI system implements several constraints to ensure human-like behavior:

#### Action Economy System
```csharp
private float actionBudget = 0f; // Action economy system

void Update()
{
    // Restore action budget over time (like stamina)
    actionBudget = Mathf.Min(actionBudget + Time.deltaTime * 2f, 3f);
}

void ConsumeAction(string actionType)
{
    float cost = actionType switch
    {
        "jump" => 1f,
        "dash" => 1.5f,
        "throw" => 0.5f,
        _ => 0.5f
    };
    actionBudget -= cost;
}
```

#### Mistake Simulation
```csharp
bool makeMistake = Random.value < parms.mistakeChance;

// BALANCED: Sometimes fail to recognize threat (mistake)
if (makeMistake) opponentThreat = false;

// BALANCED: Sometimes hesitate or delay throws
if (Random.value < parms.mistakeChance * 0.5f)
{
    shouldThrow = false;
}
```

---

## 4. Character-Specific Adaptations

### 4.1 Echo Character Specialization

The AI system includes special behaviors for the Echo character:

```csharp
// Check if this is Echo character
var characterData = controlledCharacter?.GetCharacterData();
if (characterData != null && characterData.characterName.ToLower().Contains("echo"))
{
    isEcho = true;
    teleportCooldown = 2.5f; // Shorter cooldown for Echo
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
```

### 4.2 Ability Usage Intelligence

The AI demonstrates intelligent ability usage:

```csharp
// Ultimate - only when charged and holding ball (high damage potential)
if (controlledCharacter.GetUltimateChargePercentage() >= 1f && Random.value < 0.08f)
{
    frame.ultimatePressed = true;
}

// Trick - use on opponent when charged (offensive/defensive utility)
if (controlledCharacter.GetTrickChargePercentage() >= 1f && Random.value < 0.12f)
{
    frame.trickPressed = true;
}
```

---

## 5. Input System Integration

### 5.1 External Input Override

The AI integrates seamlessly with the existing input system:

```csharp
void Update()
{
    // Apply frame to input via external override
    var frame = BuildInputFrame();
    input.ApplyExternalInput(frame);
}
```

### 5.2 Input Frame Construction

The AI builds input frames that mirror human input:

```csharp
PlayerInputHandler.ExternalInputFrame BuildInputFrame()
{
    var frame = new PlayerInputHandler.ExternalInputFrame();
    frame.horizontal = 0f;
    
    // State-specific input generation
    switch (state)
    {
        case AIState.SeekBall:
            // Generate movement toward ball
            break;
        case AIState.Evade:
            // Generate evasive maneuvers
            break;
        case AIState.EngageWithBall:
            // Generate attack patterns
            break;
    }
    
    return frame;
}
```

---

## 6. Performance Optimization

### 6.1 Efficient Context Finding

The AI optimizes object finding through caching:

```csharp
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
    ball = BallManager.Instance != null ? 
           BallManager.Instance.GetCurrentBall() : 
           FindObjectOfType<BallController>();
}
```

### 6.2 Debug Visualization

Optional debug visualization for development:

```csharp
[SerializeField] private bool debugDraw;
[SerializeField] private bool debugMode = false;

void OnDrawGizmos()
{
    if (debugDraw && targetOpponent != null)
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, targetOpponent.transform.position);
    }
}
```

---

## 7. Balancing and Tuning

### 7.1 Difficulty Scaling Philosophy

The difficulty system is designed to provide:
- **Easy**: For new players learning the game
- **Normal**: For experienced players seeking practice
- **Hard**: For advanced players wanting a challenge

### 7.2 Behavioral Constraints

Key constraints ensure fair gameplay:
- **Reaction Delays**: Simulate human response times
- **Action Budget**: Prevent superhuman action rates
- **Mistake Simulation**: Create realistic error patterns
- **Jump Limitations**: Prevent excessive jumping
- **Decision Cooldowns**: Add thinking time between actions

---

## 8. Future Enhancements

### 8.1 Potential Improvements

1. **Machine Learning Integration**: Implement neural networks for adaptive AI
2. **Behavioral Patterns**: Add distinct AI personalities
3. **Advanced Pathfinding**: Implement A* pathfinding for complex movement
4. **Dynamic Difficulty**: Adjust difficulty based on player performance
5. **Team AI**: Support for multiple AI opponents

### 8.2 Technical Debt

Areas for future optimization:
- **Performance Profiling**: Optimize decision-making algorithms
- **Memory Management**: Reduce object allocation in Update loops
- **Code Modularity**: Further separate concerns for maintainability

---

## 9. Conclusion

The AI system in Retro Dodge Rumble successfully provides engaging, human-like opponents that scale appropriately with player skill. The modular architecture, configurable difficulty parameters, and behavioral constraints create a robust foundation for single-player gameplay.

The system demonstrates sophisticated game AI design principles including:
- State-based decision making
- Human-like behavioral constraints
- Scalable difficulty systems
- Character-specific adaptations
- Performance optimization

This implementation showcases advanced understanding of game AI development and provides a solid foundation for future enhancements and expansions.

---

*This technical report documents the AI system implementation as of the current project state and will be updated as the system continues to evolve.*




