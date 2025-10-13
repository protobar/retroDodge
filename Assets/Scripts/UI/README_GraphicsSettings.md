# 🎮 Graphics Settings System - Complete Setup Guide

## 🎯 **EASY SETUP - STEP BY STEP**

### **📋 What This System Does**
- **Connects your 3 buttons** (Low/Medium/High) to actual graphics changes
- **Uses your URP Quality presets** from Project Settings
- **Auto-detects hardware** and suggests best settings
- **Shows FPS counter** to see performance impact
- **Saves settings** so they persist between sessions

---

## 🔧 **SETUP INSTRUCTIONS**

### **Step 1: Add Scripts to Main Menu Scene**

1. **Find your Main Menu scene**
2. **Add GraphicsSettingsSetup script** to any GameObject (like MainMenuManager)
3. **In the inspector, assign your buttons**:
   - Drag your Performant button to `performantButton` field
   - Drag your Balanced button to `balancedButton` field  
   - Drag your High Fidelity button to `highFidelityButton` field
4. **Optional - Add Resolution Dropdown**:
   - Create a TMP_Dropdown in your UI
   - Assign it to `resolutionDropdown` field in GraphicsSettingsManager
5. **Optional - Add VSync Toggle**:
   - Create a Toggle in your UI
   - Assign it to `vSyncToggle` field in GraphicsSettingsManager

### **Step 2: Optional - Add FPS Display**

1. **Create a Text element** in your UI
2. **Assign it to `fpsCounterText`** in GraphicsSettingsSetup
3. **Check `showFPS = true`** to enable FPS counter

### **Step 3: Test the System**

1. **Press Play** in Main Menu scene
2. **Click your graphics buttons** - you should see console messages
3. **Check Project Settings > Quality** - should change when you click buttons
4. **Look for FPS counter** if you enabled it

---

## ⚙️ **WHAT EACH PRESET DOES**

### **🟢 PERFORMANT Graphics**
- **Shadows**: Short distance (50 units), 2 cascades, Low resolution
- **Anti-aliasing**: None
- **VSync**: Off (maximum FPS)
- **LOD Bias**: 0.5 (lower detail)
- **Physics**: Reduced iterations
- **Audio**: Optimized for performance
- **Target**: 60+ FPS on low-end devices

### **🟡 BALANCED Graphics** 
- **Shadows**: Medium distance (100 units), 4 cascades, Medium resolution
- **Anti-aliasing**: 2x
- **VSync**: Off
- **LOD Bias**: 1.0 (standard detail)
- **Physics**: Standard iterations
- **Audio**: Standard quality
- **Target**: 60 FPS on mid-range devices

### **🔴 HIGH FIDELITY Graphics**
- **Shadows**: Long distance (200 units), 4 cascades, High resolution
- **Anti-aliasing**: 4x
- **VSync**: On (smooth gameplay)
- **LOD Bias**: 1.5 (higher detail)
- **Physics**: Enhanced iterations
- **Audio**: High quality
- **Target**: 60 FPS on high-end devices

---

## 🎮 **HOW TO USE**

### **For Players:**
1. **Click Performant** = Maximum performance, lower quality, optimized audio
2. **Click Balanced** = Balanced performance and quality, standard audio
3. **Click High Fidelity** = Maximum quality, high-quality audio, requires good hardware

### **New Features:**
- **Resolution Dropdown**: Change screen resolution with refresh rates
- **VSync Toggle**: Enable/disable VSync for smooth gameplay
- **Button Highlighting**: Selected quality button turns green
- **Audio Quality**: Preserves audio quality (no more sound degradation)

### **Auto-Detection:**
- **Low-end devices** → Automatically suggests Low
- **High-end devices** → Automatically suggests High
- **Mid-range devices** → Stays on Medium

---

## 🔍 **TESTING & VERIFICATION**

### **Test Each Preset:**
1. **Click Low** → Should see lower shadows, no anti-aliasing
2. **Click Medium** → Should see balanced settings
3. **Click High** → Should see enhanced shadows, anti-aliasing

### **Check Performance:**
- **FPS Counter** shows current performance
- **Low**: Should get 60+ FPS
- **Medium**: Should get 60 FPS
- **High**: Should get 60 FPS (on good hardware)

### **Verify Settings:**
- Go to **Project Settings > Quality**
- Click your buttons and watch the quality level change
- Settings should persist when you restart the game

---

## 🚨 **TROUBLESHOOTING**

### **Buttons Don't Work:**
1. Make sure you assigned the buttons in GraphicsSettingsSetup
2. Check console for error messages
3. Verify the buttons have onClick events

### **Settings Don't Change:**
1. Check that your URP Quality presets are set up in Project Settings
2. Make sure you have 3 quality levels (Low=0, Medium=1, High=2)
3. Verify URP is your render pipeline

### **FPS Counter Not Showing:**
1. Make sure you assigned a Text element to `fpsCounterText`
2. Check `showFPS = true` in GraphicsSettingsSetup
3. Make sure the Text element is active in the scene

### **Auto-Detection Not Working:**
1. Check `autoDetectHardware = true` in GraphicsSettingsSetup
2. Look at console for hardware detection messages
3. Manually set preset if auto-detection fails

---

## 🎯 **EXPECTED RESULTS**

### **After Setup:**
- ✅ **Clicking buttons** changes graphics settings
- ✅ **FPS counter** shows performance (if enabled)
- ✅ **Settings persist** between game sessions
- ✅ **Auto-detection** suggests appropriate preset
- ✅ **Console messages** confirm button presses

### **Performance Impact:**
- **Low**: 20-30% better FPS than Medium
- **Medium**: Balanced performance and quality
- **High**: 10-20% lower FPS than Medium, but better quality

---

## 🔧 **CUSTOMIZATION**

### **Modify Presets:**
Edit `GraphicsSettingsManager.cs` to change:
- Render scale (resolution)
- Shadow settings
- Anti-aliasing levels
- VSync settings

### **Add More Presets:**
1. Add new enum values to `GraphicsPreset`
2. Add new cases in `ApplyGraphicsPreset()`
3. Create new buttons and connect them

### **Disable Auto-Detection:**
Set `autoDetectHardware = false` in GraphicsSettingsSetup

---

## 🚀 **THAT'S IT!**

**Summary**: Add the scripts, assign your buttons, and enjoy a fully functional graphics settings system that actually changes your game's performance!

**Your players will love being able to adjust graphics for their hardware!** 🎮✨
