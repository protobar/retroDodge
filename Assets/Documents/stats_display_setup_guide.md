# Consolidated Main Menu Profile UI Setup Guide

## Overview
The `MainMenuProfileUI` script now handles ALL player statistics display in the main menu, eliminating redundancy. It supports multiple display areas using arrays for name, level, XP, rank, and stats.

## Setup Instructions

### Step 1: MainMenuProfileUI Setup
1. In your Main Menu scene, find or create the GameObject with `MainMenuProfileUI` component
2. The script now handles both the main profile panel AND detailed stats panel
3. No separate PlayerStatsDisplay script needed

### Step 2: UI Structure
Create the following UI hierarchy:

```
MainMenuProfileUI GameObject
├── ProfilePanel (Canvas) - Main profile display
│   ├── Background (Image)
│   ├── Header
│   │   ├── Title (TextMeshPro) - "Player Profile"
│   │   └── CloseButton (Button)
│   └── Content (VerticalLayoutGroup)
│       ├── PlayerInfoSection
│       │   ├── PlayerName (TextMeshPro) - Array element 0
│       │   ├── PlayerLevel (TextMeshPro) - Array element 0
│       │   ├── PlayerRank (TextMeshPro) - Array element 0
│       │   ├── XPProgress (TextMeshPro) - Array element 0
│       │   ├── XPProgressSlider (Slider) - Array element 0
│       │   ├── RankIcon (Image) - Array element 0
│       │   └── RankBackground (Image) - Array element 0
│       ├── CurrencySection
│       │   ├── DodgeCoins (TextMeshPro)
│       │   └── RumbleTokens (TextMeshPro)
│       ├── QuickStatsSection
│       │   ├── TotalMatches (TextMeshPro) - Array element 0
│       │   ├── TotalWins (TextMeshPro) - Array element 0
│       │   ├── WinRate (TextMeshPro) - Array element 0
│       │   └── CurrentStreak (TextMeshPro) - Array element 0
│       ├── CompetitiveSection
│       │   ├── SkillRating (TextMeshPro) - Array element 0
│       │   └── PlacementMatches (TextMeshPro) - Array element 0
│       └── DetailedStatsButton (Button)
├── DetailedStatsPanel (Canvas) - Detailed stats overlay
│   ├── Background (Image)
│   ├── Header
│   │   ├── Title (TextMeshPro) - "Detailed Stats"
│   │   └── CloseButton (Button)
│   └── Content (VerticalLayoutGroup)
│       ├── PlayerInfoSection
│       │   ├── PlayerName (TextMeshPro) - Array element 1
│       │   ├── PlayerLevel (TextMeshPro) - Array element 1
│       │   ├── PlayerRank (TextMeshPro) - Array element 1
│       │   ├── XPProgress (TextMeshPro) - Array element 1
│       │   ├── XPProgressSlider (Slider) - Array element 1
│       │   ├── RankIcon (Image) - Array element 1
│       │   └── RankBackground (Image) - Array element 1
│       ├── QuickStatsSection
│       │   ├── TotalMatches (TextMeshPro) - Array element 1
│       │   ├── TotalWins (TextMeshPro) - Array element 1
│       │   ├── WinRate (TextMeshPro) - Array element 1
│       │   └── CurrentStreak (TextMeshPro) - Array element 1
│       └── CompetitiveSection
│           ├── SkillRating (TextMeshPro) - Array element 1
│           └── PlacementMatches (TextMeshPro) - Array element 1
└── ProfileButton (Button) - Opens main profile
```

### Step 3: Assign Array References
In the `MainMenuProfileUI` component, assign arrays for multiple display areas:

**Player Info Arrays (for multiple locations):**
- `Player Name Texts` → Array of TextMeshPro components
- `Player Level Texts` → Array of TextMeshPro components  
- `Player Rank Texts` → Array of TextMeshPro components
- `XP Progress Texts` → Array of TextMeshPro components
- `XP Progress Sliders` → Array of Slider components
- `Rank Icon Images` → Array of Image components
- `Rank Background Images` → Array of Image components

**Quick Stats Arrays:**
- `Total Matches Texts` → Array of TextMeshPro components
- `Total Wins Texts` → Array of TextMeshPro components
- `Win Rate Texts` → Array of TextMeshPro components
- `Current Streak Texts` → Array of TextMeshPro components

**Competitive Stats Arrays:**
- `Competitive Sections` → Array of GameObject components
- `Skill Rating Texts` → Array of TextMeshPro components
- `Placement Matches Texts` → Array of TextMeshPro components


## Features

### Consolidated System
- **Single Script**: All player stats handled by `MainMenuProfileUI`
- **No Redundancy**: Eliminated duplicate `PlayerStatsDisplay` script
- **Multiple Display Areas**: Arrays allow same data in different UI locations
- **Unified Updates**: All stats update simultaneously across all displays

### Essential Statistics Displayed
- **Player Level**: Current level with XP progress bar
- **Competitive Rank**: Current rank, SR, and placement status
- **Match Statistics**: Total matches, wins, win rate, current streak
- **Currency**: Dodge Coins and Rumble Tokens
- **Achievements**: Count and list of unlocked achievements

### Array-Based Display System
- **Player Name**: Display in multiple locations (profile, detailed stats, etc.)
- **Level & XP**: Show in main profile and detailed overlay
- **Rank & SR**: Consistent display across all UI areas
- **Match Stats**: Same data, different presentations
- **Flexible Layout**: Add new display areas by extending arrays

### Color Coding
- **Win Rate**: Green (70%+), Yellow (50-69%), Orange (30-49%), Red (<30%)
- **Current Streak**: Green (positive), Red (negative)
- **Rank**: Uses rank-specific colors from RankingSystem
- **Placement**: Yellow (in placement), Green (ranked)

### Auto-Update
- Stats automatically update every 5 seconds
- Updates when player data changes
- Manual refresh available via context menu
- All array elements update simultaneously

## Usage
1. **Main Profile**: Player clicks profile button to see basic stats
2. **Detailed Stats**: Click "Detailed Stats" button for expanded view
3. **Multiple Locations**: Same data appears consistently across UI
4. **Easy Navigation**: Simple toggle between profile and detailed views

## Debugging
- Right-click on MainMenuProfileUI component → "Refresh Profile"
- Check console for debug messages
- Ensure PlayerDataManager is properly initialized
- Verify array assignments in inspector
