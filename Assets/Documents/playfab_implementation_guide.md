# PlayFab Implementation Guide for CursorAI

## Project Context

**Retro Dodge Rumble** is a complete 2.5D multiplayer dodgeball fighting game with:
- Existing Photon PUN2 multiplayer system
- Character selection and AI gameplay
- Complete game mechanics and UI

**Goal**: Add PlayFab authentication, player progression, and leaderboards without breaking existing functionality.

## Critical Requirements

1. **Use PlayFab Unity SDK properly** - Title ID is auto-configured via PlayFabSharedSettings
2. **Maintain existing Photon integration** - sync PlayFab display name to Photon nickname
3. **Implement proper authentication flow** with comprehensive error handling
4. **Use correct PlayFab APIs** for different data types
5. **Keep code simple and modular** to avoid integration issues

---

## Phase 1: PlayFab Authentication System

### Authentication Manager Requirements

Create `PlayFabAuthManager.cs` with these exact specifications:

#### Core Features
- **Email/Password login** with validation
- **Account registration** with display name
- **Guest login** using device ID
- **Auto-login** with stored credentials
- **Proper error handling** with user-friendly messages
- **UI state management** (loading, success, error states)

#### Critical Implementation Details

```csharp
// CORRECT login request format
var request = new LoginWithEmailAddressRequest
{
    Email = email,
    Password = password,
    InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
    {
        GetPlayerProfile = true,
        GetUserAccountInfo = true
    }
};

// CORRECT display name extraction
private void OnLoginSuccess(LoginResult result)
{
    PlayFabId = result.PlayFabId;
    PlayerDisplayName = result.InfoResultPayload?.PlayerProfile?.DisplayName ?? 
                       "Player_" + PlayFabId.Substring(0, 8);
    
    // Sync to Photon
    PhotonNetwork.NickName = PlayerDisplayName;
}

// CORRECT registration request
var registerRequest = new RegisterPlayFabUserRequest
{
    Email = email,
    Password = password,
    DisplayName = displayName,
    RequireBothUsernameAndEmail = false
};
```

#### Error Handling Requirements

Map PlayFab error codes to user-friendly messages:

```csharp
private string GetErrorMessage(PlayFabError error)
{
    switch (error.Error)
    {
        case PlayFabErrorCode.InvalidEmailAddress:
            return "Invalid email format";
        case PlayFabErrorCode.InvalidUsernameOrPassword:
            return "Invalid email or password";
        case PlayFabErrorCode.EmailAddressNotAvailable:
            return "Email already in use";
        case PlayFabErrorCode.AccountNotFound:
            return "Account not found";
        default:
            return "Login failed: " + error.ErrorMessage;
    }
}
```

#### Input Validation Requirements

```csharp
// Email validation
private bool IsValidEmail(string email)
{
    try
    {
        var addr = new System.Net.Mail.MailAddress(email);
        return addr.Address == email;
    }
    catch { return false; }
}

// Password validation
private bool IsValidPassword(string password)
{
    return !string.IsNullOrEmpty(password) && password.Length >= 6;
}
```

---

## Phase 2: Connection Scene Implementation

### Scene Structure
1. **Connection Scene** - handles authentication and initial setup
2. **MainMenu Scene** - existing scene, remove auth logic
3. Keep existing CharacterSelection and Gameplay scenes

### Connection Scene Requirements

#### UI Components Needed
- Auth selection panel (Sign In/Sign Up/Guest buttons)
- Login panel (email, password, login button, back button)
- Signup panel (nickname, email, password, confirm password, signup button, back button)
- Loading panel (status text, loading indicator)
- Status text for error messages

#### Flow Logic
1. Show auth selection panel on scene load
2. Handle panel switching (selection → login/signup → loading → success)
3. On successful authentication, load MainMenu scene
4. Handle errors by returning to appropriate panel with error message

#### Critical Connection Logic

```csharp
public class ConnectionManager : MonoBehaviour
{
    private void Start()
    {
        // Check for auto-login
        CheckStoredCredentials();
    }
    
    private void CheckStoredCredentials()
    {
        string email = PlayerPrefs.GetString("PlayFab_Email", "");
        string password = PlayerPrefs.GetString("PlayFab_Password", "");
        
        if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
        {
            ShowLoadingPanel("Auto-logging in...");
            PlayFabAuthManager.Instance.LoginWithEmail(email, password);
        }
        else
        {
            ShowAuthSelectionPanel();
        }
    }
    
    private void OnAuthSuccess()
    {
        ShowLoadingPanel("Connecting to servers...");
        PhotonNetwork.ConnectUsingSettings();
    }
    
    private void OnPhotonConnected()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
```

---

## Phase 3: Player Data Management

### Data Structure

Use this exact PlayerStats structure:

```csharp
[System.Serializable]
public class PlayerStats
{
    [Header("Identity")]
    public string playerName;
    public string playFabId;
    public int level = 1;
    public int xp = 0;
    
    [Header("Currency")]
    public int dodgeCoins = 0;    // Soft currency
    public int retroTokens = 0;   // Premium currency
    
    [Header("Performance")]
    public int totalMatches = 0;
    public int wins = 0;
    public int losses = 0;
    public int currentWinStreak = 0;
    public int bestWinStreak = 0;
    
    [Header("Competitive")]
    public int skillRating = 0;
    public string currentRank = "Unranked";
    
    public float GetWinRate() => totalMatches > 0 ? (float)wins / totalMatches * 100f : 0f;
}
```

### Correct Data Storage Methods

**For Player Progression Data (XP, currency, stats):**

```csharp
// Use UpdateUserDataRequest - NOT UpdatePlayerStatisticsRequest
public void SavePlayerData(PlayerStats stats)
{
    var request = new UpdateUserDataRequest
    {
        Data = new Dictionary<string, string>
        {
            { "PlayerStats", JsonUtility.ToJson(stats) },
            { "LastLoginDate", DateTime.Now.ToString() }
        }
    };
    
    PlayFabClientAPI.UpdateUserData(request, OnDataSaved, OnDataSaveError);
}

// Load player data
public void LoadPlayerData()
{
    var request = new GetUserDataRequest();
    PlayFabClientAPI.GetUserData(request, OnDataLoaded, OnDataLoadError);
}
```

**For Leaderboard Data (competitive ranking only):**

```csharp
// Use UpdatePlayerStatisticsRequest ONLY for leaderboards
public void UpdateLeaderboard(int skillRating)
{
    var request = new UpdatePlayerStatisticsRequest
    {
        Statistics = new List<StatisticUpdate>
        {
            new StatisticUpdate
            {
                StatisticName = "SkillRating",
                Value = skillRating
            }
        }
    };
    
    PlayFabClientAPI.UpdatePlayerStatistics(request, OnLeaderboardUpdated, OnLeaderboardError);
}
```

---

## Phase 4: MainMenu Integration

### Remove Authentication Logic

Update MainMenuManager to remove all authentication code:

```csharp
// Remove these from MainMenuManager:
// - PlayFab login methods
// - Authentication UI references
// - Connection logic

// Keep only:
private void Start()
{
    // Assume user is already authenticated when this scene loads
    if (!PhotonNetwork.IsConnectedAndReady)
    {
        ShowStatus("Reconnecting...");
        PhotonNetwork.ConnectUsingSettings();
    }
    else
    {
        ShowMainMenu();
    }
}

public override void OnConnectedToMaster()
{
    // User is authenticated, just show main menu
    ShowMainMenu();
    UpdatePlayerInfo();
}
```

### Player Info Display

```csharp
private void UpdatePlayerInfo()
{
    if (playerInfoText != null)
    {
        string guestStatus = PlayFabAuthManager.IsGuest ? " (Guest)" : "";
        playerInfoText.text = $"{PlayFabAuthManager.PlayerDisplayName}{guestStatus}";
    }
}
```

---

## Phase 5: Match Result Processing

### XP and Currency Calculation

```csharp
public class MatchResultProcessor : MonoBehaviour
{
    [SerializeField] private XPSettings xpSettings;
    [SerializeField] private CurrencySettings currencySettings;
    
    public void ProcessMatchResult(MatchData matchData)
    {
        if (PlayFabAuthManager.IsGuest) return; // No progression for guests
        
        PlayerStats stats = PlayerDataManager.Instance.GetPlayerStats();
        
        // Calculate base rewards
        int xpGained = CalculateXP(matchData);
        int dcGained = CalculateCurrency(matchData);
        
        // Update stats
        UpdatePlayerStats(stats, matchData, xpGained, dcGained);
        
        // Save to PlayFab
        PlayerDataManager.Instance.SavePlayerData(stats);
        
        // Show rewards UI
        ShowRewardsUI(xpGained, dcGained);
    }
    
    private int CalculateXP(MatchData matchData)
    {
        int baseXP = matchData.isWin ? xpSettings.winXP : xpSettings.lossXP;
        
        // Add performance bonuses
        baseXP += matchData.damageDealt / 10;
        baseXP += matchData.successfulCatches * 15;
        baseXP += matchData.ultimateHits * 20;
        
        return baseXP;
    }
}
```

---

## Phase 6: Leaderboard Implementation

### Leaderboard Manager

```csharp
public class LeaderboardManager : MonoBehaviour
{
    public void UpdatePlayerRanking(int skillRating)
    {
        var request = new UpdatePlayerStatisticsRequest
        {
            Statistics = new List<StatisticUpdate>
            {
                new StatisticUpdate
                {
                    StatisticName = "SkillRating",
                    Value = skillRating
                }
            }
        };
        
        PlayFabClientAPI.UpdatePlayerStatistics(request, OnRankingUpdated, OnRankingError);
    }
    
    public void GetLeaderboard()
    {
        var request = new GetLeaderboardRequest
        {
            StatisticName = "SkillRating",
            StartPosition = 0,
            MaxResultsCount = 100
        };
        
        PlayFabClientAPI.GetLeaderboard(request, OnLeaderboardReceived, OnLeaderboardError);
    }
}
```

---

## Implementation Checklist

### Phase 1 - Authentication
- [ ] Create PlayFabAuthManager with proper login/register methods
- [ ] Implement comprehensive error handling
- [ ] Add input validation for email/password
- [ ] Test auto-login functionality

### Phase 2 - Connection Scene
- [ ] Create Connection scene with proper UI panels
- [ ] Implement panel switching logic
- [ ] Add loading states and error display
- [ ] Test scene transition to MainMenu

### Phase 3 - Data Management
- [ ] Create PlayerStats structure
- [ ] Implement data save/load with UpdateUserDataRequest
- [ ] Add data validation and error handling
- [ ] Test data persistence across sessions

### Phase 4 - MainMenu Update
- [ ] Remove authentication logic from MainMenuManager
- [ ] Update player info display
- [ ] Test reconnection after AI matches
- [ ] Verify Photon nickname sync

### Phase 5 - Match Processing
- [ ] Implement match result calculation
- [ ] Add XP and currency rewards
- [ ] Create rewards display UI
- [ ] Test progression after matches

### Phase 6 - Leaderboards
- [ ] Implement leaderboard updates for competitive mode only
- [ ] Create leaderboard display UI
- [ ] Test ranking system
- [ ] Verify guest users are excluded

---

## Common Pitfalls to Avoid

1. **Don't manually set PlayFabId** - it's handled automatically by SDK
2. **Don't use UpdatePlayerStatisticsRequest for progression data** - use UpdateUserDataRequest
3. **Don't forget InfoRequestParameters** in login requests
4. **Don't skip input validation** - validate email format and password length
5. **Don't ignore error codes** - map them to user-friendly messages
6. **Don't break existing Photon integration** - always sync nicknames

---

This guide provides complete, working examples for each component. Follow the exact API usage patterns shown to avoid common integration issues.