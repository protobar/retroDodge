using UnityEngine;
using Photon.Pun;
using System.Collections;

// ==================== CHARACTER STATS SCRIPTABLE OBJECT ====================
[CreateAssetMenu(fileName = "CharacterStats", menuName = "Game/Character Stats")]
public class CharacterStats : ScriptableObject
{
    [Header("Basic Stats")]
    public float maxHealth = 100f;
    public float movementSpeed = 5.0f;
    public float jumpHeight = 4.0f;
    public float damageMultiplier = 1.0f;
    public float throwPowerMultiplier = 1.0f;

    [Header("Character Info")]
    public string characterName;
    public string description;
    public Sprite characterIcon;

    [Header("Special Abilities")]
    public bool hasAirDash = false;
    public float airDashCooldown = 2.0f;
    public int ultimateDamage = 25;
}