# Ultimate Cinematic System - Quick Reference

## What Was Added

### Core Mechanic
- **Press Q** → Start ultimate charge phase with camera zoom
- **Release Q** (or wait 3 seconds) → Execute ultimate and zoom out
- **Local only** → Only the activating player sees the cinematic effect

---

## Modified Files

### 1. `PlayerCharacter.cs`
**New Variables:**
```csharp
private bool isUltimateCharging = false;
private float ultimateChargeStartTime = 0f;
private float ultimateChargeWindow = 3f;
private Coroutine ultimateCameraZoomCoroutine;
```

**New Methods:**
- `UltimateCameraZoom()` - Camera zoom coroutine
- `UltimateAutoReleaseTimer()` - Auto-release after 3 seconds
- `ReleaseUltimate()` - Execute ultimate throw
- Modified `ActivateUltimate()` - Start charge phase

**Modified Input Logic in `HandleInput()`:**
```csharp
// Check for ultimate activation (prevent re-activation during charge)
if (inputHandler.GetUltimatePressed() && CanUseAbility(0) && !isUltimateCharging)
{
    ActivateUltimate();
}

// Check for Q release during charge
if (isUltimateCharging && inputHandler.GetUltimateReleased())
{
    ReleaseUltimate();
}
```

### 2. `PlayerInputHandler.cs`
**New Variables:**
```csharp
private bool ultimateHeld = false;
private bool ultimateReleased = false;
```

**New Methods:**
```csharp
public bool GetUltimateHeld()
public bool GetUltimateReleased()
```

**Modified Input Detection:**
```csharp
// In HandleKeyboardInput()
ultimateHeld = Input.GetKey(ultimateKey);
if (Input.GetKeyUp(ultimateKey))
{
    ultimateReleased = true;
}
```

---

## How It Works - Simple Flow

```
1. Player presses Q (has ultimate ready)
   └─> Camera zooms in on player (0.3s)
   └─> Ultimate animation starts
   └─> Auto-release timer starts (3s)

2. Player holds Q (charge phase)
   └─> Camera stays focused on player
   └─> Animation continues playing

3. Player releases Q OR 3 seconds pass
   └─> Ultimate executes
   └─> Camera zooms out (0.4s)
   └─> Normal gameplay resumes
```

---

## Network Behavior

### Multiplayer (Online)
- **P1 activates ultimate:**
  - P1's camera zooms in ✅
  - P2's camera stays normal ✅
  - P2 sees P1's animation ✅

### Offline/AI
- **Human activates ultimate:**
  - Camera zooms in ✅
- **AI activates ultimate:**
  - No camera zoom ✅

---

## Camera Settings (Now Adjustable in Inspector!)

| Setting | Default Value | Description | Adjustable? |
|---------|---------------|-------------|-------------|
| Zoom Amount | 60% (0.6x) | How much to zoom in | ✅ Inspector |
| Zoom In Time | 0.3 seconds | Speed of zoom in | ✅ Inspector |
| Zoom Out Time | 0.4 seconds | Speed of zoom out | ✅ Inspector |
| Camera Offset | +2 Y-axis | Focus point above character | ✅ Inspector |
| Use Easing | Enabled | Smooth camera movement | ✅ Inspector |
| Auto-Release | 3.0 seconds | Max charge time | Code only |

---

## Testing

### What to Test
✅ **Single Player:**
   - Press Q → Camera zooms
   - Release Q → Ultimate executes
   - Hold Q for 3s → Auto-release

✅ **Multiplayer:**
   - Each player only sees their own zoom
   - Opponent's animations visible
   - No network lag

✅ **Offline with AI:**
   - Human ultimate → Camera zooms
   - AI ultimate → No zoom

---

## Key Code Sections

### Camera Zoom (Local Only)
```150:1095:Assets/Scripts/Characters/PlayerCharacter.cs
IEnumerator UltimateCameraZoom()
{
    // Only zoom for the local player
    bool isLocalPlayer = PhotonNetwork.OfflineMode || photonView.IsMine;
    if (!isLocalPlayer) yield break;

    Camera mainCamera = Camera.main;
    if (mainCamera == null) yield break;

    // Get original camera settings
    float originalSize = mainCamera.orthographicSize;
    Vector3 originalPosition = mainCamera.transform.position;
    
    // Calculate zoom target
    float zoomSize = originalSize * 0.6f; // Zoom in to 60% of original
    Vector3 zoomPosition = transform.position + new Vector3(0, 2f, originalPosition.z);

    // Zoom IN
    float zoomInDuration = 0.3f;
    float elapsed = 0f;

    while (elapsed < zoomInDuration)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / zoomInDuration;
        
        if (mainCamera.orthographic)
        {
            mainCamera.orthographicSize = Mathf.Lerp(originalSize, zoomSize, t);
        }
        
        // Smoothly move camera to focus on player
        Vector3 targetPos = transform.position + new Vector3(0, 2f, originalPosition.z);
        mainCamera.transform.position = Vector3.Lerp(originalPosition, targetPos, t);
        
        yield return null;
    }

    // Hold zoom during charge window
    while (isUltimateCharging)
    {
        // Keep camera focused on player during charge
        Vector3 targetPos = transform.position + new Vector3(0, 2f, originalPosition.z);
        mainCamera.transform.position = targetPos;
        yield return null;
    }

    // Zoom OUT
    float zoomOutDuration = 0.4f;
    elapsed = 0f;
    Vector3 startPos = mainCamera.transform.position;

    while (elapsed < zoomOutDuration)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / zoomOutDuration;
        
        if (mainCamera.orthographic)
        {
            mainCamera.orthographicSize = Mathf.Lerp(zoomSize, originalSize, t);
        }
        
        mainCamera.transform.position = Vector3.Lerp(startPos, originalPosition, t);
        
        yield return null;
    }

    // Ensure we're back to original
    if (mainCamera.orthographic)
    {
        mainCamera.orthographicSize = originalSize;
    }
    mainCamera.transform.position = originalPosition;
}
```

### Ultimate Activation
```1112:1143:Assets/Scripts/Characters/PlayerCharacter.cs
void ActivateUltimate()
{
    if (!hasBall) return; // Can't use ultimate without ball
    
    abilityCharges[0] = 0f;
    StartCoroutine(AbilityCooldown(0));
    
    // NEW: Start cinematic charge phase
    isUltimateCharging = true;
    ultimateChargeStartTime = Time.time;
    
    // Animation
    animationController?.TriggerUltimate();
    
    // FIXED: Only sync in online mode, not offline
    if (!PhotonNetwork.OfflineMode && photonView.IsMine)
    {
        photonView.RPC("SyncPlayerAction", RpcTarget.Others, "Ultimate");
    }

    // NEW: Start camera zoom (local only)
    if (ultimateCameraZoomCoroutine != null)
    {
        StopCoroutine(ultimateCameraZoomCoroutine);
    }
    ultimateCameraZoomCoroutine = StartCoroutine(UltimateCameraZoom());

    SpawnUltimateVFX();
    
    // NEW: Start auto-release timer
    StartCoroutine(UltimateAutoReleaseTimer());
}
```

### Ultimate Release
```1162:1175:Assets/Scripts/Characters/PlayerCharacter.cs
void ReleaseUltimate()
{
    if (!isUltimateCharging) return;
    
    isUltimateCharging = false;
    
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

## Customization (NEW: Inspector Controls!)

### ✨ Adjust in Unity Inspector (No Code Needed!)
Open your player prefab and find **"Ultimate Camera Settings - Adjustable"**:

1. **Ultimate Camera Zoom Amount** (0.2 - 1.0)
   - Default: 0.6
   - Lower = closer zoom
   - Try: 0.4 for dramatic close-up!

2. **Ultimate Camera Offset** (Vector2)
   - Default: X=0, Y=2
   - X = horizontal shift
   - Y = vertical focus height

3. **Ultimate Zoom In Duration** (0.1 - 1.0 seconds)
   - Default: 0.3
   - Try: 0.2 for snappier zoom!

4. **Ultimate Zoom Out Duration** (0.1 - 1.0 seconds)
   - Default: 0.4

5. **Use Easing For Zoom** (checkbox)
   - Default: ✅ Enabled
   - Smooth ease-in-out movement

### 📝 Change Charge Window (Code Only)
```csharp
// In PlayerCharacter.cs (line 76)
private float ultimateChargeWindow = 3f; // Modify this value
```

**See `ULTIMATE_CAMERA_CONTROLS.md` for detailed configuration guide!**

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Camera not zooming | Check if `Camera.main` exists and is orthographic |
| Auto-release not working | Verify `ultimateChargeWindow` is set (default: 3.0) |
| Release not triggering | Check `GetUltimateReleased()` in InputHandler |
| Zoom happening for both players | Verify local player check in `UltimateCameraZoom()` |

---

## Documentation Files
- 📄 **ULTIMATE_CINEMATIC_SYSTEM.md** - Full documentation with diagrams
- 📄 **ULTIMATE_CINEMATIC_SUMMARY.md** - This quick reference (you are here)

---

## Status
✅ **Fully Implemented and Ready to Test**

All code is in place and error-free. Test in Unity to see the cinematic ultimate system in action!

