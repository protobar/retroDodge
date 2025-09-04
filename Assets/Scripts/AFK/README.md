# Simple AFK System - FIXED

## What it does:
- If any player doesn't move for 10 seconds → Shows "Player is AFK" message to **ALL PLAYERS**
- Starts dealing 5 damage per second until player dies
- When player moves again → Shows "Player is active again" message to **ALL PLAYERS**
- **FIXED**: Uses proper RPC-based UI synchronization (Photon best practice)

## Setup:

### 1. PlayerCharacter (Already Done)
- ✅ No setup needed - AFK detector auto-initializes

### 2. Create AFK UI Manager
1. **Create an empty GameObject** in your scene (name it "AFKManager")
2. **Add AFKUIManager script** to this GameObject
3. **Add PhotonView component** to the same GameObject
4. **Create UI Elements** in your scene:
   - AFK Message Panel (UI Panel with TextMeshPro)
   - AFK Status Text (TextMeshPro for status display)

### 3. Assign UI References
In the AFKUIManager component inspector:
- **AFK Message Text** → Your message text component
- **AFK Message Panel** → Your message panel GameObject
- **AFK Status Text** → Your status text component (optional)

### 4. Configure PhotonView
In the PhotonView component:
- **Observed Components** → Add AFKUIManager script
- **Synchronization** → Unreliable On Change

### 5. That's It!
- The AFKUIManager will automatically receive RPCs from all players
- Messages will appear on ALL clients when any player goes AFK

## Files:
- `SimpleAFKDetector.cs` - Detects AFK and deals damage (auto-attached to players)
- `AFKUIManager.cs` - UI manager with RPC-based synchronization
- `PlayerCharacter.cs` - Integration (already done)

## Settings (in Inspector):
- **AFK Threshold**: 10 seconds (how long before AFK)
- **Damage Interval**: 1 second (how often to deal damage)
- **AFK Damage**: 5 damage per interval
- **Position Threshold**: 0.5 units (minimum movement to reset timer)

## How it works (FIXED):
1. **SimpleAFKDetector** detects AFK on local player
2. **Calls AFKUIManager.ShowAFKMessage()** with player name and AFK state
3. **AFKUIManager sends RPC** to all clients to show/hide UI
4. **All clients receive RPC** and update their UI accordingly
5. **Uses Photon best practices** - RPCs for UI, not OnPhotonSerializeView

## Troubleshooting:

### If UI doesn't appear:
1. **Check Console** - Look for debug messages:
   - `[AFK UI Manager] Showing message: PlayerName is AFK`
   - `[AFK UI Manager] Missing UI references!` ← This means UI not assigned

2. **Check AFKUIManager** - Make sure:
   - AFKUIManager script is attached to a GameObject in scene
   - PhotonView component is attached to the same GameObject
   - UI references are assigned in inspector
   - GameObject is active in scene

3. **Check PhotonView** - Make sure:
   - AFKUIManager script is in "Observed Components"
   - Synchronization is set to "Unreliable On Change"

4. **Test Locally First** - The AFK player should see their own message

## Why this approach works:
- **RPCs are the correct way** to sync UI in Photon (not OnPhotonSerializeView)
- **OnPhotonSerializeView** is for frequent data like position, rotation
- **UI changes are infrequent** and perfect for RPCs
- **Single UI manager** handles all players from one place

That's it! Simple and effective. 🎯