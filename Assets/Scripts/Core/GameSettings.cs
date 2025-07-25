using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== GAME SETTINGS SCRIPTABLE OBJECT ====================
[CreateAssetMenu(fileName = "GameSettings", menuName = "Game/Settings")]
public class GameSettings : ScriptableObject
{
    [Header("Match Settings")]
    public float roundDuration = 90f;
    public int roundsToWin = 2;
    public float respawnDelay = 1.5f;

    [Header("Ball Settings")]
    public float ballSpawnDelay = 1.5f;
    public float maxBallHoldTime = 5f;
    public float holdDamagePerSecond = 2f;
    public float catchWindowDuration = 0.5f;

    [Header("Network Settings")]
    public float interpolationDelay = 0.1f;
    public int maxPing = 200;

    [Header("Mobile Settings")]
    public float mobileInputSensitivity = 1.5f;
    public bool enableVibration = true;
}