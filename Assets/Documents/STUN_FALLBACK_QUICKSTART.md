# Stun & Fallback - Quick Start Guide ⚡

## What Was Added

### 1. Stun System ⭐
- **What:** Player freezes after 3+ consecutive hits
- **Duration:** 3 seconds (or until hit again)
- **Effect:** Can't move, use abilities, catch, or throw
- **VFX:** Spawns stun effect above player's head (stars/dizzy)
- **✨ NEW:** Stun breaks immediately if player is hit again (fighting game standard!)

### 2. Ultimate Fallback 🎯
- **What:** Player falls to ground when hit by ultimate
- **Duration:** ~2.3 seconds (fall → ground → get up)
- **Effect:** Can't move, act, catch, or throw during sequence

---

## Animation Setup (REQUIRED)

### Step 1: Add Animator Parameters

**Open your Character Animator and add:**

| Name | Type | Purpose |
|------|------|---------|
| `IsStunned` | Bool | True while stunned |
| `IsFallen` | Bool | True while fallen |
| `Stun` | Trigger | Triggers stun animation |
| `Fall` | Trigger | Triggers fall animation |
| `GetUp` | Trigger | Triggers get up animation |

### Step 2: Create Animations

**You need 4 animations:**
1. **Stun** - Player frozen/dazed (looping, 3s)
2. **Fall** - Player falling to ground (0.5s)
3. **Ground** - Player on ground (looping)
4. **GetUp** - Player getting back up (0.8s)

### Step 3: Setup Transitions

**Stun:**
```
Any State → Stun Animation
  Conditions: Stun (trigger) + IsStunned (true)
  
Stun Animation → Idle
  Conditions: IsStunned (false)
```

**Fallback:**
```
Any State → Fall Animation
  Conditions: Fall (trigger) + IsFallen (true)
  
Fall Animation → Ground Animation
  Exit Time: 100%
  
Ground Animation → GetUp Animation
  Conditions: GetUp (trigger)
  
GetUp Animation → Idle
  Conditions: IsFallen (false)
```

---

## Testing

### Test Stun:
1. Start game
2. Get hit 3 times rapidly (within 2 seconds)
3. Player should freeze for 3 seconds

**Console should show:**
```
[STUN] Player1 consecutive hits: 1/3
[STUN] Player1 consecutive hits: 2/3
[STUN] Player1 consecutive hits: 3/3
[STUN] Player1 stunned after 3 consecutive hits!
```

### Test Fallback:
1. Charge ultimate
2. Hit opponent with ultimate
3. Opponent should fall → stay down → get up

**Console should show:**
```
[FALLBACK] Player1 hit by ultimate - triggering fallback!
[FALLBACK] Player1 falling...
[FALLBACK] Player1 on ground for 1s
[FALLBACK] Player1 getting up...
[FALLBACK] Player1 back to normal!
```

---

## Tuning (Inspector Settings)

### In PlayerCharacter Component:

**Stun Settings:**
- `Hits To Stun`: 3 (how many hits to stun)
- `Stun Duration`: 3s (how long stunned)
- `Consecutive Hit Window`: 2s (time window for combos)

**Ultimate Fallback Settings:**
- `Fall Animation Duration`: 0.5s
- `Ground Duration`: 1s
- `Get Up Animation Duration`: 0.8s

---

## Stun VFX Setup

### In CharacterData (Each Character):

1. **Assign Stun Effect Prefab:**
   - Find your stun VFX prefab (stars, dizzy effect, etc.)
   - Drag into `Stun Effect VFX` field

2. **Set Stun VFX Offset:**
   - Default: `(0, 2.0, 0)` - 2 units above player
   - Adjust X, Y, Z to position effect (usually above head)

**Example Values:**
```
Stun Effect VFX: StarsDizzyEffect.prefab
Stun VFX Offset: 
  X: 0    (centered)
  Y: 2.0  (above head)
  Z: 0    (centered)
```

---

## How It Works

### Stun Flow:
```
Hit 1 → Hit 2 → Hit 3 (within 2s) → STUN! → Freeze 3s → Recover
```

### Fallback Flow:
```
Ultimate Hit → Fall (0.5s) → Ground (1s) → Get Up (0.8s) → Recover
```

---

## Code Reference

### Animation Calls

```csharp
// Stun
animationController.SetStunned(true);
animationController.TriggerStun();
// ... wait 3 seconds
animationController.SetStunned(false);

// Fallback
animationController.SetFallen(true);
animationController.TriggerFall();
// ... wait
animationController.TriggerGetUp();
// ... wait
animationController.SetFallen(false);
```

### Damage Integration

```csharp
// In your damage code:
playerCharacter.OnDamageTaken(damage, isUltimateHit: true);
```

---

## Troubleshooting

### Stun not working:
- ✓ Check: Are you hitting 3 times within 2 seconds?
- ✓ Check: Is stun animation added to Animator?
- ✓ Check: Are `IsStunned` and `Stun` parameters added?

### Fallback not working:
- ✓ Check: Are you hitting with an **ultimate** (not normal throw)?
- ✓ Check: Are all 3 animations added (Fall, Ground, GetUp)?
- ✓ Check: Are all parameters added (IsFallen, Fall, GetUp)?

### Player stuck:
- Press R key (if debug enabled) for emergency reset
- Check console for errors in coroutines

---

## Files Modified

✅ `PlayerAnimationController.cs` - Animation triggers  
✅ `PlayerCharacter.cs` - Stun/fallback logic  
✅ `PlayerHealth.cs` - Ultimate hit tracking  
✅ `CollisionDamageSystem.cs` - Pass ultimate info  

---

## ✅ Implementation Checklist

- [ ] Add 5 animator parameters (IsStunned, IsFallen, Stun, Fall, GetUp)
- [ ] Create 4 animations (Stun, Fall, Ground, GetUp)
- [ ] Setup animator transitions
- [ ] Test stun (3 rapid hits)
- [ ] Test fallback (ultimate hit)
- [ ] Tune settings in inspector
- [ ] Add VFX/SFX (optional)

---

## Quick Stats

| Feature | Trigger | Duration | Effect |
|---------|---------|----------|--------|
| **Stun** | 3 hits in 2s | 3s | Freeze |
| **Fallback** | Ultimate hit | ~2.3s | Fall sequence |

---

**For full documentation, see:** `STUN_AND_FALLBACK_SYSTEMS.md`

