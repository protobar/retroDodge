# Presentation Content - Retro Dodge Rumble FYP

## Slide 02: Problem Statement

### Content:

**The Problem:**
The current gaming market lacks accessible, competitive multiplayer dodgeball games that combine fast-paced action with meaningful progression systems. Existing dodgeball games either lack competitive depth, have poor networking infrastructure, or fail to provide engaging long-term progression that keeps players invested.

**Why This Matters:**
- **Gap in Market**: Limited quality multiplayer dodgeball games with competitive features
- **Networking Challenges**: Real-time multiplayer requires low-latency, synchronized gameplay
- **Player Retention**: Without progression systems, players lose motivation to continue playing
- **Accessibility**: Many competitive games have high skill barriers, excluding casual players

**Visual Suggestions:**
- Infographic showing: "Market Gap" → "Our Solution"
- Comparison table: Existing Games vs. Our Solution
- Statistics on multiplayer game market growth

---

## Slide 03: Objectives and Scope of the Project

### Objectives:

1. **Develop a Competitive Multiplayer Dodgeball Game**
   - Real-time 2.5D dodgeball combat with fighting game mechanics
   - Multiple game modes (Quick Match, Competitive, AI Practice, Custom Rooms)

2. **Implement Robust Networking Infrastructure**
   - Photon PUN2 integration for seamless multiplayer
   - Low-latency synchronization for responsive gameplay
   - Matchmaking system for balanced player matching

3. **Create Comprehensive Progression System**
   - XP-based leveling (1-100 levels)
   - Skill Rating (SR) system for competitive ranking
   - Currency and reward systems (DodgeCoins, RumbleTokens)

4. **Design Character System with Unique Abilities**
   - Multiple characters with distinct playstyles
   - Ultimate, Trick, and Treat ability framework
   - Character-specific progression and unlocks

5. **Integrate Backend Services for Player Data**
   - PlayFab authentication and user management
   - Cloud-based progression tracking
   - Global leaderboards and statistics

### In Scope:

✅ **Core Gameplay Systems**
- Player movement, ball physics, combat mechanics
- Character abilities (Ultimate, Trick, Treat)
- Stun and fallback systems for combat depth

✅ **Multiplayer Infrastructure**
- Real-time networking with Photon PUN2
- Matchmaking (Quick, Competitive, Custom)
- Network synchronization and optimization

✅ **Progression & Backend**
- PlayFab integration (Auth, Progression, Leaderboards)
- XP/Leveling system (1-100)
- Competitive ranking (SR system with 7 tiers)
- Currency system and rewards

✅ **Game Modes**
- Quick Match (casual multiplayer)
- Competitive Mode (ranked matches)
- AI Practice (offline training)
- Custom Rooms (private matches)

✅ **UI/UX Systems**
- Main menu, character selection, match UI
- Health bars, ability cooldowns, match information
- Settings and configuration panels

### Out of Scope:

❌ **Mobile Platform Support**
- Focus on PC/Desktop platform only
- Touch controls not implemented

❌ **Tournament Mode**
- Future feature, not included in initial release

❌ **Replay System**
- Match recording/playback not implemented

❌ **Spectator Mode**
- Watching live matches not included

❌ **Cross-Platform Play**
- Single platform focus (Windows/PC)

---

## Slide 04: Project Overview

### Content:

**Retro Dodge Rumble** is a 2.5D multiplayer dodgeball fighting game that combines classic dodgeball mechanics with competitive fighting game elements. Players engage in fast-paced matches where they must throw balls at opponents while dodging incoming attacks, using unique character abilities to gain tactical advantages.

**Project Type:** Game  
**Genre:** Action, Fighting, Sports, Multiplayer Competitive  
**Platform:** PC (Windows)  
**Engine:** Unity 2022.3+  
**Networking:** Photon PUN2  
**Backend:** PlayFab

**Key Features:**
- Real-time multiplayer dodgeball combat
- Multiple characters with unique abilities
- Competitive ranking system (SR-based)
- Comprehensive progression (XP, levels, currency)
- Multiple game modes (Quick, Competitive, AI, Custom)
- Cinematic ultimate abilities with camera effects
- Stun and fallback combat mechanics

**Visual Suggestions:**
- Gameplay screenshot showing 2.5D perspective
- Character roster showcase
- Game mode icons/buttons
- Logo/branding

---

## Slide 05: Development Flow & Architecture

### Content:

**Development Approach:**
- **Iterative Development**: Core-first strategy, then feature expansion
- **Component-Based Architecture**: Modular systems for maintainability
- **ScriptableObject Configuration**: Easy balancing without code changes

**Development Phases:**

**Phase 1: Core Gameplay Foundation** (Weeks 1-4)
- Player movement system (2.5D, jump, dash, duck)
- Ball physics and throwing mechanics
- Basic combat and catch system
- Character controller implementation

**Phase 2: Networking Integration** (Weeks 5-8)
- Photon PUN2 integration
- Network synchronization (player actions, ball state)
- Matchmaking system (Quick, Competitive, Custom)
- Room management and scene transitions

**Phase 3: Advanced Combat Systems** (Weeks 9-12)
- Ultimate ability system with cinematic camera
- Stun and fallback mechanics
- Ability framework (Ultimate/Trick/Treat)
- VFX integration and visual effects

**Phase 4: Backend & Progression** (Weeks 13-16)
- PlayFab integration (authentication, progression)
- XP/leveling system (1-100 levels)
- Competitive ranking (SR system)
- Currency system and rewards
- Leaderboard integration

**Phase 5: Polish & Optimization** (Weeks 17-20)
- UI/UX refinement
- Network optimization
- AI integration for offline practice
- Performance tuning and bug fixes
- Map system implementation

**Key Technical Decisions:**
- **Custom Physics**: Avoided Unity physics to reduce latency
- **Master Client Pattern**: Authoritative match state management
- **ScriptableObject System**: Map registry and character data
- **Event-Driven Architecture**: Loose coupling between systems

**Visual Suggestions:**
- Timeline diagram showing 5 phases
- Architecture diagram (Managers → Systems → Components)
- Code snippet showing ScriptableObject usage
- Network architecture diagram

---

## Slide 06: Main Systems

### Content:

**1. Core Gameplay Systems**
- **Player Character Controller**: 2.5D movement, jump, dash, duck, teleport
- **Ball System**: Physics, throwing, catching, bounce mechanics
- **Combat System**: Damage calculation, hit detection, health management
- **Catch System**: Scale-aware catch zones with timing windows

**2. Ability & Combat Mechanics**
- **Ultimate System**: Cinematic camera zoom, hold/release mechanics
- **Stun System**: Consecutive hit tracking, 3-second freeze
- **Fallback System**: Ultimate knockdown sequence (fall → ground → get up)
- **Ability Framework**: Unified system for Ultimate, Trick, Treat abilities

**3. Networking & Matchmaking**
- **Photon PUN2 Integration**: Real-time multiplayer synchronization
- **Matchmaking**: Quick Match, Competitive, Custom Rooms
- **Network Optimization**: Custom sync, reduced bandwidth
- **Room Management**: Settings, properties, scene transitions

**4. Progression & Backend**
- **PlayFab Integration**: Authentication, user data, cloud storage
- **XP System**: 1-100 leveling with exponential requirements
- **Ranking System**: Skill Rating (SR) with 7 tiers (Bronze → Rumbler)
- **Currency System**: DodgeCoins (soft), RumbleTokens (premium)
- **Leaderboards**: Global rankings, regional support

**5. Map & Room Settings**
- **Map Registry System**: ScriptableObject-based map management
- **Random Map Selection**: Unlocked maps for variety
- **Room Configuration**: Match length, map selection, privacy settings

**6. AI Integration**
- **AI Session Config**: Difficulty levels, character selection
- **AI Controller**: Human-like behavior patterns
- **Offline Practice**: Seamless AI fallback for matchmaking

**7. UI/UX Systems**
- **Main Menu**: Mode selection, profile display, settings
- **Character Selection**: Network-synchronized selection with timers
- **Match UI**: Health bars, round info, ability cooldowns, results screen
- **Progression UI**: Stats display, leaderboard, rank icons

**Visual Suggestions:**
- System architecture diagram
- Screenshots of each system in action
- Flow diagrams for matchmaking and progression
- Network synchronization visualization

---

## Quick Reference: Talking Points

### Problem Statement (30 seconds):
> "The gaming market lacks accessible competitive multiplayer dodgeball games with meaningful progression. Existing games either lack competitive depth or fail to retain players long-term. Retro Dodge Rumble addresses this gap by combining fast-paced dodgeball combat with comprehensive progression systems and robust networking infrastructure."

### Project Overview (45 seconds):
> "Retro Dodge Rumble is a 2.5D multiplayer dodgeball fighting game built in Unity. Players engage in fast-paced matches using unique character abilities in multiple game modes. The game features competitive ranking, comprehensive progression, and seamless multiplayer networking using Photon PUN2 and PlayFab backend services."

### Development Flow (2-2.5 minutes):
> "I followed an iterative, component-based approach. Phase 1 established core gameplay - movement and combat. Phase 2 integrated Photon networking for multiplayer. Phase 3 added advanced systems like ultimates and stun mechanics. Phase 4 integrated PlayFab for progression and leaderboards. Phase 5 focused on polish, optimization, and the map system. Key technical decisions included custom physics for low latency, ScriptableObjects for easy configuration, and a master-client networking model."

### Main Systems (2-2.5 minutes):
> "The game consists of seven main system categories. Core gameplay handles movement, ball physics, and combat. The ability system includes cinematic ultimates, stun, and fallback mechanics. Networking uses Photon PUN2 for real-time multiplayer with optimized synchronization. Progression integrates PlayFab for authentication, XP leveling, competitive ranking, and leaderboards. Additional systems include map management, AI integration, and comprehensive UI/UX."

---

*This document provides ready-to-use content for your FYP presentation slides.*

