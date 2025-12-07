**Command System** What Is It?

A **text-based instruction system** that lets you trigger game actions without writing code. Think of it like giving orders to the game using simple text messages.

---

## The Format

```
type:parameter
```

Or with extra data:
```
type:parameter:value
```

**Examples:**
- `flag:door_unlocked` → Set a flag called "door_unlocked" to true
- `item:rusty_key` → Give player the "rusty_key" item
- `cam:sky:3` → Move camera to "sky" point for 3 seconds

---

## Available Commands

| Command       |              What It Does         | Example             |
|---------------|-----------------------------------!---------------------|
| `flag:name`   | Sets a boolean flag to TRUE       | `flag:met_writer`   |
| `unflag:name` | Sets a boolean flag to FALSE      | `unflag:door_locked`|
| `var:name+5`  | Adds to a number variable         | `var:courage+10`    |
| `var:name-5`  | Subtracts from a number variable  | `var:insanity-5`    |
| `item:id`     | Gives player an item              | `item:ancient_key`  |
| `cam:point`   | Move camera to focus point        | `cam:tree_closeup`  |
| `cam:point:3` | Move camera, auto-return after 3s | `cam:door:4`        |
| `cam:reset`   | Return camera to player           | `cam:reset`         |
| `ending:name` | Trigger a game ending             | `ending:bad_ending` |

---

## Where It's Used

### 1. **Dialogue System**
In `DialogueNode` ScriptableObjects, you add commands that run when the node plays:

```
Speaker: "Take this key, you'll need it."
Commands: item:ancient_key
          flag:received_key
          var:trust+5
```

When this dialogue plays → player gets the key, flag is set, trust increases.

### 2. **Puzzle System**
In `GridPuzzleConfig`, you add commands that run on success/failure:

```
On Solved Commands:
  flag:puzzle_solved
  cam:door_reveal:4
  
On Failed Commands:
  var:insanity-5
```

### 3. **Anywhere Else**
Any system can parse and execute these commands using the same pattern.

---

## How It Works (Simple Version)

```
┌─────────────────────────────────────────────────────────────┐
│  You write:  "flag:door_unlocked"                           │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│  System splits by colon ":"                                 │
│  ├── type = "flag"                                          │
│  └── parameter = "door_unlocked"                            │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│  Switch statement checks type:                              │
│                                                             │
│  case "flag":                                               │
│      GameState.Instance.SetBool("door_unlocked", true);     │
│      break;                                                 │
│                                                             │
│  case "item":                                               │
│      InventoryManager.Instance.AddItem(parameter);          │
│      break;                                                 │
│                                                             │
│  case "cam":                                                │
│      CameraFocusController.Instance.FocusOn(parameter);     │
│      break;                                                 │
└─────────────────────────────────────────────────────────────┘
```

---

## The Actual Code (Simplified)

```csharp
private void ExecuteCommand(string command)
{
    // Split "flag:door_unlocked" into ["flag", "door_unlocked"]
    string[] parts = command.Split(':');
    string type = parts[0].ToLower();
    string param = parts.Length > 1 ? parts[1] : "";

    switch (type)
    {
        case "flag":
            GameState.Instance.SetBool(param, true);
            break;
            
        case "unflag":
            GameState.Instance.SetBool(param, false);
            break;
            
        case "item":
            InventoryManager.Instance.AddItem(param);
            break;
            
        case "var":
            // Parse "courage+5" and modify variable
            ParseAndSetVariable(param);
            break;
            
        case "cam":
            CameraFocusController.Instance.FocusOn(param);
            break;
    }
}
```

---

## Why This Pattern Is Great

| Benefit              |                Explanation                       |
|----------------------|--------------------------------------------------|
| **No coding needed** | Designers can add game logic by typing text      |
| **Easy to read**     | `item:key` is clearer than function calls        |
| **Reusable**         | Same format works in dialogue, puzzles, triggers |
| **Extensible**       | Add new command types by adding a `case`         |
| **Data-driven**      | Commands live in ScriptableObjects, not code     |

---

## Quick Reference Card

```
┌────────────────────────────────────────────┐
│           COMMAND CHEAT SHEET              │
├────────────────────────────────────────────┤
│  flag:name        → Set flag TRUE          │
│  unflag:name      → Set flag FALSE         │
│  var:name+10      → Add to variable        │
│  var:name-5       → Subtract from variable │
│  item:item_id     → Give item to player    │
│  cam:point_id     → Move camera to point   │
│  cam:point_id:3   → Camera + auto-return   │
│  cam:reset        → Return camera          │
│  ending:name      → Trigger ending         │
└────────────────────────────────────────────┘
