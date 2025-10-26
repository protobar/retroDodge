# Ultimate Cinematic System - Fighting Game Style

## Overview
A **simple, automatic cinematic** when you activate your ultimate - inspired by Street Fighter and King of Fighters super moves!

---

## How It Works

```
Player presses Q (with ball, HOLD IT)
  ↓
Camera zooms in with Z depth (0.3s) ← LOCAL ONLY, moves closer!
  ↓
Dramatic pose animation plays + VFX + sound
  ↓
Camera holds zoomed (1.5s default - matches animation)
  ↓
Camera zooms back out (0.4s)
  ↓
Ball is charged and ready (isUltimateReady = true)
  ↓
[Player still holding Q key]
  ↓
Player releases Q button
  ↓
Ultimate throw executes (PowerThrow/MultiThrow/Curveball)
  ↓
Done!
```

**Key Points:**
- ✅ Camera zoom includes Z-axis depth (camera moves closer, not just orthographic size)
- ✅ Press and HOLD Q during the cinematic
- ✅ Release Q after camera returns to throw the ball
- ✅ Simple hold-and-release mechanic

---

## Inspector Controls

Open your player prefab → Find **"Ultimate Camera Settings - Adjustable"**:

### 1. **Ultimate Camera Zoom Amount** (0.2-1.0)
- Default: `0.6`
- Lower = closer zoom
- **Try: `0.4` for Street Fighter style close-up!**

### 2. **Ultimate Camera Offset** (Vector2)
- Default: `X=0, Y=2`
- X = horizontal shift
- Y = vertical focus height

### 3. **Ultimate Zoom In Duration** (0.1-1.0s)
- Default: `0.3`
- How fast camera zooms in
- **Try: `0.25` for snappier feel!**

### 4. **Ultimate Zoom Out Duration** (0.1-1.0s)
- Default: `0.4`
- How fast camera zooms out
- **Try: `0.3` for faster return!**

### 5. **Ultimate Camera Hold Duration** (0.5-3.0s)
- Default: `1.5`
- **How long to hold the zoom (match your animation length!)**
- Adjust this to match your dramatic pose animation duration

### 6. **Ultimate Camera Z Depth** (-10 to 0) ← **NEW!**
- Default: `-3`
- **Camera Z-axis movement (negative = moves camera closer)**
- `-3` = moderate depth
- `-5` = closer (more dramatic)
- `-1` = subtle depth
- This gives true 3D zoom feeling!

### 7. **Use Easing For Zoom** (Checkbox)
- Default: `✅ Enabled`
- Smooth ease-in-out camera movement
- Keep enabled for professional feel!

---

## Perfect Timing Setup

**To match your animation perfectly:**

1. **Play your ultimate animation** in Unity
2. **Note the duration** (e.g., 1.2 seconds)
3. **Set Ultimate Camera Hold Duration** to match (1.2s)
4. **Adjust Zoom In/Out times** if needed

**Example for 1.2s animation:**
```
Zoom In Duration: 0.3s
Camera Hold Duration: 1.2s  ← Match animation!
Zoom Out Duration: 0.3s
Total: ~1.8s cinematic moment
```

---

## Network Behavior

### Multiplayer (Online)
- **You activate ultimate:**
  - YOUR camera zooms ✅
  - Opponent's camera stays normal ✅
  - Opponent sees your animation ✅

### Offline/AI
- **You activate ultimate:**
  - YOUR camera zooms ✅
- **AI activates ultimate:**
  - No camera zoom (AI doesn't need cinematic) ✅

---

## What Changed from Previous System

### ✅ Added (Latest Update)
- **Z-axis camera depth** - Camera physically moves closer (not just orthographic size)
- **Q release detection** - Release Q after cinematic to throw the ball
- **Ready state tracking** - `isUltimateReady` flag set after cinematic
- **7th inspector parameter** - Ultimate Camera Z Depth control

### Core System
- Press and HOLD Q → Cinematic plays
- Camera zooms with true Z depth
- After cinematic, ball is ready
- Release Q → Ultimate throw executes
- Simple hold-and-release mechanic

---

## Technical Details

### Files Modified
**PlayerCharacter.cs:**
- Removed charge state variables
- Simplified `ActivateUltimate()` method
- Updated `UltimateCameraZoom()` to use fixed hold duration
- Added `ultimateCameraHoldDuration` parameter

**Lines Changed:**
- 73-74: Removed charge state fields
- 92-93: Added camera hold duration parameter
- 494-496: Simplified input handling (no release detection)
- 1046-1122: Updated camera coroutine (fixed duration hold)
- 1128-1155: Simplified activation method

### Camera Coroutine Flow
```csharp
1. Zoom IN (ultimateZoomInDuration)
2. Hold zoomed (ultimateCameraHoldDuration) ← NEW: Fixed duration
3. Zoom OUT (ultimateZoomOutDuration)
```

---

## Example Configurations

### **Street Fighter Style** (Quick & Punchy)
```
Zoom Amount: 0.4
Camera Offset: (0, 1.5)
Z Depth: -4
Zoom In: 0.2s
Hold: 1.0s
Zoom Out: 0.3s
Easing: ✅
```

### **King of Fighters Style** (Dramatic)
```
Zoom Amount: 0.5
Camera Offset: (0, 2)
Z Depth: -5
Zoom In: 0.3s
Hold: 1.5s
Zoom Out: 0.4s
Easing: ✅
```

### **Fast Action** (Snappy)
```
Zoom Amount: 0.6
Camera Offset: (0, 2)
Z Depth: -3
Zoom In: 0.2s
Hold: 0.8s
Zoom Out: 0.2s
Easing: ✅
```

---

## Testing Checklist

### Single Player
- [ ] Press and HOLD Q with ball → Camera zooms in with Z depth
- [ ] Animation plays during zoom
- [ ] Camera holds for animation duration
- [ ] Camera returns to normal
- [ ] Ball is charged (ready state)
- [ ] Release Q → Ball throws with ultimate power
- [ ] Test early release (release Q during cinematic)
- [ ] Test late release (hold Q after cinematic ends)

### Multiplayer
- [ ] P1 activates → Only P1's camera zooms
- [ ] P2 activates → Only P2's camera zooms
- [ ] Both see opponent's animations
- [ ] No lag or interference
- [ ] Q release works correctly for both players

### Offline/AI
- [ ] Human ultimate → Camera zooms
- [ ] AI ultimate → No camera zoom
- [ ] Q release throws ball correctly

---

## Troubleshooting

### Camera returns too fast/slow
→ Adjust **Ultimate Camera Hold Duration** to match your animation length

### Want camera to zoom closer
→ Lower **Ultimate Camera Zoom Amount** (try 0.4)

### Zoom feels choppy
→ Enable **Use Easing For Zoom** ✅

### Animation plays but no camera zoom
→ Check if you're the local player (works offline and for your character in multiplayer)

---

## Code Reference

### Activation Method
```1128:1155:Assets/Scripts/Characters/PlayerCharacter.cs
void ActivateUltimate()
{
    if (!hasBall) return; // Can't use ultimate without ball
    
    abilityCharges[0] = 0f;
    StartCoroutine(AbilityCooldown(0));
    
    // Animation (dramatic pose)
    animationController?.TriggerUltimate();
    
    // FIXED: Only sync in online mode, not offline
    if (!PhotonNetwork.OfflineMode && photonView.IsMine)
    {
        photonView.RPC("SyncPlayerAction", RpcTarget.Others, "Ultimate");
    }

    // NEW: Start camera zoom (local only - for cinematic moment)
    if (ultimateCameraZoomCoroutine != null)
    {
        StopCoroutine(ultimateCameraZoomCoroutine);
    }
    ultimateCameraZoomCoroutine = StartCoroutine(UltimateCameraZoom());

    SpawnUltimateVFX();
    
    // After camera returns, player just has the charged ball ready to throw
    // They will throw it using normal throw button/animation
}
```

### Inspector Parameters
```76:99:Assets/Scripts/Characters/PlayerCharacter.cs
[Header("Ultimate Camera Settings - Adjustable")]
[SerializeField] [Range(0.2f, 1.0f)] [Tooltip("Camera zoom amount (lower = closer)")]
private float ultimateCameraZoomAmount = 0.6f;

[SerializeField] [Tooltip("Camera offset from player (X=left/right, Y=up/down)")]
private Vector2 ultimateCameraOffset = new Vector2(0f, 2f);

[SerializeField] [Range(0.1f, 1.0f)] [Tooltip("Speed of camera zoom IN")]
private float ultimateZoomInDuration = 0.3f;

[SerializeField] [Range(0.1f, 1.0f)] [Tooltip("Speed of camera zoom OUT")]
private float ultimateZoomOutDuration = 0.4f;

[SerializeField] [Tooltip("Enable smooth easing for camera movement")]
private bool useEasingForZoom = true;

[SerializeField] [Range(0.5f, 3.0f)] [Tooltip("How long to hold camera zoom (should match activation animation)")]
private float ultimateCameraHoldDuration = 1.5f;

[SerializeField] [Range(-10f, 0f)] [Tooltip("Z-axis depth change (negative = move camera closer)")]
private float ultimateCameraZDepth = -3f;

// State tracking
private bool isUltimateReady = false; // Ball is charged and ready to throw
```

### Q Release Detection
```500:510:Assets/Scripts/Characters/PlayerCharacter.cs
if (inputHandler.GetUltimatePressed() && CanUseAbility(0)) ActivateUltimate();

// Check for Q release to throw ultimate ball
if (isUltimateReady && inputHandler.GetUltimateReleased())
{
    ThrowUltimateBall();
}
```

### Throw Method
```1177:1190:Assets/Scripts/Characters/PlayerCharacter.cs
void ThrowUltimateBall()
{
    if (!isUltimateReady) return;
    
    isUltimateReady = false;
    
    // Execute the ultimate throw based on type
    switch (characterData.ultimateType)
    {
        case UltimateType.PowerThrow: ExecutePowerThrow(); break;
        case UltimateType.MultiThrow: StartCoroutine(ExecuteMultiThrow()); break;
        case UltimateType.Curveball: ExecuteCurveball(); break;
    }
}
```

---

## Summary

**What you get:**
- ✅ **Hold Q** → Cinematic plays automatically
- ✅ **Release Q** → Ball throws with ultimate power
- ✅ **Z-depth zoom** → Camera physically moves closer
- ✅ **7 inspector controls** → Full customization
- ✅ **Fighting game feel** → Street Fighter/KOF style

**Key Features:**
- Camera zooms in with true 3D depth
- Dramatic pose animation during zoom
- Simple hold-and-release mechanic
- Local only (no network lag)
- Smooth easing for professional feel

**Perfect for:**
- Players who want satisfying ultimate activation
- Fighting game style super moves
- Cinematic moments without complexity
- Easy to understand controls

**Go test it! Hold Q to activate, release Q to throw! 🥊**

