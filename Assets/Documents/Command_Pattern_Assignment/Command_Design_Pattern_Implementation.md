# Command Design Pattern Implementation for Retro Dodge Rumble

## 📋 Assignment Overview

**Task**: Implement Command Design Pattern (a specialized design pattern for games) on your FYP. Create diagram according to your FYP. This task is FYP group-wise.

**Project**: Retro Dodge Rumble - A 2.5D multiplayer dodgeball fighting game built in Unity

---

## 🎯 What is the Command Design Pattern?

The Command Design Pattern is a behavioral design pattern that encapsulates a request as an object, thereby allowing you to parameterize clients with different requests, queue requests, log requests, and support undoable operations. In game development, this pattern is particularly valuable for:

- **Input Handling**: Decoupling input from actions
- **Action Queuing**: Queueing actions for execution
- **Replay Systems**: Recording and replaying player actions
- **Undo/Redo**: Implementing undo functionality
- **Network Synchronization**: Serializing actions for multiplayer
- **AI Systems**: Allowing AI to use the same action system as players

---

## 🎮 Systems in Retro Dodge Rumble Suitable for Command Pattern

Based on the codebase analysis, the following systems are ideal candidates for Command pattern implementation:

### 1. **Player Action Commands System** ⭐ (Primary Focus)

**Current Implementation**: Direct method calls in `PlayerCharacter.cs` and `PlayerInputHandler.cs`

**Actions to Encapsulate**:
- `ThrowBallCommand` - Throwing the ball (normal, jump throw, ultimate)
- `CatchBallCommand` - Catching incoming balls
- `PickupBallCommand` - Picking up balls from the ground
- `JumpCommand` - Jumping action
- `DoubleJumpCommand` - Double jump (character-specific)
- `DashCommand` - Dash movement ability
- `DuckCommand` - Ducking to avoid low throws

**Benefits**:
- Decouple input handling from action execution
- Enable action queuing for responsive controls
- Support replay/recording systems
- Allow AI to use same command system
- Enable network serialization of actions

**Current Code Location**:
- `Assets/Scripts/Characters/PlayerCharacter.cs` (lines 498-771)
- `Assets/Scripts/Input/PlayerInputHandler.cs` (lines 263-632)
- `Assets/Scripts/System/CatchSystem.cs` (lines 259-347)

---

### 2. **Ability Commands System** ⭐ (Primary Focus)

**Current Implementation**: Direct method calls in `PlayerCharacter.cs`

**Actions to Encapsulate**:
- `ActivateUltimateCommand` - Ultimate ability activation (Power Throw, Multi Throw, Curveball)
- `ActivateTrickCommand` - Trick ability (Slow Speed, Freeze, Instant Damage)
- `ActivateTreatCommand` - Treat ability (Shield, Heal, Speed Boost)

**Benefits**:
- Centralize ability execution logic
- Enable ability queuing during cooldowns
- Support ability replay for debugging
- Allow ability macros/combos
- Simplify network synchronization

**Current Code Location**:
- `Assets/Scripts/Characters/PlayerCharacter.cs` (lines 1115-1440)
- Character-specific ability types defined in `CharacterData.cs`

---

### 3. **Movement Commands System**

**Current Implementation**: Direct movement in `PlayerCharacter.HandleMovement()`

**Actions to Encapsulate**:
- `MoveLeftCommand` - Horizontal movement left
- `MoveRightCommand` - Horizontal movement right
- `StopMovementCommand` - Stop horizontal movement

**Benefits**:
- Enable movement prediction for network games
- Support movement replay
- Allow movement macros
- Simplify AI movement decisions

**Current Code Location**:
- `Assets/Scripts/Characters/PlayerCharacter.cs` (lines 557-625)

---

## 🏗️ Proposed Command Pattern Architecture

### Core Components

1. **ICommand Interface** - Base interface for all commands
2. **Concrete Commands** - Specific action implementations
3. **CommandInvoker** - Executes and manages commands
4. **CommandHistory** - Stores command history for undo/replay
5. **CommandQueue** - Queues commands for delayed execution

### Key Classes Structure

```
ICommand (Interface)
├── Execute()
├── Undo()
└── CanExecute()

PlayerActionCommand (Abstract)
├── PlayerCharacter receiver
└── Execute()

Concrete Commands:
├── ThrowBallCommand
├── CatchBallCommand
├── PickupBallCommand
├── JumpCommand
├── DoubleJumpCommand
├── DashCommand
├── DuckCommand
├── ActivateUltimateCommand
├── ActivateTrickCommand
└── ActivateTreatCommand

CommandInvoker
├── ExecuteCommand(ICommand)
├── UndoLastCommand()
└── QueueCommand(ICommand)

CommandHistory
├── AddCommand(ICommand)
├── GetHistory()
└── ClearHistory()
```

---

## 📊 Detailed System Analysis

### System 1: Player Action Commands

**Current Flow**:
```
PlayerInputHandler → PlayerCharacter → Direct Method Call
```

**Proposed Flow with Command Pattern**:
```
PlayerInputHandler → CommandInvoker → ICommand → PlayerCharacter
```

**Example: ThrowBallCommand**

**Current Implementation** (PlayerCharacter.cs:835-912):
- Direct method call: `ThrowBall()`
- Checks conditions inline
- Immediate execution

**Command Pattern Implementation**:
```csharp
public class ThrowBallCommand : ICommand
{
    private PlayerCharacter receiver;
    private ThrowType throwType;
    private int damage;
    
    public ThrowBallCommand(PlayerCharacter player, ThrowType type, int dmg)
    {
        receiver = player;
        throwType = type;
        damage = dmg;
    }
    
    public void Execute()
    {
        if (receiver.hasBall && receiver.movementEnabled)
        {
            receiver.ThrowBall();
        }
    }
    
    public void Undo()
    {
        // Revert ball state if needed
    }
    
    public bool CanExecute()
    {
        return receiver.hasBall && !receiver.IsStunned();
    }
}
```

**Benefits**:
- Input can queue throw commands
- AI can use same command
- Network can serialize command
- Replay system can record commands

---

### System 2: Ability Commands

**Current Flow**:
```
PlayerInputHandler → PlayerCharacter → ActivateUltimate() / ActivateTrick() / ActivateTreat()
```

**Proposed Flow with Command Pattern**:
```
PlayerInputHandler → CommandInvoker → AbilityCommand → PlayerCharacter
```

**Example: ActivateUltimateCommand**

**Current Implementation** (PlayerCharacter.cs:1115-1255):
- Direct method call: `ActivateUltimate()`
- Complex state management
- Animation sequence handling

**Command Pattern Implementation**:
```csharp
public class ActivateUltimateCommand : ICommand
{
    private PlayerCharacter receiver;
    private UltimateType ultimateType;
    
    public ActivateUltimateCommand(PlayerCharacter player, UltimateType type)
    {
        receiver = player;
        ultimateType = type;
    }
    
    public void Execute()
    {
        if (CanExecute())
        {
            receiver.ActivateUltimate();
        }
    }
    
    public bool CanExecute()
    {
        return receiver.hasBall && 
               receiver.abilityCharges[0] >= 1.0f && 
               !receiver.abilityCooldowns[0] &&
               !receiver.IsStunned();
    }
}
```

**Benefits**:
- Ability queuing during cooldowns
- Ability combo system
- Network synchronization
- Ability replay for debugging

---

## 🎯 Implementation Benefits for Retro Dodge Rumble

### 1. **Network Synchronization**
- Commands can be serialized and sent over network
- Consistent action execution across clients
- Reduced network traffic (send commands, not states)

### 2. **Replay System**
- Record all commands during match
- Replay matches by re-executing commands
- Enable match analysis and highlights

### 3. **AI Integration**
- AI can use same command system as players
- Consistent behavior between AI and human players
- Easier AI behavior debugging

### 4. **Input Responsiveness**
- Queue commands during animation locks
- Execute queued commands when available
- Better player experience

### 5. **Debugging & Testing**
- Log all commands for debugging
- Test specific command sequences
- Replay bug scenarios

### 6. **Undo/Redo (Future Feature)**
- Implement undo for training mode
- Allow players to practice scenarios
- Support tutorial system

---

## 🔄 Command Execution Flow

### Current Flow (Without Command Pattern)
```
Input Event → PlayerInputHandler → PlayerCharacter Method → Action Execution
```

### Proposed Flow (With Command Pattern)
```
Input Event → PlayerInputHandler → CommandInvoker → Command Creation → 
Command Queue → Command Execution → PlayerCharacter → Action Execution
```

### Example: Player Presses Throw Key

1. **Input Detection**: `PlayerInputHandler` detects throw key press
2. **Command Creation**: Creates `ThrowBallCommand` with player reference
3. **Command Validation**: Checks `CanExecute()` (has ball, not stunned, etc.)
4. **Command Queuing**: Adds to `CommandQueue` if conditions not met, or executes immediately
5. **Command Execution**: `CommandInvoker` calls `Execute()` on command
6. **Action Execution**: Command calls `PlayerCharacter.ThrowBall()`
7. **History Recording**: Command added to `CommandHistory` for replay

---

## 📐 UML Class Diagram Description

The UML diagram should show:

### Core Pattern Structure:
1. **ICommand Interface**
   - `+Execute(): void`
   - `+Undo(): void`
   - `+CanExecute(): bool`

2. **AbstractCommand (Optional)**
   - `#receiver: PlayerCharacter`
   - Common functionality for all commands

3. **Concrete Commands** (10 classes):
   - `ThrowBallCommand`
   - `CatchBallCommand`
   - `PickupBallCommand`
   - `JumpCommand`
   - `DoubleJumpCommand`
   - `DashCommand`
   - `DuckCommand`
   - `ActivateUltimateCommand`
   - `ActivateTrickCommand`
   - `ActivateTreatCommand`

4. **CommandInvoker**
   - `-commandQueue: Queue<ICommand>`
   - `-commandHistory: List<ICommand>`
   - `+ExecuteCommand(cmd: ICommand): void`
   - `+UndoLastCommand(): void`
   - `+QueueCommand(cmd: ICommand): void`

5. **CommandHistory**
   - `-history: List<ICommand>`
   - `+AddCommand(cmd: ICommand): void`
   - `+GetHistory(): List<ICommand>`
   - `+ClearHistory(): void`

6. **PlayerCharacter** (Receiver)
   - Existing methods that commands will call
   - `+ThrowBall(): void`
   - `+CatchBall(): void`
   - `+ActivateUltimate(): void`
   - etc.

7. **PlayerInputHandler** (Client)
   - `-commandInvoker: CommandInvoker`
   - Creates commands based on input
   - `+OnThrowPressed(): void`
   - `+OnCatchPressed(): void`
   - etc.

### Relationships:
- `ICommand` ← (implements) ← All Concrete Commands
- `CommandInvoker` → (uses) → `ICommand`
- `CommandInvoker` → (uses) → `CommandHistory`
- All Commands → (calls) → `PlayerCharacter`
- `PlayerInputHandler` → (creates) → Commands
- `PlayerInputHandler` → (uses) → `CommandInvoker`

---

## 🎨 Game-Specific Considerations

### 1. **Network Multiplayer (Photon PUN2)**
- Commands must be serializable for network transmission
- Commands should include player ID and timestamp
- Network authority validation in command execution

### 2. **Animation System**
- Commands may need to wait for animations
- Queue commands during animation locks
- Animation events can trigger command execution

### 3. **State Validation**
- Commands check game state before execution
- Stun/fallback states prevent command execution
- Ball possession required for certain commands

### 4. **AI Integration**
- AI controller creates commands based on decisions
- Same command system for AI and human players
- AI can queue commands for complex behaviors

### 5. **Ability System**
- Commands respect cooldowns and charges
- Ability commands have different execution paths
- Ultimate commands have complex animation sequences

---

## 📝 Implementation Priority

### Phase 1: Core Infrastructure (High Priority)
1. Create `ICommand` interface
2. Implement `CommandInvoker`
3. Implement `CommandHistory`
4. Create base `PlayerActionCommand` class

### Phase 2: Player Actions (High Priority)
1. `ThrowBallCommand`
2. `CatchBallCommand`
3. `JumpCommand`
4. `DashCommand`

### Phase 3: Abilities (Medium Priority)
1. `ActivateUltimateCommand`
2. `ActivateTrickCommand`
3. `ActivateTreatCommand`

### Phase 4: Advanced Features (Low Priority)
1. Command queuing system
2. Network serialization
3. Replay system integration
4. Undo functionality

---

## 🔍 Code References

### Key Files for Command Pattern Implementation:

1. **PlayerCharacter.cs** (`Assets/Scripts/Characters/PlayerCharacter.cs`)
   - Lines 498-771: Movement and action methods
   - Lines 1115-1440: Ability activation methods
   - Lines 835-912: Throw ball implementation

2. **PlayerInputHandler.cs** (`Assets/Scripts/Input/PlayerInputHandler.cs`)
   - Lines 263-632: Input handling and method calls
   - Lines 832-897: Input getter methods

3. **CatchSystem.cs** (`Assets/Scripts/System/CatchSystem.cs`)
   - Lines 259-347: Catch ball implementation

4. **BallController.cs** (`Assets/Scripts/Ball/BallController.cs`)
   - Lines 985-993: Throw ball method

---

## ✅ Summary

The Command Design Pattern is highly suitable for Retro Dodge Rumble's action-based gameplay. By encapsulating player actions and abilities as commands, the game can benefit from:

- **Better Architecture**: Decoupled input from execution
- **Network Support**: Serializable commands for multiplayer
- **Replay System**: Record and replay matches
- **AI Integration**: Unified action system
- **Future Features**: Undo, macros, combos

The primary systems for implementation are:
1. **Player Action Commands** (Throw, Catch, Jump, Dash, etc.)
2. **Ability Commands** (Ultimate, Trick, Treat)

These systems currently use direct method calls that can be refactored to use the Command pattern, providing significant architectural improvements and enabling advanced features.

---

*This document provides a comprehensive analysis of Command Design Pattern implementation opportunities in Retro Dodge Rumble. The pattern will improve code maintainability, enable advanced features, and provide a solid foundation for future game development.*






