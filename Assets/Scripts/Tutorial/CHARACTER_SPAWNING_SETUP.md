# Character Spawning Setup Guide

## 🎯 Overview

This guide shows how to set up character spawning in the simple tutorial system, allowing players to see live character previews with animations instead of static images.

## 🔧 Setup Instructions

### **Step 1: Create Character Preview Prefabs**

#### **Create Separate Preview Prefabs:**
1. **Duplicate Character Prefabs**: Copy your main character prefabs
2. **Remove Network Components**: Remove PhotonView, SmoothSync, etc.
3. **Remove Gameplay Scripts**: Remove PlayerCharacter, PlayerInputHandler, etc.
4. **Keep Only**: Animator, Model, and basic components
5. **Name Convention**: "Nova_Preview", "Grudge_Preview", etc.

#### **Preview Prefab Structure:**
```
Character_Preview
├── Model (MeshRenderer, etc.)
├── Animator (Character animations)
└── Basic Components (Transform, etc.)
```

### **Step 2: Setup Character Spawning in SimpleTutorialManager**

#### **In SimpleTutorialManager Inspector:**
1. **Character Spawn Parent**: Transform where characters will spawn
2. **Character Spawn Position**: Position for character spawning (e.g., 0, 0, 0)
3. **Character Spawn Rotation**: Rotation for character spawning (e.g., 0, 0, 0)
4. **Character Spawn Scale**: Scale for character spawning (e.g., 1, 1, 1)
5. **Character Preview Prefabs**: Array of your preview prefabs

#### **Character Spawn Settings:**
```csharp
[Header("Character Spawning")]
[SerializeField] private Transform characterSpawnParent;
[SerializeField] private Vector3 characterSpawnPosition = new Vector3(0, 0, 0);
[SerializeField] private Vector3 characterSpawnRotation = new Vector3(0, 0, 0);
[SerializeField] private Vector3 characterSpawnScale = new Vector3(1, 1, 1);
[SerializeField] private GameObject[] characterPreviewPrefabs;
```

### **Step 3: Setup Main Menu Integration**

#### **In MainMenuCharacterIntegration Inspector:**
1. **Main Menu Panel**: Reference to main menu UI panel
2. **Character Info Panel**: Reference to character info UI panel
3. **Character Info Button**: Button to toggle character info
4. **Tutorial Manager**: Reference to SimpleTutorialManager
5. **Available Characters**: Array of CharacterData ScriptableObjects

#### **Main Menu Panel Management:**
```csharp
[Header("Main Menu Panels")]
[SerializeField] private GameObject mainMenuPanel;
[SerializeField] private GameObject characterInfoPanel;

[Header("Character Info Button")]
[SerializeField] private Button characterInfoButton;
[SerializeField] private TextMeshProUGUI characterInfoButtonText;
```

### **Step 4: Create Character Info Panel UI**

#### **Character Info Panel Layout:**
```
┌─────────────────────────────────────┐
│ [<] Character Name [>] [Close]       │
│                                     │
│ [Character Spawn Area]              │
│ (Live character preview here)       │
│                                     │
│ Character Tagline                   │
│ Character Lore:                     │
│ A legendary warrior with...         │
│                                     │
│ Ultimate: Power Throw               │
│ Trick: Slow Speed                   │
│ Treat: Shield                       │
│                                     │
│ Character: 1 / 3                    │
└─────────────────────────────────────┘
```

#### **UI Elements Needed:**
- **Character Info Panel**: Main container
- **Character Spawn Area**: Transform for character spawning
- **Character Name**: TextMeshPro (large, bold)
- **Character Tagline**: TextMeshPro (italic, colored)
- **Character Lore**: TextMeshPro (scrollable if needed)
- **Ultimate Description**: TextMeshPro (with formatting)
- **Trick Description**: TextMeshPro (with formatting)
- **Treat Description**: TextMeshPro (with formatting)
- **Navigation Buttons**: Previous/Next character buttons
- **Close Button**: Close character panel
- **Character Counter**: "1 / 3" display

### **Step 5: Configure Character Data ScriptableObjects**

#### **For Each Character:**
1. **Character Identity**:
   - `characterName` - Display name (e.g., "Nova", "Grudge")
   - `characterTagline` - Short catchphrase (e.g., "The Speed Demon")
   - `characterDescription` - Brief description
   - `characterLore` - Detailed background story
   - `characterIcon` - Character icon sprite

2. **Ability Descriptions**:
   - `ultimateDescription` - How ultimate ability works
   - `trickDescription` - How trick ability works
   - `treatDescription` - How treat ability works

### **Step 6: Test Character Spawning**

#### **Test Scenarios:**
1. **Open Character Info**: Click character info button
2. **Character Spawning**: Verify character spawns correctly
3. **Character Animation**: Verify animations play
4. **Character Navigation**: Use Previous/Next buttons
5. **Character Data**: Verify all information displays
6. **Close Panel**: Verify close button works

#### **Expected Behavior:**
- **Character Spawns**: Character prefab spawns in designated area
- **Auto Spawning**: First character spawns automatically in main menu
- **Navigation Works**: Previous/Next buttons cycle characters
- **Character Data**: All fields populated correctly
- **Panel Management**: Main menu hides, character info shows
- **Character Persistence**: Character stays spawned when switching panels

## 🎮 Character Spawning Features

### **Character Preview System:**
- **Live Character**: Spawns actual character prefabs
- **Animations**: Idle, Victory, and other animations
- **Character Cycling**: Previous/Next buttons
- **Character Data**: All character information displayed
- **Panel Management**: Hide main menu, show character info

### **Character Spawning Process:**
1. **Destroy Current**: Remove existing character
2. **Find Preview Prefab**: Match character to preview prefab
3. **Spawn Character**: Instantiate at spawn position
4. **Set Transform**: Position, rotation, scale
5. **Get Animator**: Get character animator component (for reference only)
6. **Auto Spawning**: First character spawns automatically in main menu

### **Character Navigation:**
- **Previous Character**: Cycles to previous character
- **Next Character**: Cycles to next character
- **Character Counter**: Shows current position
- **Character Data**: Updates all character information
- **Character Spawning**: Spawns new character preview

## 🔧 Usage Examples

### **From Main Menu:**
```csharp
// Show character info panel
mainMenuCharacterIntegration.ShowCharacterInfo();

// Show specific character
mainMenuCharacterIntegration.ShowCharacterInfo(novaCharacterData);

// Hide character info panel
mainMenuCharacterIntegration.HideCharacterInfo();
```

### **Character Navigation:**
```csharp
// Previous character
mainMenuCharacterIntegration.PreviousCharacter();

// Next character
mainMenuCharacterIntegration.NextCharacter();

// Get current character
CharacterData currentChar = mainMenuCharacterIntegration.GetCurrentCharacter();
```

### **Character Spawning:**
```csharp
// Spawn character preview
tutorialManager.SpawnCharacterPreview(characterData);

// Destroy current character
tutorialManager.DestroyCurrentCharacter();

// Get current spawned character
GameObject currentChar = tutorialManager.GetCurrentSpawnedCharacter();
```

## 🎯 Benefits

### **For Players:**
- **Live Character Preview**: See actual character models
- **Character Animations**: Watch character animations
- **Character Comparison**: Easy to compare different characters
- **Visual Learning**: Character models and descriptions
- **Easy Navigation**: Simple Previous/Next buttons

### **For Development:**
- **Modular Design**: Easy to add/remove characters
- **Data-Driven**: Uses ScriptableObjects for character data
- **Flexible**: Can be customized per character
- **Maintainable**: Simple code structure
- **Visual Appeal**: Live character previews

## 🔧 Troubleshooting

### **Common Issues:**

#### **Character Not Spawning:**
- Check if `characterPreviewPrefabs` array is populated
- Verify `characterSpawnParent` is assigned
- Ensure character spawn position is correct
- Check if character preview prefabs are valid

#### **Character Animation Not Playing:**
- Verify character preview prefabs have Animator components
- Check if animation triggers are set correctly
- Ensure character animator is properly configured
- Check if animation states exist

#### **Character Navigation Not Working:**
- Check if Previous/Next buttons are assigned
- Verify button onClick events are connected
- Ensure character counter is updating
- Check if character data is valid

#### **Panel Management Issues:**
- Verify main menu panel reference is assigned
- Check if character info panel reference is assigned
- Ensure panel visibility is managed correctly
- Check if button text is updating

This character spawning system provides a clean, professional way to showcase your characters with live previews while keeping the implementation simple and focused on your FYP goals.
