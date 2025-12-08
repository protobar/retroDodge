# PS5 DualSense Controller Setup Guide

## Step-by-Step Setup Instructions

### Step 1: Install Unity Input System Package

1. **Open Package Manager**
   - In Unity, go to `Window` > `Package Manager`

2. **Install Input System**
   - In Package Manager, click the `+` button (top-left)
   - Select `Add package by name...`
   - Enter: `com.unity.inputsystem`
   - Click `Add`
   - Wait for installation to complete

3. **Enable Input System**
   - Unity will prompt you to restart the editor
   - Click `Yes` to restart
   - **OR** if prompt doesn't appear:
     - Go to `Edit` > `Project Settings` > `Player`
     - Under `Other Settings`, find `Active Input Handling`
     - Set to: `Input System Package (New)` or `Both` (recommended for compatibility)
     - Restart Unity if prompted

### Step 2: Connect PS5 Controller

1. **USB Connection (Recommended for Haptics)**
   - Connect PS5 DualSense controller to PC via USB cable
   - Windows should recognize it automatically
   - Verify in Windows: `Settings` > `Devices` > `Bluetooth & other devices`

2. **Bluetooth Connection (Optional)**
   - Note: Haptic feedback works best via USB
   - Pair controller via Bluetooth if preferred
   - Some haptic features may be limited over Bluetooth

### Step 3: Verify Controller Detection

1. **Test in Unity**
   - Open `Window` > `Analysis` > `Input Debugger`
   - Connect your PS5 controller
   - You should see `DualSenseGamepadHID` or similar in the device list
   - Test buttons to see if they register

2. **Check Console**
   - Enter Play Mode
   - Look for debug messages: `"[GAMEPAD] PS5 DualSense detected"` or similar

### Step 4: Add HapticFeedbackManager to Scene

1. **Create HapticFeedbackManager GameObject**
   - In your main gameplay scene (e.g., `GameplayArena`), create empty GameObject
   - Name it: `HapticFeedbackManager`
   - Add component: `HapticFeedbackManager` script
   - The script will auto-create singleton instance if not found

2. **Configure Input Settings**
   - Select any `PlayerCharacter` object in scene
   - In Inspector, find `PlayerInputHandler` component
   - Check `Enable Gamepad Input` checkbox (should be enabled by default)
   - Set `Gamepad Deadzone` to `0.2` (default)

3. **Haptic Settings (Optional)**
   - Select `HapticFeedbackManager` GameObject
   - Adjust vibration intensities if needed:
     - `Light Vibration`: 0.15 (default) - jumps, dashes
     - `Medium Vibration`: 0.4 (default) - catches, hits
     - `Strong Vibration`: 0.7 (default) - ultimates, stuns
   - Enable `Debug Mode` to see haptic feedback logs in console

### Step 5: Test Controller Input

1. **Basic Controls**
   - **Left Stick**: Move left/right
   - **X (Cross)**: Jump
   - **Square**: Throw
   - **Circle**: Catch
   - **Triangle**: Pickup
   - **R1**: Dash
   - **R2 (Hold)**: Ultimate (hold to charge, release to throw)
   - **L1**: Trick
   - **L2**: Treat
   - **D-Pad Down**: Duck

2. **Test Haptic Feedback**
   - Jump → Light vibration
   - Catch ball → Medium vibration
   - Get hit → Medium vibration
   - Activate ultimate → Strong vibration
   - Get stunned → Strong vibration

### Step 6: Troubleshooting

**Controller Not Detected:**
- Ensure controller is connected via USB
- Check Windows Device Manager for controller
- Restart Unity Editor
- Try different USB port/cable

**No Haptic Feedback:**
- Ensure controller is connected via USB (not Bluetooth)
- Check `HapticFeedbackManager` is in scene
- Verify `Enable Haptics` is checked
- Check Unity Console for errors

**Input Not Working:**
- Verify `Enable Gamepad Input` is checked in `PlayerInputHandler`
- Check `Active Input Handling` is set to `Both` in Project Settings
- Ensure no other input method is overriding (disable keyboard/mobile temporarily)

**Buttons Feel Wrong:**
- Use `ControllerMappingData` ScriptableObject to customize button mappings
- See `Custom_Controller_Mapping_Guide.md` for detailed instructions
- Create mapping asset: `Create` > `Game` > `Controller Mapping`
- Assign to `PlayerInputHandler` component's `Controller Mapping` field

**Other Players/AI Jumping When You Press Jump:**
- This should be fixed! Gamepad input now only affects your character
- If issue persists, check that `Enable Gamepad Input` is only enabled on YOUR character
- Verify `isMyCharacter` is true for your character in `PlayerInputHandler`

---

## Controller Button Mapping

| Game Action | PS5 Button | Xbox Equivalent |
|-------------|------------|-----------------|
| Move Left/Right | Left Stick | Left Stick |
| Jump | X (Cross) | A |
| Throw | Square | X |
| Catch | Circle | B |
| Pickup | Triangle | Y |
| Dash | R1 | RB |
| Ultimate (Hold) | R2 | RT |
| Trick | L1 | LB |
| Treat | L2 | LT |
| Duck | D-Pad Down | D-Pad Down |

---

## Haptic Feedback Events

| Event | Intensity | Duration | Description |
|-------|-----------|----------|-------------|
| Jump | Light (0.15) | 0.1s | Quick pulse on jump |
| Dash | Light (0.2) | 0.15s | Slight vibration on dash |
| Catch | Medium (0.4) | 0.2s | Satisfying catch feedback |
| Pickup | Medium (0.3) | 0.15s | Ball pickup confirmation |
| Hit Taken | Medium (0.5) | 0.3s | Impact feedback |
| Ultimate Charge | Strong (0.7) | Continuous | Intense vibration during charge |
| Ultimate Release | Strong (0.8) | 0.4s | Powerful release feedback |
| Stun | Strong (0.6) | 0.5s | Stun impact vibration |

---

## Code Files Modified/Created

1. **PlayerInputHandler.cs** - Added gamepad input handling with conditional compilation
2. **HapticFeedbackManager.cs** - New script for PS5 haptic feedback (singleton)
3. **PlayerCharacter.cs** - Integrated haptic feedback calls for:
   - Jump (light vibration)
   - Dash (light vibration)
   - Successful catch (medium vibration)
   - Ultimate activation (continuous strong vibration)
   - Ultimate release (strong vibration pulse)
   - Stun (strong vibration)
4. **PlayerHealth.cs** - Added haptic feedback on taking damage (medium/strong based on hit type)

## Important Notes

- **Backward Compatible**: Code uses `#if ENABLE_INPUT_SYSTEM` preprocessor directives
- **No Breaking Changes**: If Input System is not installed, gamepad input is simply disabled
- **Keyboard/Mobile Still Work**: Gamepad input is additive, existing input methods remain functional
- **Auto-Detection**: HapticFeedbackManager automatically detects PS5 controllers
- **Singleton Pattern**: HapticFeedbackManager uses singleton pattern, persists across scenes

---

## Notes

- **Keyboard/Mobile Input Still Works**: Gamepad input is additive, not replacing existing input methods
- **Multiplayer Compatible**: Haptic feedback only triggers for local player
- **Performance**: Haptic feedback has minimal performance impact
- **Platform**: Works on Windows, macOS (USB only). Linux support varies.

---

*Setup completed! Your PS5 controller should now work with full haptic feedback support.*

