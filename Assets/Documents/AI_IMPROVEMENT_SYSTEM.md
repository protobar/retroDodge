# AI Improvement System - Character-Specific Strategies 🤖

## Overview

The AI system has been completely overhauled with **character-specific strategies** similar to Echo's teleport tactics. Each character now has unique decision-making based on their abilities and playstyle.

---

## 🎮 **Character Archetypes**

### **Echo** - Teleport Specialist (Trickster) ⚡
- **Ultimate:** Curveball (unpredictable trajectory)
- **Trick:** Slow Speed
- **Treat:** Teleport
- **Playstyle:** Hit-and-run, unpredictable, evasive

### **Grudge** - Tank/Aggressive (Bruiser) 🛡️
- **Ultimate:** PowerThrow (massive damage + knockback)
- **Trick:** Instant Damage
- **Treat:** Shield
- **Playstyle:** Aggressive, tanky, pushes forward
- **AI Behavior:** 50% more aggressive than other characters

### **Nova** - Multi-Ball Specialist (Striker) 🌟
- **Ultimate:** MultiThrow (3-5 balls in spread pattern)
- **Trick:** Freeze/SlowSpeed
- **Treat:** Speed Boost
- **Playstyle:** Positioning-focused, multi-target pressure

---

## 📋 **Character-Specific AI Scenarios**

### **Echo - 3 Teleport Scenarios** (Existing ✅)

#### **Scenario 1: Teleport Dodge**
**When:** Ball is thrown at Echo
**Condition:** Ball is close (<3m) and coming toward Echo
**Strategy:** Teleport to safety instead of catching/dodging
**Activation:** Automatic when detect incoming ball threat

#### **Scenario 2: Teleport Steal**
**When:** Opponent is closer to free ball
**Condition:** 
- Ball is free on ground
- Opponent is closer to ball than Echo
- Opponent distance to ball < 6m
**Strategy:** Teleport to ball location to steal it before opponent
**Activation:** 35% chance when conditions met

#### **Scenario 3: Teleport Attack**
**When:** Echo has ball and opponent is far
**Condition:** 
- Echo has ball
- Opponent distance > 8m
**Strategy:** Surprise teleport attack to close distance, then ultimate/throw
**Activation:** 25% chance when conditions met

---

### **Grudge - 3 Shield Scenarios** (NEW! 🛡️)

#### **Scenario 1: Defensive Shield**
**When:** Under heavy pressure from opponent
**Conditions:**
- Opponent has ball and is close (<8m) and facing Grudge
- OR ball is thrown at Grudge (<5m)
**Strategy:** Activate shield to tank incoming damage
**Activation:** 
- 40% chance when opponent threat
- 50% chance when ball incoming (alternative to dodge)
**Debug Log:** `[GRUDGE AI] Defensive shield - opponent threat at Xm`

#### **Scenario 2: Aggressive Shield (Tank Push)**
**When:** Grudge has ball and wants to push forward
**Conditions:**
- Grudge has ball
- Ultimate is ready
- Opponent distance < 12m
**Strategy:** Shield + push forward + use ultimate for guaranteed hit
**Activation:** 60% chance when ultimate ready
**Additional:** 25% chance to shield at medium range (6-10m) while pushing
**Debug Log:** `[GRUDGE AI] Aggressive shield - ultimate tank push`

#### **Scenario 3: Survival Shield**
**When:** Grudge is low on health
**Conditions:**
- Health below 40%
**Strategy:** Activate shield for survival
**Activation:** 70% chance when low health
**Debug Log:** `[GRUDGE AI] Survival shield - low health (X%)`

---

### **Nova - 3 Speed Boost Scenarios** (NEW! ⚡)

#### **Scenario 1: Speed Boost Steal**
**When:** Racing opponent for free ball
**Conditions:**
- Ball is free on ground
- Opponent is closer to ball than Nova
- Nova distance to ball < 10m
**Strategy:** Speed boost to outrace opponent and steal ball
**Activation:** 45% chance when opponent closer
**Additional:** 30% chance if ball is far (6-12m) but gettable
**Debug Log:** `[NOVA AI] Speed boost - ball steal race`

#### **Scenario 2: Speed Boost Position (Multi-Ball Setup)**
**When:** Nova has ball and ultimate is ready
**Conditions:**
- Nova has ball
- Ultimate charged (100%)
- Opponent distance > 8m
**Strategy:** Speed boost to get into optimal multi-ball range (8-12m)
**Activation:** 
- 50% chance if too far (>12m)
- 35% chance if in good range (8-14m)
**Effect:** Sets `isPositioningForMultiBall = true`
**Debug Log:** `[NOVA AI] Speed boost - multi-ball positioning`

#### **Scenario 3: Speed Boost Escape**
**When:** Opponent has ball and is too close
**Conditions:**
- Opponent has ball and distance < 6m
- OR ball is thrown at Nova (<6m)
**Strategy:** Speed boost to create distance / evade
**Activation:** 
- 40% chance when opponent too close
- 30% chance when ball incoming (alternative to dodge)
**Debug Log:** `[NOVA AI] Speed boost - escape from threat`

---

## 🎯 **Enhanced Duck System**

### **Improvements Made:**

#### **1. Intelligent Height Detection**
- AI now checks ball height relative to character
- Only ducks when ball is at right height (0.5m - 2.5m above ground)
- Higher duck chance (1.2x) when ball is at perfect height
- Lower duck chance (0.4x) when ball is too low or too high

#### **2. Threat Distance Calculation**
- Checks opponent distance when holding ball
- Checks if opponent is facing AI
- More likely to duck when opponent is close (<7m) and facing AI

#### **3. Grudge-Specific Duck Behavior**
- Grudge ducks more often while holding ball (85% chance)
- Larger threat detection range (9m vs 7m)
- Fits tank/defensive playstyle

#### **4. Action Economy Integration**
- Ducking costs 0.3 action budget (cheap)
- Won't duck if action budget too low (<0.3)
- Prevents duck spam

### **Duck Usage Scenarios:**

**Evading Thrown Balls:**
```csharp
// Check ball height first
float ballHeight = ball.position.y - ourPosition.y;
bool ballIsRightHeight = ballHeight > 0.5f && ballHeight < 2.5f;

// Higher chance if right height
float duckChance = ballIsRightHeight ? 
    (dodgeProbability * 1.2f) : (dodgeProbability * 0.4f);

if (Random.value < duckChance)
{
    frame.duckHeld = true; // Duck!
}
```

**Holding Ball vs Opponent:**
```csharp
// Check opponent threat
float threatDistance = Distance(opponent, us);
bool opponentFacingUs = Dot(opponent.forward, toUs) > 0.5f;

// Duck if close and facing us
bool shouldDuck = threatDistance < 7f && opponentFacingUs && 
    Random.value < (dodgeProbability * 0.7f);

// Grudge ducks more (tank behavior)
if (isGrudge) 
    shouldDuck = threatDistance < 9f && Random.value < 0.85f;
```

---

## 🧠 **AI Decision Flow**

### **Priority System:**

1. **State Machine** (SeekBall, Evade, EngageWithBall)
2. **Character Detection** (Echo/Grudge/Nova)
3. **Basic Actions** (Move, Jump, Dash, Duck)
4. **Abilities** (Ultimate, Trick)
5. **Character-Specific Treats** (Teleport, Shield, Speed Boost)

### **Ability Usage Flow:**

```
Every Frame:
  ↓
Update Action Budget (restores over time)
  ↓
Think() - Every 0.1 seconds
  ↓
Check State (SeekBall/Evade/EngageWithBall)
  ↓
Build Input Frame:
  ├─ Movement (based on state)
  ├─ Jump/Dash/Duck (based on threat)
  ├─ Ultimate (8% chance when charged + hasball)
  ├─ Trick (12% chance when charged)
  └─ Treat (Character-Specific):
      ├─ Echo: Teleport (3 scenarios)
      ├─ Grudge: Shield (3 scenarios)
      └─ Nova: Speed Boost (3 scenarios)
```

---

## 📊 **AI Difficulty Scaling**

### **Threat Detection Range:**
- **Easy:** 6m
- **Normal:** 8m
- **Hard:** 10m
- **Nightmare:** 12m + prediction

### **Movement Speed Multiplier:**
- **Easy:** 0.7x
- **Normal:** 1.0x
- **Hard:** 1.2x
- **Nightmare:** 1.5x

### **Throw Decision Speed:**
- **Easy-Hard:** Normal cooldown
- **Nightmare:** 0.5x cooldown (throws twice as fast)

### **Prediction Accuracy:**
- **Easy:** 40%
- **Normal:** 60%
- **Hard:** 80%
- **Nightmare:** 95%

### **Grudge Aggression Bonus:**
- **All Difficulties:** 1.5x base aggression
- Moves toward opponent more aggressively
- Shorter reaction times
- More likely to engage

---

## 🔍 **Debug Logs**

Enable `debugMode` in AI Inspector to see logs:

### **Character Detection:**
```
[AI] Grudge_AI identified as GRUDGE - Aggressive tank strategies active
[AI] Nova_AI identified as NOVA - Multi-ball specialist strategies active
[AI] Echo_AI identified as ECHO - Teleport strategies active
```

### **Duck System:**
```
[AI] Grudge crouching to avoid ball (height: 1.2)
[AI] Nova crouching while holding ball (threat distance: 6.5)
```

### **Echo Strategies:**
```
[ECHO AI] Executed teleport strategy
```

### **Grudge Strategies:**
```
[GRUDGE AI] Defensive shield - opponent threat at 7.2m
[GRUDGE AI] Aggressive shield - ultimate tank push
[GRUDGE AI] Survival shield - low health (35%)
[GRUDGE AI] Executed shield strategy
```

### **Nova Strategies:**
```
[NOVA AI] Speed boost - ball steal race
[NOVA AI] Speed boost - multi-ball positioning
[NOVA AI] Speed boost - escape from threat
[NOVA AI] Executed speed boost strategy
```

---

## 🧪 **Testing Scenarios**

### **Test 1: Echo Teleport**
1. Play as any character vs Echo AI
2. Throw ball at Echo → Echo should sometimes teleport away
3. Drop ball far from Echo → Echo may teleport steal
4. Stay far from Echo with ball → Echo may teleport attack

### **Test 2: Grudge Tank**
1. Play vs Grudge AI
2. Get ball and face Grudge → Grudge should shield defensively
3. Let Grudge charge ultimate → Grudge should shield + push + ult
4. Damage Grudge below 40% HP → Grudge should survival shield

### **Test 3: Nova Positioning**
1. Play vs Nova AI
2. Drop ball mid-field → Nova should speed boost to steal
3. Let Nova charge ultimate → Nova should speed boost to optimal range (8-12m)
4. Get ball and chase Nova → Nova should speed boost to escape

### **Test 4: Duck System**
1. Play vs any AI
2. Throw at AI at head height → AI should duck
3. Throw at AI at ground level → AI should jump/dodge (not duck)
4. Hold ball near Grudge AI → Grudge should duck while waiting

---

## 📈 **Performance Impact**

### **Optimizations:**
- Think() only runs every 0.1 seconds (not every frame)
- Character-specific checks cached at Start()
- Action economy prevents ability spam
- Efficient state machine

### **Memory Usage:**
- ~15 new boolean/float fields per AI
- Minimal heap allocations (no lists/dictionaries in hot path)
- All calculations use value types

---

## 🔧 **Configuration**

### **Cooldowns (Per Character):**
```csharp
// Echo
private float teleportCooldown = 2.5f;

// Grudge
private float shieldCooldown = 4f;
private float aggression = 1.5f;

// Nova
private float speedBoostCooldown = 5f;
```

### **Activation Probabilities:**
Can be tuned in code:
- Echo teleport: 25-35% chance
- Grudge shield: 25-70% chance (scenario-dependent)
- Nova speed boost: 30-50% chance (scenario-dependent)

---

## ✅ **Summary**

### **What Was Added:**

**Echo:** ✅ Already had 3 teleport scenarios
**Grudge:** ✅ NEW - 3 shield scenarios (defensive, aggressive, survival)
**Nova:** ✅ NEW - 3 speed boost scenarios (steal, position, escape)
**Duck System:** ✅ Enhanced with height detection and character-specific behavior
**All Characters:** ✅ Smarter ability usage and threat assessment

### **Files Modified:**
- `Assets/Scripts/AI/AIControllerBrain.cs` - 300+ lines added

### **Benefits:**
- ✅ Each character feels unique
- ✅ AI uses abilities strategically
- ✅ Duck system is intelligent
- ✅ Scales with difficulty
- ✅ Debug logs for testing
- ✅ Well-documented and maintainable

---

## 🎮 **Play Against Improved AI!**

The AI is now much smarter and each character has a distinct personality:
- **Echo** will surprise you with teleports
- **Grudge** will tank and push aggressively
- **Nova** will position perfectly for multi-ball ults

**Enable Debug Mode to watch the AI think! 🤖**

