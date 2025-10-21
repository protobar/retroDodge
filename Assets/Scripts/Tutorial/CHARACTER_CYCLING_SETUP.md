# Character Cycling Setup Guide

## 🎯 Overview

This guide shows how to set up character cycling in the simple tutorial system, allowing players to browse through all available characters and see their descriptions.

## 🔧 Setup Instructions

### **Step 1: Assign Character Data References**

#### **In SimpleTutorialManager Inspector:**
1. **Available Characters Array**: Drag your CharacterData ScriptableObjects into the `availableCharacters` array
2. **Character Panel UI Elements**: Assign all the UI references:
   - `characterIcon` - Image component for character icon
   - `characterName` - TextMeshPro for character name
   - `characterTagline` - TextMeshPro for character tagline
   - `characterDescription` - TextMeshPro for character description
   - `ultimateDescription` - TextMeshPro for ultimate ability description
   - `trickDescription` - TextMeshPro for trick ability description
   - `treatDescription` - TextMeshPro for treat ability description
   - `characterCloseButton` - Button to close character panel
   - `previousCharacterButton` - Button to go to previous character
   - `nextCharacterButton` - Button to go to next character
   - `characterCounter` - TextMeshPro to show "1 / 3" etc.

#### **In TutorialMenuIntegration Inspector:**
1. **Available Characters Array**: Drag the same CharacterData ScriptableObjects into the `availableCharacters` array
2. **Tutorial Manager Reference**: Assign the SimpleTutorialManager reference

### **Step 2: Configure Character Data ScriptableObjects**

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

### **Step 3: Create Character Panel UI**

#### **Character Panel Layout:**
```
┌─────────────────────────────────────┐
│ [<] Character Name [>] [Close]       │
│ [Character Icon]                    │
│ "Character Tagline"                 │
│                                     │
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
- **Character Panel**: Main container
- **Character Icon**: Image component
- **Character Name**: TextMeshPro (large, bold)
- **Character Tagline**: TextMeshPro (italic, colored)
- **Character Description**: TextMeshPro (scrollable if needed)
- **Ultimate Description**: TextMeshPro (with formatting)
- **Trick Description**: TextMeshPro (with formatting)
- **Treat Description**: TextMeshPro (with formatting)
- **Navigation Buttons**: Previous/Next character buttons
- **Close Button**: Close character panel
- **Character Counter**: "1 / 3" display

### **Step 4: Test Character Cycling**

#### **Test Scenarios:**
1. **Open Character Panel**: Click character info button
2. **Navigate Characters**: Use Previous/Next buttons
3. **Character Counter**: Verify counter updates correctly
4. **Character Data**: Verify all character information displays
5. **Close Panel**: Verify close button works

#### **Expected Behavior:**
- **First Character**: Shows first character in array
- **Previous Button**: Cycles to last character when at first
- **Next Button**: Cycles to first character when at last
- **Character Counter**: Shows current position (e.g., "2 / 3")
- **Character Data**: All fields populated correctly

## 🎮 Usage Examples

### **From Main Menu:**
```csharp
// Show character info panel
tutorialMenuIntegration.ShowCharacterInfo();

// Show specific character
tutorialMenuIntegration.ShowCharacterInfo(novaCharacterData);
```

### **From Tutorial Manager:**
```csharp
// Show character panel
tutorialManager.ShowCharacterPanel();

// Show specific character
tutorialManager.ShowCharacterPanel(novaCharacterData);

// Show character at specific index
tutorialManager.ShowCharacterPanelAtIndex(1);

// Get current character
CharacterData currentChar = tutorialManager.GetCurrentCharacter();
```

### **Character Navigation:**
```csharp
// Previous character
tutorialManager.PreviousCharacter();

// Next character
tutorialManager.NextCharacter();
```

## 🔧 Character Data Example

### **Nova Character Data:**
```csharp
characterName = "Nova";
characterTagline = "The Speed Demon";
characterDescription = "A lightning-fast warrior with incredible agility.";
characterLore = "Nova was born with the gift of speed, allowing her to move faster than the eye can see. Her ultimate ability, Speed Burst, makes her nearly untouchable for a short time.";

ultimateDescription = "Speed Burst: Temporarily increases movement speed and makes you harder to hit.";
trickDescription = "Slow Speed: Reduces opponent's movement speed for a short time.";
treatDescription = "Shield: Creates a protective barrier that blocks incoming damage.";
```

### **Grudge Character Data:**
```csharp
characterName = "Grudge";
characterTagline = "The Powerhouse";
characterDescription = "A massive warrior with incredible strength and durability.";
characterLore = "Grudge's immense strength comes from years of training and a burning desire for victory. His ultimate ability, Power Throw, can knock down even the strongest opponents.";

ultimateDescription = "Power Throw: A devastating throw that deals massive damage and knockback.";
trickDescription = "Instant Damage: Deals immediate damage to the opponent.";
treatDescription = "Teleport: Instantly teleports to a nearby location.";
```

## 🎯 Benefits

### **For Players:**
- **Character Comparison**: Easy to compare different characters
- **Ability Understanding**: Clear explanation of each ability
- **Visual Learning**: Character icons and descriptions
- **Easy Navigation**: Simple Previous/Next buttons

### **For Development:**
- **Modular Design**: Easy to add/remove characters
- **Data-Driven**: Uses ScriptableObjects for character data
- **Flexible**: Can be customized per character
- **Maintainable**: Simple code structure

## 🔧 Troubleshooting

### **Common Issues:**

#### **Character Panel Not Showing:**
- Check if `availableCharacters` array is populated
- Verify `tutorialManager` reference is assigned
- Ensure character panel UI is properly set up

#### **Character Data Not Displaying:**
- Verify CharacterData ScriptableObjects are configured
- Check if UI references are assigned correctly
- Ensure character descriptions are filled in

#### **Navigation Not Working:**
- Check if Previous/Next buttons are assigned
- Verify button onClick events are connected
- Ensure character counter is updating

#### **Character Cycling Issues:**
- Check if `availableCharacters` array has characters
- Verify `currentCharacterIndex` is within bounds
- Ensure character data is valid

This character cycling system provides a clean, professional way to showcase your characters and their abilities while keeping the implementation simple and focused on your FYP goals.
