# Fighting Game Camera System Setup

## Overview
The new camera system provides a KOF/Street Fighter style camera that follows both players smoothly, maintaining competitive visibility at all times.

## Components

### 1. CameraController.cs
- **Main camera logic** - follows both players
- **Automatic player detection** - finds PlayerCharacter objects
- **Dynamic zooming** - zooms based on player distance
- **Smooth movement** - prevents jerky camera motion
- **Camera shake** - for impact effects

### 2. CameraManager.cs
- **Integration layer** - connects camera to match system
- **Event handling** - responds to game events
- **Camera shake triggers** - for hits and ultimates

## Setup Instructions

### 1. Camera Setup
1. **Add CameraController** to your main camera
2. **Configure settings** in the inspector:
   - `Base Position`: (0, 8, -12) - default camera position
   - `Follow Speed`: 8 - how fast camera follows
   - `Min/Max Distance`: 8/20 - zoom range
   - `Ideal Distance`: 12 - preferred zoom level

### 2. Camera Bounds
1. **Set arena bounds** to match your arena:
   - `Left Bound`: -12
   - `Right Bound`: 12
   - `Top Bound`: 6
   - `Bottom Bound`: 2

### 3. MatchManager Integration
1. **Add CameraManager** to your MatchManager object
2. **Assign CameraManager** to MatchManager's camera field
3. **Camera will auto-refresh** when players spawn

## Features

### ✅ Automatic Player Detection
- Finds both players automatically
- Works in offline and online modes
- Handles player respawning

### ✅ Dynamic Zooming
- Zooms out when players are far apart
- Zooms in when players are close
- Maintains competitive view

### ✅ Smooth Movement
- No jerky camera motion
- Smooth position transitions
- Maintains fixed rotation (fighting game style)

### ✅ Camera Shake
- Shake on player hits
- Shake on ultimate abilities
- Configurable intensity and duration

### ✅ Arena Bounds
- Camera stays within arena limits
- Prevents camera from going off-screen
- Maintains proper framing

## Usage

### Basic Usage
The camera works automatically once set up. No additional code needed.

### Manual Control
```csharp
// Get camera controller
CameraController cam = FindObjectOfType<CameraController>();

// Trigger camera shake
cam.ShakeCamera(0.5f, 0.3f);

// Refresh player detection
cam.RefreshPlayers();

// Set specific players
cam.SetPlayers(player1, player2);
```

### Camera Manager
```csharp
// Get camera manager
CameraManager camMgr = FindObjectOfType<CameraManager>();

// Trigger ultimate shake
camMgr.OnUltimateUsed();

// Set arena bounds
camMgr.SetArenaBounds(-15, 15, 8, 2);
```

## Debug Features

### Visual Debugging
- **Yellow box**: Camera bounds
- **Cyan line**: Ideal distance range
- **Green sphere**: Current camera position
- **Red sphere**: Player midpoint
- **White line**: Line between players

### Debug Info
Enable `Show Debug Info` in CameraController to see:
- Player detection status
- Midpoint calculations
- Distance measurements

## Compatibility

### ✅ Offline Mode
- Works with AI matches
- Finds both human and AI players
- Maintains proper framing

### ✅ Online Mode
- Works with PUN2 multiplayer
- Each client has their own camera
- Synchronized with match state

### ✅ Character Selection
- Camera adapts to different character sizes
- Works with all character types
- Maintains competitive view

## Troubleshooting

### Camera Not Following Players
1. Check if CameraController is on the main camera
2. Verify players have PlayerCharacter component
3. Enable debug info to see detection status

### Jerky Movement
1. Increase `Position Smoothing` value
2. Decrease `Follow Speed` value
3. Check for conflicting camera scripts

### Camera Out of Bounds
1. Adjust `Left/Right Bound` values
2. Check arena size matches bounds
3. Verify `Enable Bounds` is checked

## Performance Notes

- **Optimized for 60fps** - smooth movement
- **Minimal overhead** - only updates in LateUpdate
- **Efficient player detection** - cached references
- **No unnecessary calculations** - only when needed

The camera system is designed to be lightweight and efficient while providing a professional fighting game experience.
