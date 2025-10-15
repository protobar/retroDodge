# 🎮 Character Preview System Setup Guide

## ✅ **FEATURE IMPLEMENTED:**

The NetworkCharacterSelectionManager now includes a complete character preview system that spawns character prefabs when selected, just like in Valorant!

## 🎯 **Key Features:**

### **Character Preview Spawning**
- ✅ **Instant Preview**: Character prefab spawns immediately when button is pressed
- ✅ **Auto Cleanup**: Previous character is destroyed when selecting a new one
- ✅ **Animation Support**: Characters play idle and victory animations
- ✅ **Component Safety**: Disables gameplay components to prevent issues

### **Preview Customization**
- ✅ **Position Control**: Set preview spawn position
- ✅ **Rotation Control**: Set preview spawn rotation  
- ✅ **Scale Control**: Adjust preview size
- ✅ **Animation Toggle**: Enable/disable preview animations

## 🔧 **Setup Instructions:**

### **Step 1: Inspector Setup**
1. Open the **NetworkCharacterSelectionManager** in the Inspector
2. In the **Character Preview** section, configure:
   - **Character Preview Parent**: Assign an empty GameObject as the preview spawn location
   - **Preview Position**: Set where characters spawn (e.g., 0, 0, 0)
   - **Preview Rotation**: Set character rotation (e.g., 0, 180, 0 for facing camera)
   - **Preview Scale**: Set character size (e.g., 1.0 for normal size)
   - **Enable Preview Animations**: Check to enable animations
   - **Preview Animator Controller**: Assign your custom Animator Controller

### **Step 2: Character Prefab Setup**
1. **Create Separate Preview Prefabs**: Create dedicated preview prefabs for each character with:
   - ✅ **Animator Component**: For preview animations
   - ✅ **Character Model**: 3D model with animations
   - ✅ **Animation Controller**: Set up with Idle, Victory, etc. animations
   - ❌ **NO Network Components**: No PhotonView, SmoothSync, etc.
   - ❌ **NO Gameplay Components**: No PlayerCharacter, PlayerHealth, etc.

2. **Assign Preview Prefabs**: In NetworkCharacterSelectionManager:
   - Assign your preview prefabs to the **Character Preview Prefabs** array
   - System will automatically match them to characters by name or index

### **✅ FIXED: Network Component Issue**
The error `Illegal view ID:0 method: RpcClearBuffer` has been resolved! The system now uses **separate preview prefabs** that have no network components, making them completely safe.

**How It Works:**
1. **Separate Preview Prefabs**: Uses dedicated prefabs with only Animator + Model
2. **No Network Components**: Preview prefabs have no PhotonView, SmoothSync, etc.
3. **Automatic Matching**: System matches preview prefabs to characters by name/index
4. **Fallback Support**: Falls back to gameplay prefab with component disabling if no preview prefab found
5. **Complete Network Isolation**: Preview characters are completely offline and safe

### **Step 3: Animation Setup**
1. **Create Custom Animator Controller** for preview characters:
   - **Automatic Transitions**: Set up transitions between animations
   - **Idle Animation**: Default standing animation
   - **Victory Animation**: Celebration animation
   - **Loop Animations**: Configure looping for continuous playback

2. **Animation Controller Features**:
   - **Automatic Transitions**: No need for code triggers
   - **Loop Settings**: Configure animation looping
   - **Blend Trees**: For smooth animation blending
   - **State Machine**: Complex animation state management

## 🎮 **How It Works:**

### **Character Selection Flow**
1. **Player Clicks Character Button** → `OnCharacterSelected()` called
2. **Destroy Current Preview** → `DestroyCurrentPreview()` removes old character
3. **Spawn New Preview** → `SpawnCharacterPreview()` creates new character
4. **Setup Preview** → `DisablePreviewComponents()` disables gameplay components
5. **Start Animation** → `StartPreviewAnimation()` plays idle/victory animations

### **Preview Animation Sequence**
1. **Spawn**: Character appears in preview area
2. **Idle Animation**: Plays for 1 second
3. **Victory Animation**: Plays for 2 seconds
4. **Return to Idle**: Loops back to idle animation

## 🔧 **Technical Details:**

### **Component Safety**
The system automatically handles ALL components in preview:
- ✅ **ALL SCRIPTS DISABLED**: Every MonoBehaviour script is disabled (except Animator)
- ✅ **NETWORK COMPONENTS DESTROYED**: PhotonView, SmoothSync, etc. are completely removed
- ✅ **PHYSICS DISABLED**: Colliders disabled, Rigidbodies made kinematic
- ✅ **ONLY ANIMATOR ACTIVE**: Only the Animator component remains enabled for animations
- ✅ **COMPLETE ISOLATION**: Preview characters are completely safe and lightweight

### **Preview Management**
```csharp
// Spawn preview when character selected
SpawnCharacterPreview(selectedCharacter);

// Destroy preview when changing characters
DestroyCurrentPreview();

// Update preview when character changes
OnChangeCharacterButtonClicked();
```

## 🎯 **Customization Options:**

### **Preview Position**
```csharp
[SerializeField] private Vector3 previewPosition = new Vector3(0, 0, 0);
[SerializeField] private Vector3 previewRotation = new Vector3(0, 0, 0);
[SerializeField] private float previewScale = 1f;
```

### **Animation Control**
```csharp
[SerializeField] private bool enablePreviewAnimations = true;
```

### **Debug Mode**
```csharp
[SerializeField] private bool debugMode;
// Enables detailed logging for troubleshooting
```

## 🐛 **Troubleshooting:**

### **Character Not Spawning**
1. **Check CharacterData**: Ensure `characterPrefab` is assigned
2. **Check Preview Parent**: Ensure `characterPreviewParent` is assigned
3. **Check Console**: Look for `[CHAR PREVIEW]` debug messages

### **Animations Not Playing**
1. **Check Animator**: Ensure preview prefab has Animator component
2. **Check Animation Controller**: Ensure proper triggers are set up
3. **Check Enable Preview Animations**: Ensure it's checked in Inspector

### **Performance Issues**
1. **Reduce Preview Scale**: Lower the `previewScale` value
2. **Disable Animations**: Uncheck `enablePreviewAnimations`
3. **Optimize Prefabs**: Use LOD models for preview characters

## 🎮 **Example Setup:**

### **Character Preview Parent Setup**
1. Create empty GameObject called "CharacterPreviewArea"
2. Position it where you want characters to appear
3. Assign it to `characterPreviewParent` in NetworkCharacterSelectionManager

### **Preview Prefab Setup**
1. Create prefab with character model
2. Add Animator component
3. Set up Animation Controller with Idle/Victory animations
4. Remove all gameplay components
5. Assign to CharacterData.characterPrefab

### **Animation Controller Setup**
1. Create new Animation Controller
2. Add "Idle" and "Victory" trigger parameters
3. Create transitions between animations
4. Assign to preview prefab's Animator

## 🚀 **Advanced Features:**

### **Custom Preview Animations**
- Add more animation triggers (e.g., "Dance", "Pose")
- Create character-specific preview animations
- Add particle effects for preview characters

### **Preview Interactions**
- Add hover effects when mouse over character buttons
- Add preview rotation with mouse drag
- Add zoom in/out functionality

The character preview system is now fully functional and ready to use! Characters will spawn with animations when selected, just like in Valorant.

## 🔧 **Troubleshooting:**

### **Network Component Errors**
**Error:** `Illegal view ID:0 method: RpcClearBuffer`
**Cause:** Preview characters have active network components trying to sync without valid ViewIDs
**Solution:** 
- ✅ **Complete Script Disabling**: ALL MonoBehaviour scripts are disabled (except Animator)
- ✅ **Network Component Destruction**: PhotonView, SmoothSync, etc. are completely destroyed
- ✅ **Recursive Cleanup**: Finds and destroys any remaining network components
- ✅ **Only Animator Active**: Preview characters are completely isolated and safe
- ✅ **No Separate Prefabs Needed**: Works with existing gameplay prefabs

### **Animation Issues**
**Problem:** Animations not playing
**Solution:**
- Check if `Enable Preview Animations` is enabled
- Verify Animator Controller is assigned
- Ensure animations are properly configured in the controller

### **Performance Issues**
**Problem:** Preview characters causing lag
**Solution:**
- Disable unnecessary components in preview prefabs
- Use lower-poly models for preview
- Optimize animation controllers
