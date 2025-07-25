using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== NETWORK MANAGER ====================
public class NetworkManager : MonoBehaviourPunCallbacks
{
    private static NetworkManager instance;
    public static NetworkManager Instance => instance;

    [Header("Network Settings")]
    public GameObject playerPrefab;
    public Transform[] spawnPoints;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        // Configure Photon settings
        PhotonNetwork.SendRate = 30;
        PhotonNetwork.SerializationRate = 15;
    }

    public override void OnJoinedRoom()
    {
        SpawnPlayer();
    }

    void SpawnPlayer()
    {
        if (playerPrefab != null && spawnPoints.Length > 0)
        {
            int spawnIndex = PhotonNetwork.LocalPlayer.ActorNumber - 1;
            spawnIndex = Mathf.Clamp(spawnIndex, 0, spawnPoints.Length - 1);

            Vector3 spawnPos = spawnPoints[spawnIndex].position;

            GameObject player = PhotonNetwork.Instantiate(playerPrefab.name, spawnPos, spawnPoints[spawnIndex].rotation);

            // Register with GameManager
            CharacterBase character = player.GetComponent<CharacterBase>();
            if (character != null)
            {
                GameManager.Instance.RegisterPlayer(character);
            }
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"Player {newPlayer.NickName} joined the room");
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"Player {otherPlayer.NickName} left the room");
    }
}