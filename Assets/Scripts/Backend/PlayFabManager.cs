using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== PLAYFAB MANAGER ====================
public class PlayFabManager : MonoBehaviour
{
    private static PlayFabManager instance;
    public static PlayFabManager Instance => instance;

    [Header("PlayFab Settings")]
    public string titleId = "YOUR_TITLE_ID";
    public bool useDeviceId = true;

    // Player data cache
    private Dictionary<string, object> playerData = new Dictionary<string, object>();
    private string playFabId = "";
    private bool isLoggedIn = false;

    // Events
    public System.Action<bool> OnLoginResult;
    public System.Action<Dictionary<string, int>> OnStatisticsReceived;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePlayFab();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void InitializePlayFab()
    {
        // Note: This is a simplified PlayFab integration
        // In a real implementation, you would use the PlayFab SDK
        Debug.Log("PlayFab Manager initialized");

        // Auto-login on start
        if (useDeviceId)
        {
            LoginWithDeviceID();
        }
    }

    public void LoginWithDeviceID()
    {
        string customId = SystemInfo.deviceUniqueIdentifier;
        LoginWithCustomID(customId);
    }

    public void LoginWithCustomID(string customId)
    {
        // Simulate PlayFab login
        StartCoroutine(SimulateLogin(customId));
    }

    IEnumerator SimulateLogin(string customId)
    {
        Debug.Log($"Logging in with Custom ID: {customId}");

        // Simulate network delay
        yield return new WaitForSeconds(1f);

        // Simulate successful login
        isLoggedIn = true;
        playFabId = "Player_" + customId.Substring(0, 8);

        Debug.Log($"Login successful! PlayFab ID: {playFabId}");
        OnLoginResult?.Invoke(true);

        // Load player statistics
        GetPlayerStatistics();
    }

    public void GetPlayerStatistics()
    {
        if (!isLoggedIn)
        {
            Debug.LogWarning("Not logged in to PlayFab");
            return;
        }

        StartCoroutine(SimulateGetStatistics());
    }

    IEnumerator SimulateGetStatistics()
    {
        yield return new WaitForSeconds(0.5f);

        // Simulate loaded statistics
        Dictionary<string, int> stats = new Dictionary<string, int>
        {
            { "TotalMatches", PlayerPrefs.GetInt("TotalMatches", 0) },
            { "Wins", PlayerPrefs.GetInt("Wins", 0) },
            { "Losses", PlayerPrefs.GetInt("Losses", 0) },
            { "TotalDamageDealt", PlayerPrefs.GetInt("TotalDamageDealt", 0) },
            { "TotalDamageTaken", PlayerPrefs.GetInt("TotalDamageTaken", 0) },
            { "UltimatesUsed", PlayerPrefs.GetInt("UltimatesUsed", 0) }
        };

        OnStatisticsReceived?.Invoke(stats);
    }

    public void UpdatePlayerStatistics(Dictionary<string, int> stats)
    {
        if (!isLoggedIn)
        {
            Debug.LogWarning("Not logged in to PlayFab");
            return;
        }

        StartCoroutine(SimulateUpdateStatistics(stats));
    }

    IEnumerator SimulateUpdateStatistics(Dictionary<string, int> stats)
    {
        yield return new WaitForSeconds(0.3f);

        // Update local PlayerPrefs (simulating PlayFab storage)
        foreach (var stat in stats)
        {
            int currentValue = PlayerPrefs.GetInt(stat.Key, 0);
            PlayerPrefs.SetInt(stat.Key, currentValue + stat.Value);
        }

        PlayerPrefs.Save();
        Debug.Log("Statistics updated successfully");
    }

    public void RecordMatchResult(bool won, int damageDealt, int damageTaken, string characterUsed)
    {
        var stats = new Dictionary<string, int>
        {
            { "TotalMatches", 1 },
            { won ? "Wins" : "Losses", 1 },
            { "TotalDamageDealt", damageDealt },
            { "TotalDamageTaken", damageTaken }
        };

        UpdatePlayerStatistics(stats);

        // Update character-specific stats
        if (!string.IsNullOrEmpty(characterUsed))
        {
            var charStats = new Dictionary<string, int>
            {
                { $"{characterUsed}_Matches", 1 },
                { $"{characterUsed}_Wins", won ? 1 : 0 }
            };

            UpdatePlayerStatistics(charStats);
        }

        Debug.Log($"Match result recorded: {(won ? "Victory" : "Defeat")}");
    }

    public void GetLeaderboard(string leaderboardName, System.Action<List<LeaderboardEntry>> callback)
    {
        StartCoroutine(SimulateGetLeaderboard(leaderboardName, callback));
    }

    IEnumerator SimulateGetLeaderboard(string leaderboardName, System.Action<List<LeaderboardEntry>> callback)
    {
        yield return new WaitForSeconds(1f);

        // Generate fake leaderboard data
        List<LeaderboardEntry> leaderboard = new List<LeaderboardEntry>();

        for (int i = 0; i < 10; i++)
        {
            leaderboard.Add(new LeaderboardEntry
            {
                PlayFabId = $"Player_{i + 1}",
                DisplayName = $"Player{i + 1}",
                StatValue = Random.Range(50 - i * 5, 100 - i * 3),
                Position = i
            });
        }

        callback?.Invoke(leaderboard);
    }

    public void AddCurrency(int amount, string currencyCode = "DC")
    {
        if (!isLoggedIn) return;

        StartCoroutine(SimulateAddCurrency(amount, currencyCode));
    }

    IEnumerator SimulateAddCurrency(int amount, string currencyCode)
    {
        yield return new WaitForSeconds(0.3f);

        int currentAmount = PlayerPrefs.GetInt(currencyCode, 0);
        PlayerPrefs.SetInt(currencyCode, currentAmount + amount);
        PlayerPrefs.Save();

        Debug.Log($"Added {amount} {currencyCode}. Total: {currentAmount + amount}");
    }

    public int GetCurrency(string currencyCode = "DC")
    {
        return PlayerPrefs.GetInt(currencyCode, 0);
    }

    public bool IsLoggedIn()
    {
        return isLoggedIn;
    }

    public string GetPlayFabId()
    {
        return playFabId;
    }
}

// ==================== LEADERBOARD ENTRY ====================
[System.Serializable]
public class LeaderboardEntry
{
    public string PlayFabId;
    public string DisplayName;
    public int StatValue;
    public int Position;
}