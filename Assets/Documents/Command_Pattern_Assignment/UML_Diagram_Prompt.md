# UML Class Diagram Generation Prompt for Gemini Nano Banana

## 🎯 Task
Generate an accurate UML Class Diagram for the Command Design Pattern implementation in Retro Dodge Rumble game.

---

## 📋 Detailed Prompt for Gemini Nano Banana

**Copy this entire prompt to Gemini Nano Banana:**

---

### PROMPT START

Create a detailed UML Class Diagram showing the Command Design Pattern implementation for a Unity game called "Retro Dodge Rumble" - a 2.5D multiplayer dodgeball fighting game.

## Context
The game has player actions (throw ball, catch, jump, dash, duck) and special abilities (ultimate, trick, treat) that need to be encapsulated as commands.

## Required UML Diagram Components

### 1. INTERFACE
**ICommand** (Interface)
- `+Execute(): void` (public, abstract)
- `+Undo(): void` (public, abstract)
- `+CanExecute(): bool` (public, abstract)

### 2. ABSTRACT CLASS (Optional but recommended)
**AbstractPlayerCommand** (Abstract Class)
- `#receiver: PlayerCharacter` (protected field)
- `+AbstractPlayerCommand(receiver: PlayerCharacter)` (constructor)
- `+CanExecute(): bool` (abstract method)

### 3. CONCRETE COMMAND CLASSES (10 classes total)

**ThrowBallCommand** (implements ICommand)
- `-throwType: ThrowType` (private field)
- `-damage: int` (private field)
- `+ThrowBallCommand(receiver: PlayerCharacter, throwType: ThrowType, damage: int)` (constructor)
- `+Execute(): void` (public method)
- `+Undo(): void` (public method)
- `+CanExecute(): bool` (public method)

**CatchBallCommand** (implements ICommand)
- `-ball: BallController` (private field)
- `+CatchBallCommand(receiver: PlayerCharacter, ball: BallController)` (constructor)
- `+Execute(): void`
- `+Undo(): void`
- `+CanExecute(): bool`

**PickupBallCommand** (implements ICommand)
- `-ball: BallController` (private field)
- `+PickupBallCommand(receiver: PlayerCharacter, ball: BallController)` (constructor)
- `+Execute(): void`
- `+Undo(): void`
- `+CanExecute(): bool`

**JumpCommand** (implements ICommand)
- `+JumpCommand(receiver: PlayerCharacter)` (constructor)
- `+Execute(): void`
- `+Undo(): void`
- `+CanExecute(): bool`

**DoubleJumpCommand** (implements ICommand)
- `+DoubleJumpCommand(receiver: PlayerCharacter)` (constructor)
- `+Execute(): void`
- `+Undo(): void`
- `+CanExecute(): bool`

**DashCommand** (implements ICommand)
- `-direction: Vector3` (private field)
- `+DashCommand(receiver: PlayerCharacter, direction: Vector3)` (constructor)
- `+Execute(): void`
- `+Undo(): void`
- `+CanExecute(): bool`

**DuckCommand** (implements ICommand)
- `-isDucking: bool` (private field)
- `+DuckCommand(receiver: PlayerCharacter, isDucking: bool)` (constructor)
- `+Execute(): void`
- `+Undo(): void`
- `+CanExecute(): bool`

**ActivateUltimateCommand** (implements ICommand)
- `-ultimateType: UltimateType` (private field)
- `+ActivateUltimateCommand(receiver: PlayerCharacter, ultimateType: UltimateType)` (constructor)
- `+Execute(): void`
- `+Undo(): void`
- `+CanExecute(): bool`

**ActivateTrickCommand** (implements ICommand)
- `-trickType: TrickType` (private field)
- `+ActivateTrickCommand(receiver: PlayerCharacter, trickType: TrickType)` (constructor)
- `+Execute(): void`
- `+Undo(): void`
- `+CanExecute(): bool`

**ActivateTreatCommand** (implements ICommand)
- `-treatType: TreatType` (private field)
- `+ActivateTreatCommand(receiver: PlayerCharacter, treatType: TreatType)` (constructor)
- `+Execute(): void`
- `+Undo(): void`
- `+CanExecute(): bool`

### 4. INVOKER CLASS
**CommandInvoker** (Class)
- `-commandQueue: Queue<ICommand>` (private field)
- `-commandHistory: CommandHistory` (private field)
- `+CommandInvoker()` (constructor)
- `+ExecuteCommand(cmd: ICommand): void` (public method)
- `+UndoLastCommand(): void` (public method)
- `+QueueCommand(cmd: ICommand): void` (public method)
- `+ProcessCommandQueue(): void` (public method)

### 5. HISTORY CLASS
**CommandHistory** (Class)
- `-history: List<ICommand>` (private field)
- `-maxHistorySize: int` (private field)
- `+CommandHistory(maxSize: int)` (constructor)
- `+AddCommand(cmd: ICommand): void` (public method)
- `+GetHistory(): List<ICommand>` (public method)
- `+ClearHistory(): void` (public method)
- `+GetLastCommand(): ICommand` (public method)

### 6. RECEIVER CLASS
**PlayerCharacter** (Class - existing game class)
- `+hasBall: bool` (public field)
- `+isGrounded: bool` (public field)
- `+movementEnabled: bool` (public field)
- `+ThrowBall(): void` (public method)
- `+CatchBall(ball: BallController): void` (public method)
- `+PickupBall(ball: BallController): void` (public method)
- `+Jump(): void` (public method)
- `+DoubleJump(): void` (public method)
- `+PerformDash(): void` (public method)
- `+SetDucking(isDucking: bool): void` (public method)
- `+ActivateUltimate(): void` (public method)
- `+ActivateTrick(): void` (public method)
- `+ActivateTreat(): void` (public method)
- `+IsStunned(): bool` (public method)

### 7. CLIENT CLASS
**PlayerInputHandler** (Class - existing game class)
- `-commandInvoker: CommandInvoker` (private field)
- `-playerCharacter: PlayerCharacter` (private field)
- `+OnThrowPressed(): void` (public method)
- `+OnCatchPressed(): void` (public method)
- `+OnPickupPressed(): void` (public method)
- `+OnJumpPressed(): void` (public method)
- `+OnDashPressed(): void` (public method)
- `+OnDuckPressed(): void` (public method)
- `+OnUltimatePressed(): void` (public method)
- `+OnTrickPressed(): void` (public method)
- `+OnTreatPressed(): void` (public method)

### 8. SUPPORTING CLASSES (for completeness)
**BallController** (Class - existing game class)
- `+GetBallState(): BallState` (public method)
- `+OnCaught(player: PlayerCharacter): void` (public method)

**ThrowType** (Enum)
- Normal
- JumpThrow
- Ultimate

**UltimateType** (Enum)
- PowerThrow
- MultiThrow
- Curveball

**TrickType** (Enum)
- SlowSpeed
- Freeze
- InstantDamage

**TreatType** (Enum)
- Shield
- Heal
- SpeedBoost

## Relationships (Use UML arrows)

1. **ICommand** ← (implements, dashed arrow) ← All 10 Concrete Command Classes
2. **AbstractPlayerCommand** ← (extends, solid arrow) ← All 10 Concrete Commands (if using abstract class)
3. **CommandInvoker** → (uses, solid arrow) → **ICommand** (association)
4. **CommandInvoker** → (uses, solid arrow) → **CommandHistory** (composition - filled diamond)
5. **All Commands** → (calls, dashed arrow) → **PlayerCharacter** (dependency)
6. **PlayerInputHandler** → (creates, dashed arrow) → **ICommand** (dependency)
7. **PlayerInputHandler** → (uses, solid arrow) → **CommandInvoker** (association)
8. **PlayerInputHandler** → (uses, solid arrow) → **PlayerCharacter** (association)
9. **Commands** → (uses, dashed arrow) → **BallController** (dependency)
10. **Commands** → (uses, dashed arrow) → Enums (ThrowType, UltimateType, etc.) (dependency)

## Visual Requirements

1. **Use standard UML notation**:
   - Interface: «interface» ICommand (with guillemets)
   - Abstract class: AbstractPlayerCommand (italicized name)
   - Classes: Rectangle boxes with three compartments (name, attributes, methods)
   - Visibility: + (public), - (private), # (protected)

2. **Arrows**:
   - Inheritance: Solid line with hollow triangle arrowhead
   - Implementation: Dashed line with hollow triangle arrowhead
   - Association: Solid line with arrow
   - Dependency: Dashed line with arrow
   - Composition: Solid line with filled diamond

3. **Layout**:
   - Place ICommand at the top center
   - Place AbstractPlayerCommand (if used) below ICommand
   - Place all 10 Concrete Commands in a grid below
   - Place CommandInvoker and CommandHistory on the right side
   - Place PlayerCharacter (Receiver) at the bottom center
   - Place PlayerInputHandler (Client) on the left side
   - Place supporting classes (BallController, Enums) at the bottom

4. **Color Coding** (optional but helpful):
   - Interfaces: Light blue
   - Abstract classes: Light yellow
   - Concrete commands: Light green
   - Invoker/History: Light orange
   - Receiver/Client: Light pink
   - Supporting classes: Light gray

5. **Labels on relationships**:
   - Label associations with role names (e.g., "receiver", "invoker")
   - Label dependencies with brief descriptions if needed

## Additional Notes

- Show that CommandInvoker has a composition relationship with CommandHistory (CommandHistory is part of CommandInvoker)
- Show that all commands depend on PlayerCharacter (they call its methods)
- Show that PlayerInputHandler creates commands and passes them to CommandInvoker
- Include multiplicity where appropriate (e.g., CommandInvoker can have many ICommand in queue)
- Use proper UML stereotypes if needed (e.g., «creates», «uses»)

## Output Format

Generate a clean, professional UML Class Diagram that clearly shows:
1. All classes with their attributes and methods
2. All relationships between classes
3. Proper UML notation and symbols
4. Clear layout that's easy to read
5. All 10 concrete command classes visible

The diagram should be suitable for academic presentation and clearly demonstrate the Command Design Pattern implementation in the game context.

---

### PROMPT END

---

## 📝 Usage Instructions

1. Copy the entire content between "PROMPT START" and "PROMPT END"
2. Paste it into Gemini Nano Banana
3. Request: "Generate a UML Class Diagram based on this specification"
4. The AI should generate a comprehensive UML diagram showing all components

## 🎨 Expected Output

The generated diagram should show:
- ✅ ICommand interface at the top
- ✅ 10 concrete command classes (ThrowBallCommand, CatchBallCommand, etc.)
- ✅ CommandInvoker and CommandHistory classes
- ✅ PlayerCharacter (Receiver) class
- ✅ PlayerInputHandler (Client) class
- ✅ Supporting classes (BallController, Enums)
- ✅ All relationships properly labeled
- ✅ Proper UML notation

## 🔍 Verification Checklist

After generation, verify the diagram includes:
- [ ] ICommand interface with 3 methods
- [ ] All 10 concrete command classes
- [ ] CommandInvoker with queue and history
- [ ] CommandHistory class
- [ ] PlayerCharacter with all action methods
- [ ] PlayerInputHandler with command creation methods
- [ ] Proper inheritance arrows (ICommand ← Commands)
- [ ] Proper association arrows (Invoker → Commands)
- [ ] Proper dependency arrows (Commands → PlayerCharacter)
- [ ] Supporting enums and classes
- [ ] Clear, readable layout

---

*This prompt is designed to generate a comprehensive UML Class Diagram for the Command Design Pattern implementation in Retro Dodge Rumble.*





