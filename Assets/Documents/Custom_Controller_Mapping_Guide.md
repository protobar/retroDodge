# Custom Controller Mapping Guide

## Overview

You can now customize controller button mappings using ScriptableObjects! This allows you to create different control schemes for different players, game modes, or preferences.

## Quick Setup

### Step 1: Create Controller Mapping Asset

1. **In Unity Project Window**
   - Right-click in `Assets/ScriptableObjects` folder (or create it if it doesn't exist)
   - Select `Create` > `Game` > `Controller Mapping`
   - Name it: `DefaultControllerMapping` (or any name you prefer)

2. **Configure Your Mapping**
   - Select the newly created asset
   - In Inspector, you'll see all button mappings
   - Change buttons to your preference:
     - **Jump**: ButtonSouth (X/A), ButtonWest (Square/X), etc.
     - **Throw**: ButtonWest (Square/X), ButtonNorth (Triangle/Y), etc.
     - And so on...

### Step 2: Assign to PlayerCharacter

1. **In Your Gameplay Scene**
   - Select a `PlayerCharacter` GameObject
   - Find `PlayerInputHandler` component in Inspector
   - Under `Gamepad Settings`, find `Controller Mapping` field
   - Drag your `ControllerMappingData` asset into the field
   - **OR** leave it empty to use default mapping

2. **Test It!**
   - Enter Play Mode
   - Use your controller - buttons should now match your custom mapping!

## Available Button Options

### Face Buttons
- **ButtonSouth**: X (PS5) / A (Xbox)
- **ButtonWest**: Square (PS5) / X (Xbox)
- **ButtonEast**: Circle (PS5) / B (Xbox)
- **ButtonNorth**: Triangle (PS5) / Y (Xbox)

### Shoulder Buttons
- **LeftShoulder**: L1 (PS5) / LB (Xbox)
- **RightShoulder**: R1 (PS5) / RB (Xbox)

### Triggers
- **LeftTrigger**: L2 (PS5) / LT (Xbox)
- **RightTrigger**: R2 (PS5) / RT (Xbox)

### D-Pad
- **DPadUp**: D-Pad Up
- **DPadDown**: D-Pad Down
- **DPadLeft**: D-Pad Left
- **DPadRight**: D-Pad Right

### Stick Buttons
- **LeftStick**: Press left stick
- **RightStick**: Press right stick

### System Buttons
- **Start**: Start/Menu button
- **Select**: Select/View button

## Example Custom Mappings

### Example 1: FPS-Style Layout
```
Jump: RightShoulder (R1)
Throw: RightTrigger (R2)
Catch: LeftShoulder (L1)
Pickup: ButtonNorth (Triangle/Y)
Dash: LeftTrigger (L2)
Ultimate: ButtonWest (Square/X)
Trick: ButtonEast (Circle/B)
Treat: ButtonSouth (X/A)
Duck: DPadDown
```

### Example 2: Fighting Game Layout
```
Jump: ButtonSouth (X/A)
Throw: ButtonWest (Square/X)
Catch: ButtonEast (Circle/B)
Pickup: ButtonNorth (Triangle/Y)
Dash: RightShoulder (R1)
Ultimate: RightTrigger (R2)
Trick: LeftShoulder (L1)
Treat: LeftTrigger (L2)
Duck: DPadDown
```

## Advanced Settings

### Movement Stick
- **LeftStick**: Default (recommended)
- **RightStick**: Alternative (for left-handed players)

### Deadzone
- **Movement Deadzone**: 0.1 - 0.5
- Lower = more sensitive
- Higher = less sensitive
- Default: 0.2

### Trigger Thresholds
- **Trigger Press Threshold**: 0.1 - 1.0 (default: 0.5)
  - How far you need to press trigger to activate
- **Trigger Release Threshold**: 0.1 - 1.0 (default: 0.3)
  - How far you need to release to deactivate
  - Must be less than Press Threshold

## Creating Multiple Mappings

You can create multiple mapping assets for different scenarios:

1. **Player1Mapping**: Custom controls for Player 1
2. **Player2Mapping**: Custom controls for Player 2
3. **CompetitiveMapping**: Optimized for competitive play
4. **CasualMapping**: Easier controls for casual play

Then assign different mappings to different PlayerCharacters in your scene!

## Tips

- **Test in Play Mode**: Always test your mappings in Play Mode
- **Save Presets**: Create multiple mapping assets for different preferences
- **Share Mappings**: You can share `.asset` files with other players
- **Default Mapping**: If no mapping is assigned, default controls are used (same as before)

## Troubleshooting

**Mapping Not Working?**
- Ensure `Controller Mapping` field is assigned in `PlayerInputHandler`
- Check that `Enable Gamepad Input` is checked
- Verify controller is connected

**Buttons Feel Wrong?**
- Check your mapping asset settings
- Verify button assignments in Inspector
- Test with different mapping presets

**Can't Find Controller Mapping Option?**
- Ensure `ControllerMappingData.cs` script exists
- Refresh Unity (Assets > Refresh)
- Check that ScriptableObject menu shows "Game/Controller Mapping"

---

**Enjoy your custom controller setup!** 🎮

