# Fix: Input System TypeInitializationException Error

## Problem
You're getting `TypeInitializationException` and `ArgumentNullException` errors in the Unity Editor console, even when not in Play Mode. This happens because Unity's Input System is trying to initialize but can't find the required InputSettings asset.

## Solution 1: Create Input System Settings (Recommended)

1. **Open Project Settings**
   - Go to `Edit` > `Project Settings`
   - Click on `Input System Package` in the left sidebar
   - If you don't see it, the Input System package might not be fully installed

2. **Let Unity Create Settings Automatically**
   - Unity should automatically create the settings when you open the Input System Package settings
   - If it doesn't, try this:
     - Close Unity
     - Delete `Library` folder (Unity will regenerate it)
     - Reopen Unity
     - Go to `Edit` > `Project Settings` > `Input System Package`
     - Unity should create the settings automatically

## Solution 2: Configure Active Input Handling

1. **Set Input Handling Mode**
   - Go to `Edit` > `Project Settings` > `Player`
   - Under `Other Settings`, find `Active Input Handling`
   - Set it to: **`Both`** (recommended) or **`Input System Package (New)`**
   - **Do NOT** set it to `Input Manager (Old)` if you want gamepad support

2. **Restart Unity**
   - Close Unity completely
   - Reopen the project
   - The error should be resolved

## Solution 3: Disable Input System in Editor (If Not Needed)

If you only need Input System at runtime and want to avoid editor errors:

1. **Create Editor Script** (temporary fix)
   - Create folder: `Assets/Editor` (if it doesn't exist)
   - Create script: `DisableInputSystemInEditor.cs`
   - This will prevent Input System from initializing in editor

**Note:** This is a workaround. Solution 1 or 2 is preferred.

## Solution 4: Reinstall Input System Package

If the above doesn't work:

1. **Remove Input System**
   - `Window` > `Package Manager`
   - Find `Input System` package
   - Click `Remove`

2. **Clear Library Folder**
   - Close Unity
   - Delete `Library` folder in project root
   - Reopen Unity

3. **Reinstall Input System**
   - `Window` > `Package Manager` > `+` > `Add package by name...`
   - Enter: `com.unity.inputsystem`
   - Click `Add`
   - Restart Unity when prompted

## Quick Fix (Try This First) ⭐

**Step 1: Configure Project Settings**
1. In Unity, go to `Edit` > `Project Settings` > `Player`
2. Under `Other Settings`, scroll down to find `Active Input Handling`
3. Set it to: **`Both`** (this allows both old and new input systems)
4. **Save** (Ctrl+S or Cmd+S)

**Step 2: Access Input System Settings**
1. Go to `Edit` > `Project Settings`
2. In the left sidebar, look for **`Input System Package`**
3. Click on it - Unity will automatically create the InputSystemSettings asset
4. If you see settings, the error should be resolved

**Step 3: Restart Unity**
1. Close Unity completely
2. Reopen your project
3. Check Console - errors should be gone

**If Step 2 doesn't work:**
1. Close Unity
2. Delete `Library` folder in your project root
3. Reopen Unity (it will regenerate Library)
4. Go to `Edit` > `Project Settings` > `Input System Package` again
5. Unity should create settings automatically

## Verify Fix

After applying a solution:
- Check Unity Console - errors should be gone
- Go to `Edit` > `Project Settings` > `Input System Package` - settings should be visible
- Enter Play Mode - no errors should appear

---

**Most Common Fix:** Solution 2 (Set Active Input Handling to "Both") + Restart Unity

