# Custom Rank Icons Setup Guide

## Overview
The system now supports custom rank icons through a dedicated `RankIconConfiguration` ScriptableObject. This provides better separation of concerns, easier maintenance, and improved team workflow. Your custom rank icons will be displayed in both the leaderboard and player profile.

## Required Rank Icons

### Rank Names and Expected Files
Based on your ranking system, you need these icon files:

```
Assets/Resources/RankIcons/
├── Bronze 1.png
├── Bronze 2.png
├── Bronze 3.png
├── Silver 1.png
├── Silver 2.png
├── Silver 3.png
├── Gold 1.png
├── Gold 2.png
├── Gold 3.png
├── Platinum 1.png
├── Platinum 2.png
├── Platinum 3.png
├── Diamond 1.png
├── Diamond 2.png
├── Diamond 3.png
├── Dodger 1.png
├── Dodger 2.png
└── Rumbler.png
```

## Setup Instructions

### Step 1: Create RankIconConfiguration ScriptableObject
1. **Right-click in Project window** → Create → RDR → Rank Icon Configuration
2. **Name it**: `RankIconConfig` (or your preferred name)
3. **This creates**: `Assets/RankIconConfig.asset`

### Step 2: Configure Rank Icons
1. **Select the RankIconConfig asset**
2. **In Inspector**: Expand "Rank Icons Configuration"
3. **Set Array Size** to match your number of ranks
4. **For each rank**:
   - **Rank Name**: Exact rank name (e.g., "Bronze 1", "Silver 2")
   - **Icon Sprite**: Drag your main rank icon
   - **Small Icon**: Optional smaller variant
   - **Large Icon**: Optional larger variant
   - **Icon Color**: Optional color tint (default: white)
   - **Size Multiplier**: Optional size adjustment (default: 1.0)

### Step 3: Import Your Rank Icons
1. **Create folder**: `Assets/Art/RankIcons/` (or your preferred location)
2. **Drag your rank icon images** into this folder
3. **Import settings**:
   - **Texture Type**: Sprite (2D and UI)
   - **Sprite Mode**: Single
   - **Pixels Per Unit**: 100
   - **Filter Mode**: Bilinear
   - **Compression**: None (for best quality)

### Step 4: Update UI References

#### Leaderboard Entry Prefab
1. **Find your LeaderboardEntryPrefab**
2. **Replace the rank icon Text component** with an Image component
3. **In LeaderboardEntryUI script**:
   - Assign the Image component to `Rank Icon Image` field
   - Assign the RankIconConfig asset to `Rank Icon Config` field
   - Configure `Use Small Icon` and `Use Large Icon` as needed

#### Main Menu Profile UI
1. **Find your MainMenuProfileUI GameObject**
2. **In MainMenuProfileUI script**:
   - Assign the RankIconConfig asset to `Rank Icon Config` field
   - Configure `Use Small Icons` and `Use Large Icons` as needed
   - Ensure rank icon Images are assigned to the `Rank Icon Images` array

### Step 4: Test the Icons

#### Method 1: In-Game Testing
1. **Play competitive matches** to change your rank
2. **Check Main Menu profile** - should show custom rank icon
3. **Open leaderboard** - should show custom rank icons for all players

#### Method 2: Debug Testing
1. **Use LeaderboardDebugger**:
   - Right-click → "Force Update Player SR"
   - This sets high SR to test higher ranks
2. **Check console** for icon loading messages
3. **Verify icons appear** in both profile and leaderboard

## Icon Specifications

### Recommended Dimensions
- **Size**: 64x64 pixels (or 128x128 for high DPI)
- **Format**: PNG with transparency
- **Aspect Ratio**: Square (1:1)
- **Background**: Transparent

### Design Guidelines
- **Consistent Style**: All icons should have the same visual style
- **Clear Recognition**: Easy to distinguish between ranks
- **High Contrast**: Visible on various backgrounds
- **Scalable**: Look good at different sizes

## Troubleshooting

### Issue 1: Icons Not Loading
**Symptoms**: 
- Console shows "Rank icon not found: RankIcons/[RankName]"
- Default/empty icons displayed

**Solutions**:
- Check file names are exactly correct (case-sensitive)
- Verify files are in `Assets/Resources/RankIcons/`
- Ensure images are imported as Sprites
- Check file extensions (.png, .jpg, etc.)

### Issue 2: Wrong Icons Displayed
**Symptoms**:
- Different rank icons than expected
- Icons don't match current rank

**Solutions**:
- Verify rank name mapping in RankingSystem
- Check icon file names match rank names exactly
- Test with different SR values to see rank changes

### Issue 3: Icons Too Small/Large
**Symptoms**:
- Icons appear too small or too large in UI

**Solutions**:
- Adjust Image component size in Unity
- Resize source images to appropriate dimensions
- Check Image component settings (Preserve Aspect, etc.)

## Advanced Configuration

### Custom Icon Paths
If you want to use different folder structure, modify the `GetRankIconSprite` method:

```csharp
// In LeaderboardEntryUI.cs and MainMenuProfileUI.cs
private Sprite GetRankIconSprite(string rankName)
{
    if (string.IsNullOrEmpty(rankName)) return null;
    
    // Custom path - change this to your preferred structure
    string iconPath = $"YourCustomFolder/{rankName}";
    Sprite rankIcon = Resources.Load<Sprite>(iconPath);
    
    return rankIcon;
}
```

### Fallback Icons
Add fallback logic for missing icons:

```csharp
private Sprite GetRankIconSprite(string rankName)
{
    if (string.IsNullOrEmpty(rankName)) return null;
    
    string iconPath = $"RankIcons/{rankName}";
    Sprite rankIcon = Resources.Load<Sprite>(iconPath);
    
    // Fallback to default icon if specific rank icon not found
    if (rankIcon == null)
    {
        rankIcon = Resources.Load<Sprite>("RankIcons/Default");
    }
    
    return rankIcon;
}
```

## Testing Checklist

### ✅ Basic Functionality
- [ ] Icons load in Main Menu profile
- [ ] Icons load in leaderboard entries
- [ ] Icons change when rank changes
- [ ] No console errors about missing icons

### ✅ Visual Quality
- [ ] Icons are clear and recognizable
- [ ] Icons scale properly in UI
- [ ] Icons have consistent style
- [ ] Icons work on different backgrounds

### ✅ Performance
- [ ] Icons load quickly
- [ ] No memory leaks from icon loading
- [ ] Smooth UI updates when rank changes
- [ ] No stuttering or lag

## Success Indicators

**You'll know it's working when:**
- Custom rank icons appear in Main Menu profile
- Custom rank icons appear in leaderboard
- Icons change when you gain/lose rank
- Console shows no "icon not found" warnings
- All rank tiers display correct icons

## File Structure Example

```
Assets/
├── Resources/
│   └── RankIcons/
│       ├── Bronze 1.png
│       ├── Bronze 2.png
│       ├── Bronze 3.png
│       ├── Silver 1.png
│       ├── Silver 2.png
│       ├── Silver 3.png
│       ├── Gold 1.png
│       ├── Gold 2.png
│       ├── Gold 3.png
│       ├── Platinum 1.png
│       ├── Platinum 2.png
│       ├── Platinum 3.png
│       ├── Diamond 1.png
│       ├── Diamond 2.png
│       ├── Diamond 3.png
│       ├── Dodger 1.png
│       ├── Dodger 2.png
│       └── Rumbler.png
└── Scripts/
    └── Progression/
        ├── LeaderboardEntryUI.cs
        └── MainMenuProfileUI.cs
```

**Your custom rank icons will now be displayed throughout the game!** 🎨
