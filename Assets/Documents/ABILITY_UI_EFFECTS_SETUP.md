# 🎨 ABILITY UI EFFECTS - SETUP GUIDE

## **Simple Trick & Treat UI Effect System**

This system shows PNG images on screen when abilities are used:
- **Trick effects** → Show on **opponent's screen**
- **Treat effects** → Show on **your own screen**

Works in both **online (PUN2)** and **offline** modes.

---

## **📦 WHAT WAS ADDED**

1. **`AbilityUIEffectManager.cs`** - Manages UI effect display
2. **CharacterData.cs** - Added UI sprite fields for trick/treat
3. **PlayerCharacter.cs** - Integrated UI effect calls

---

## **🎮 UNITY SETUP (IN GAMEPLAY SCENE)**

### **Step 1: Create UI Canvas (if you don't have one)**

1. In your **Gameplay Scene** (where matches happen)
2. Right-click in Hierarchy → UI → Canvas
3. Name it "AbilityEffectsCanvas"
4. Set Canvas settings:
   - Render Mode: **Screen Space - Overlay**
   - UI Scale Mode: **Scale With Screen Size**
   - Reference Resolution: **1920x1080**

### **Step 2: Create Trick Effect Image**

1. Right-click on your Canvas → UI → Image
2. Name it **"TrickEffectImage"**
3. Set RectTransform:
   - Anchor: **Center**
   - Pos X: 0, Pos Y: 0
   - Width: **800** (or your preference)
   - Height: **800** (or your preference)
4. Set Image component:
   - Source Image: **Leave empty** (will be set dynamically)
   - Color: **White** (RGB: 255, 255, 255, Alpha: 255)
   - Preserve Aspect: **✓ Checked**
5. **Deactivate** the GameObject (uncheck in Inspector)

### **Step 3: Create Treat Effect Image**

1. Right-click on your Canvas → UI → Image
2. Name it **"TreatEffectImage"**
3. Set RectTransform (same as above):
   - Anchor: **Center**
   - Pos X: 0, Pos Y: 0
   - Width: **800**
   - Height: **800**
4. Set Image component:
   - Source Image: **Leave empty**
   - Color: **White**
   - Preserve Aspect: **✓ Checked**
5. **Deactivate** the GameObject

### **Step 4: Add AbilityUIEffectManager**

1. Right-click in Hierarchy → Create Empty
2. Name it **"AbilityUIEffectManager"**
3. Add Component → **Ability UI Effect Manager** (script)
4. In the Inspector:
   - **Trick Effect Image**: Drag your TrickEffectImage here
   - **Treat Effect Image**: Drag your TreatEffectImage here
   - **Effect Duration**: 3 (default, can adjust)
   - **Fade In Duration**: 0.3
   - **Fade Out Duration**: 0.5
   - **Debug Mode**: ✓ (check for testing, uncheck later)

---

## **🎨 ASSIGN PNG SPRITES TO CHARACTER DATA**

For **each character** in your game:

1. Find the character's ScriptableObject in Project:
   - Example: `Assets/ScriptableObjects/Characters/Grudge.asset`

2. Open it in Inspector

3. Scroll to **"Trick UI Effect - Shows on Opponent Screen"**
   - Drag your Trick PNG here (e.g., `InstantDamageEffect.png`)
   - This image will show on the **opponent's screen**

4. Scroll to **"Treat UI Effect - Shows on Self Screen"**
   - Drag your Treat PNG here (e.g., `ShieldEffect.png`)
   - This image will show on **your own screen**

### **Example Setup:**

```
Grudge Character:
├── Trick Type: Instant Damage
├── Trick UI Effect: InstantDamageIcon.png ← Shows on opponent
├── Treat Type: Shield
└── Treat UI Effect: ShieldIcon.png ← Shows on self

Phantom Character:
├── Trick Type: Freeze
├── Trick UI Effect: FreezeIcon.png ← Shows on opponent
├── Treat Type: Teleport
└── Treat UI Effect: TeleportIcon.png ← Shows on self
```

---

## **🧪 TESTING**

### **Test 1: In-Game Testing**

1. Start a match (offline or online)
2. Charge up your Trick ability
3. Press Trick button (default: E)
4. **Expected:** Trick UI image appears on screen (fades in, holds, fades out)
5. Charge up Treat ability
6. Press Treat button (default: R)
7. **Expected:** Treat UI image appears on screen

### **Test 2: Online Testing**

1. Start online match with 2 players
2. Player 1 uses Trick
3. **Expected:** Player 2 sees the trick effect on their screen
4. Player 2 uses Treat
5. **Expected:** Player 2 sees treat effect on their own screen (Player 1 doesn't see it)

### **Test 3: Offline/AI Testing**

1. Start AI match
2. **You use Trick on AI**
3. **Expected:** No effect (AI would see it, but we don't show AI screen)
4. **AI uses Trick on you**
5. **Expected:** Effect shows on your screen (you are the victim)
6. **You use Treat**
7. **Expected:** Effect shows on your screen (you used it)
8. **AI uses Treat**
9. **Expected:** No effect (AI used it, not you)

---

## **⚙️ CUSTOMIZATION**

### **Adjust Effect Duration:**

In **AbilityUIEffectManager** Inspector:
- **Effect Duration**: How long effect stays on screen (default: 3s)
- **Fade In Duration**: How fast it fades in (default: 0.3s)
- **Fade Out Duration**: How fast it fades out (default: 0.5s)

### **Adjust Effect Size:**

Select **TrickEffectImage** or **TreatEffectImage**:
- Change **Width** and **Height** in RectTransform
- Recommended: 600-1000 pixels

### **Adjust Effect Position:**

By default, effects show in **center of screen**.

To change position:
1. Select TrickEffectImage or TreatEffectImage
2. Change **Anchor** preset (top, bottom, corners, etc.)
3. Adjust **Pos X** and **Pos Y**

---

## **🎯 HOW IT WORKS**

### **Trick Flow (Shows on Opponent):**

```
Player 1 presses Trick
    ↓
PlayerCharacter.ActivateTrick()
    ↓
ShowTrickUIEffectOnOpponent()
    ↓
[ONLINE MODE]                      [OFFLINE MODE]
Send RPC to opponent          →    Check if victim is human player
    ↓                                   ↓
Opponent receives RPC              If victim = human & attacker = AI
    ↓                                   ↓
ShowTrickUIEffectRPC()              Show effect (you're the victim!)
    ↓                                   ↓
AbilityUIEffectManager.ShowTrickEffect()
    ↓
Effect appears on VICTIM's screen only!
```

### **Treat Flow (Shows on Self):**

```
Player presses Treat
    ↓
PlayerCharacter.ActivateTreat()
    ↓
ShowTreatUIEffectOnSelf()
    ↓
[ONLINE MODE]                      [OFFLINE MODE]
If this is my character       →    Check if user is human player
    ↓                                   ↓
Show effect locally               If user = human (not AI)
    ↓                                   ↓
AbilityUIEffectManager.ShowTreatEffect()
    ↓
Effect appears on USER's screen only!
```

---

## **📋 CHECKLIST**

Before testing, make sure:

- [ ] AbilityUIEffectManager GameObject exists in scene
- [ ] TrickEffectImage exists and is assigned to manager
- [ ] TreatEffectImage exists and is assigned to manager
- [ ] Both images are initially **deactivated**
- [ ] Both images have **white color** (not transparent)
- [ ] PNG sprites are assigned to **CharacterData** (trick + treat)
- [ ] Canvas is set to **Screen Space - Overlay**
- [ ] Debug mode is **enabled** for testing

---

## **🐛 TROUBLESHOOTING**

### **Issue: Effect doesn't show**

**Solution:**
1. Check Console for warnings
2. Verify PNG sprites are assigned in CharacterData
3. Check AbilityUIEffectManager has images assigned
4. Make sure images are child of Canvas
5. Enable Debug Mode to see logs

### **Issue: Effect shows but immediately disappears**

**Solution:**
1. Check Effect Duration is > 0
2. Verify Fade In/Out durations are reasonable
3. Check images aren't being hidden by other UI

### **Issue: Effect shows on wrong player in online mode**

**Solution:**
1. Verify you're using latest code
2. Check that RPC is being sent correctly
3. Enable Debug Mode and check logs
4. Ensure both players have AbilityUIEffectManager in scene

### **Issue: AI's treat effect shows on my screen (offline mode)**

**Solution:**
This is now FIXED! The code checks:
- Trick effects: Only show if YOU are the victim (not the attacker)
- Treat effects: Only show if YOU are the user (not AI)

If you still see AI's effects:
1. Make sure you have the latest PlayerCharacter.cs code
2. Check Debug logs to see which character is triggering effects
3. Verify AI has AIControllerBrain component attached

### **Issue: Effect is too small/big**

**Solution:**
1. Adjust Width/Height in TrickEffectImage/TreatEffectImage
2. Make sure **Preserve Aspect** is checked
3. Adjust PNG resolution if needed

### **Issue: Effect doesn't fade properly**

**Solution:**
1. Check Fade In/Out durations aren't 0
2. Verify Image color alpha starts at 255 (not 0)
3. Make sure images aren't marked as **RaycastTarget** (can uncheck)

---

## **🎨 RECOMMENDED SETTINGS**

### **For Subtle Effects:**
- Width/Height: 400-600
- Effect Duration: 2-3s
- Fade In: 0.2s
- Fade Out: 0.3s

### **For Prominent Effects:**
- Width/Height: 800-1000
- Effect Duration: 3-4s
- Fade In: 0.3s
- Fade Out: 0.5s

### **For Quick Flash Effects:**
- Width/Height: 600-800
- Effect Duration: 1-2s
- Fade In: 0.1s
- Fade Out: 0.2s

---

## **📝 EXAMPLE: GRUDGE CHARACTER SETUP**

1. **Find Grudge.asset** (or your character SO)

2. **Assign Trick UI Effect:**
   - Scroll to "Trick UI Effect - Shows on Opponent Screen"
   - Drag **InstantDamageEffect.png** here
   - This shows when Grudge uses Instant Damage trick

3. **Assign Treat UI Effect:**
   - Scroll to "Treat UI Effect - Shows on Self Screen"
   - Drag **ShieldEffect.png** here
   - This shows when Grudge uses Shield treat

4. **Test:**
   - Start match as Grudge
   - Use Shield (R) → Shield icon appears on your screen
   - Use Instant Damage (E) → Damage icon appears on opponent's screen

---

## **✨ THAT'S IT!**

Your trick and treat abilities now have visual UI feedback!

**Key Points:**
- ✅ Trick effects show on **opponent's screen**
- ✅ Treat effects show on **your screen**
- ✅ Works in **online and offline** modes
- ✅ Duration matches ability duration
- ✅ Smooth fade in/out
- ✅ Network-synced via PUN2

---

**Need help?** Check the troubleshooting section or enable Debug Mode to see what's happening.

Good luck! 🎮

