# Complete Setup Guide: PlayFab Authentication + Progression System

## 🎯 **What's Already Done**

I've already modified your existing scripts:
- ✅ **MainMenuManager.cs** - Added OnConnectClicked() and updated OnDisconnected()
- ✅ **MatchManager.cs** - Added progression integration and ApplyProgressionRewards() method
- ✅ **PlayerHealth.cs** - Added damage tracking fields and logic
- ✅ **CollisionDamageSystem.cs** - Added damage dealt tracking

## 🚀 **Setup Steps**

### **Step 1: Create Connection Scene**
1. **File** → **New Scene** → Save as `Connection` in `Assets/Scenes/`
2. **Set as default**: File → Build Settings → Drag Connection scene to index 0

### **Step 2: Setup Connection Scene UI**
Create this hierarchy in Connection scene:
```
Canvas
├── SplashPanel (Image)
│   └── ClickArea (Button - full screen)
└── MainPanel (Image)
    ├── SignInButton, SignUpButton, GuestButton
    ├── SignInPanel (with EmailInput, PasswordInput, LoginButton, ErrorText)
    ├── SignUpPanel (with DisplayNameInput, EmailInput, PasswordInput, ConfirmPasswordInput, SignUpButton, ErrorText)
    └── LoadingPanel (with LoadingText)
```

### **Step 3: Add Authentication Scripts**
1. **Create Empty GameObject** "AuthenticationManager" in Connection scene
2. **Add Components**:
   - `PlayFabAuthManager`
   - `ConnectionManager` 
   - `ConnectionUI`
3. **Configure ConnectionUI** with all UI references

### **Step 4: Create Configuration Asset**
1. **Right-click** → Create → RDR → Progression Configuration
2. **Name**: "DefaultProgressionConfig"
3. **Move to**: `Assets/Resources/` folder

### **Step 5: Add Progression System (DontDestroyOnLoad)**
1. **Create Empty GameObject** "ProgressionManager" in Connection scene
2. **Add Components**:
   - `PlayerDataManager`
   - `MatchResultHandler`
   - `ModularRewardCalculator`
3. **Assign Config**: Set "DefaultProgressionConfig" to both PlayerDataManager and ModularRewardCalculator Config fields
4. **Set all debug options to true**
5. **Important**: This GameObject will persist across all scenes automatically

### **Step 6: Add Progression Results to MatchUI**
1. **In your Gameplay scene**:
   - Open your existing MatchUI GameObject
   - Add the new progression fields to the Inspector:
     - `Progression Results Panel` (GameObject)
     - `XP Gained Text` (TextMeshProUGUI) - Shows "+150 XP"
     - `Coins Gained Text` (TextMeshProUGUI) - Shows "+95 Coins"
     - `SR Change Text` (TextMeshProUGUI) - Shows "+22 SR" or "N/A"
     - `Rank Change Text` (TextMeshProUGUI) - Shows "RANK UP! Bronze 1 → Bronze 2"
     - `Level Up Text` (TextMeshProUGUI) - Shows "LEVEL UP! Level 5"

**Important**: Each player will see their OWN progression data (XP, coins, SR changes, etc.)

**Debugging**: If progression results don't show up:
1. Check the Console for debug messages
2. Right-click on MatchUI component → "Force Refresh Progression Results" to test
3. Make sure all progression text fields are assigned in the Inspector

### **Step 7: Assign References**
1. **In ConnectionUI**:
   - Assign all UI references to corresponding fields
2. **MatchManager Auto-Assignment**:
   - ✅ **Automatic!** MatchResultHandler will automatically assign itself to MatchManager when gameplay scene loads
   - No manual assignment needed

## 🧪 **Test the System**

### **Expected Flow:**
1. **Game starts** → Connection scene
2. **Click anywhere** → Main panel appears
3. **Click Guest** → Goes to MainMenu
4. **Play matches** → Check console for progression logs
5. **Restart game** → Data persists

### **Console Output:**
When a match ends, you should see:
```
[MatchResultHandler] Processing Casual match result. Win: True, Duration: 120.5s, Damage: 450/200
[PlayerDataManager] Applying rewards for Casual match. Win: True
[PlayerDataManager] Data saved successfully.
[MatchManager] Applied progression rewards for Casual match. Win: True
```

## 🐛 **Troubleshooting**

- **"Scene not found"** → Add Connection to Build Settings
- **"MatchResultHandler not assigned"** → Assign ProgressionManager reference
- **"No config found"** → Create DefaultProgressionConfig in Resources
- **"PlayFab not connected"** → Check Title ID in Project Settings

## ✅ **Success Checklist**

- [ ] Connection scene created and set as default
- [ ] Authentication UI setup complete
- [ ] AuthenticationManager GameObject created
- [ ] ProgressionManager GameObject created in Connection scene
- [ ] DefaultProgressionConfig asset created in Resources
- [ ] ConnectionUI references assigned in Inspector
- [ ] MatchUI progression fields assigned in Gameplay scene
- [ ] MatchResultHandler auto-assigns to MatchManager (automatic)
- [ ] Game starts at Connection scene
- [ ] Authentication works (Guest, Sign Up, Sign In)
- [ ] Matches show progression rewards (XP, coins, SR changes)
- [ ] Data persists between sessions

**Everything is ready! Just follow the setup steps above.** 🎉
