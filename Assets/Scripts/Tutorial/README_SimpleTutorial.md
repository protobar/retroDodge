# Simple Tutorial System for Retro Dodge Rumble

## 🎯 Overview

A simple UI-based tutorial system perfect for FYP implementation. Uses character spawning and text to explain game mechanics without complex interactive systems.

## 🎮 Features

### **Tutorial Panel**
- **Image-based**: Uses your tutorial images to explain mechanics
- **Navigation**: Next/Back buttons to navigate through pages
- **Progress**: Page counter showing current progress
- **Skip Option**: Players can skip tutorial if desired

### **Character Description Panel**
- **Character Spawning**: Spawns character prefabs with animations
- **Character Info**: Icon, name, tagline, lore, and descriptions
- **Ability Descriptions**: Ultimate, Trick, and Treat abilities
- **Character Cycling**: Previous/Next buttons to browse characters
- **Visual Design**: Clean, informative layout with live character preview

## 🔧 Implementation

### **Step 1: Setup Tutorial Manager**
1. Add `SimpleTutorialManager` to your main menu scene
2. Assign UI references in the inspector
3. Create tutorial pages with your images
4. Assign character preview prefabs for spawning

### **Step 2: Create Tutorial Pages**
```csharp
[System.Serializable]
public class TutorialPage
{
    public string title;
    public string description;
    public Sprite tutorialImage;
}
```

### **Step 3: Setup UI Elements**
- **Tutorial Panel**: Main tutorial container
- **Character Panel**: Character description container
- **Character Spawn Area**: Transform for character spawning
- **Navigation Buttons**: Next, Back, Close, Skip
- **Character Navigation**: Previous/Next character buttons
- **Content Areas**: Image, title, description text, character lore

### **Step 4: Configure Character Data**
Add to your CharacterData ScriptableObjects:
- **Character Tagline**: Short catchphrase
- **Character Lore**: Detailed background story
- **Ultimate Description**: How ultimate works
- **Trick Description**: How trick ability works
- **Treat Description**: How treat ability works

### **Step 5: Setup Character Spawning**
1. **Character Preview Prefabs**: Create separate prefabs with only animators
2. **Character Spawn Parent**: Transform for character spawning
3. **Character Spawn Position**: Position for character spawning
4. **Character Spawn Rotation**: Rotation for character spawning
5. **Character Spawn Scale**: Scale for character spawning

### **Step 6: Setup Main Menu Integration**
1. **MainMenuCharacterIntegration**: Add to main menu scene
2. **Main Menu Panel**: Reference to main menu UI panel
3. **Character Info Panel**: Reference to character info UI panel
4. **Character Info Button**: Button to toggle character info
5. **Panel Management**: Hide main menu, show character info
6. **Auto Character Spawning**: First character spawns automatically in main menu

## 🎯 Tutorial Flow

### **Page 1: Welcome**
- Welcome image
- Game introduction
- Basic overview

### **Page 2: Movement**
- Movement controls (Arrow keys)
- Jump and duck mechanics
- Visual demonstration

### **Page 3: Ball Mechanics**
- Ball pickup (D key)
- Ball throwing (A key)
- Ball catching (S key)
- Ball hold damage warning

### **Page 4: Abilities**
- Ultimate ability (Q key)
- Trick ability (W key)
- Treat ability (E key)
- Ability charging system

### **Page 5: Strategy**
- Dodging techniques
- Timing and positioning
- Health management
- Winning strategies

## 🎮 Character Description Panel

### **Character Information**
- **Character Icon**: Visual representation
- **Character Name**: Display name
- **Character Tagline**: Catchphrase
- **Character Lore**: Background story

### **Ability Descriptions**
- **Ultimate**: Detailed ultimate ability explanation
- **Trick**: Detailed trick ability explanation
- **Treat**: Detailed treat ability explanation

## 🔧 Integration

### **Main Menu Integration**
```csharp
public class TutorialMenuIntegration : MonoBehaviour
{
    public void ShowTutorial()
    {
        tutorialManager.ShowTutorial();
    }
    
    public void ShowCharacterInfo()
    {
        tutorialManager.ShowCharacterPanel(characterData);
    }
}
```

### **New Player Detection**
- Automatically shows tutorial for new players
- Saves tutorial completion status
- Allows replay for returning players

## 🎯 UI Setup

### **Tutorial Panel UI**
```
┌─────────────────────────────────────┐
│ [Tutorial Image]                    │
│                                     │
│ Title: Movement Controls            │
│ Description: Use arrow keys to...   │
│                                     │
│ [Back] [Next] [Skip] [Close]       │
│ Page: 1 / 5                         │
└─────────────────────────────────────┘
```

### **Character Panel UI**
```
┌─────────────────────────────────────┐
│ [Character Icon] Character Name     │
│ "Character Tagline"                 │
│                                     │
│ Character Lore:                     │
│ A legendary warrior with...         │
│                                     │
│ Ultimate: Power Throw               │
│ Trick: Slow Speed                   │
│ Treat: Shield                       │
│                                     │
│ [Close]                             │
└─────────────────────────────────────┘
```

## 🔧 Configuration

### **Tutorial Pages Setup**
1. Create tutorial images for each page
2. Add pages to `tutorialPages` array
3. Set titles and descriptions
4. Assign images to each page

### **Character Data Setup**
1. Open CharacterData ScriptableObjects
2. Fill in character tagline and lore
3. Add ability descriptions
4. Assign character icons

### **UI Setup**
1. Create tutorial panel in main menu
2. Add navigation buttons
3. Assign references in SimpleTutorialManager
4. Test tutorial flow

## 🎯 Benefits

### **For FYP**
- **Simple Implementation**: Easy to understand and maintain
- **Quick Setup**: 30 minutes to implement
- **Professional Look**: Clean, informative design
- **Focus on Core**: More time for game mechanics

### **For Players**
- **Clear Instructions**: Visual and text explanations
- **Easy Navigation**: Simple next/back navigation
- **Skip Option**: Can skip if desired
- **Character Info**: Detailed character descriptions

### **For Development**
- **Modular Design**: Easy to add/remove pages
- **Image-based**: Use your custom tutorial images
- **Flexible**: Can be customized per character
- **Maintainable**: Simple code structure

## 🎮 Usage

### **For New Players**
1. Tutorial automatically shows on first launch
2. Navigate through pages with Next/Back buttons
3. Skip if desired
4. Tutorial completion is saved

### **For Returning Players**
1. Tutorial button available in main menu
2. Can replay tutorial anytime
3. Character info accessible
4. Quick access to game mechanics

## 🔧 Testing

### **Test Scenarios**
1. **New Player**: First-time launch
2. **Returning Player**: Tutorial button access
3. **Character Info**: Character description panel
4. **Navigation**: Next/Back button functionality
5. **Skip Function**: Skip tutorial option

### **Validation**
1. **Tutorial Shows**: For new players
2. **Navigation Works**: Next/Back buttons
3. **Skip Works**: Skip button functionality
4. **Character Info**: Character panel displays
5. **Completion Saved**: Tutorial status saved

This simple tutorial system provides a clean, professional way to teach players your game mechanics while keeping the implementation simple and focused on your FYP goals.
