using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;

/// <summary>
/// Centralized AudioManager for Retro Dodge Rumble
/// Handles audio arrays, random selection, and Photon networking
/// </summary>
public class AudioManager : MonoBehaviourPunCallbacks
{
    private static AudioManager instance;
    public static AudioManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("AudioManager");
                instance = go.AddComponent<AudioManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    [Header("Audio Settings")]
    [SerializeField] private float masterVolume = 1f;
    [SerializeField] private float sfxVolume = 0.8f;
    [SerializeField] private float musicVolume = 0.6f;
    [SerializeField] private int maxSimultaneousSounds = 16;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource masterAudioSource;
    [SerializeField] private AudioSource sfxAudioSource;
    [SerializeField] private AudioSource musicAudioSource;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    // Audio tracking
    private List<AudioSource> activeAudioSources = new List<AudioSource>();
    private Dictionary<string, float> lastPlayTime = new Dictionary<string, float>();
    private float audioCooldown = 0.1f; // Prevent audio spam

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioManager();
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    void InitializeAudioManager()
    {
        // Create audio sources if they don't exist
        if (masterAudioSource == null)
        {
            masterAudioSource = gameObject.AddComponent<AudioSource>();
            masterAudioSource.playOnAwake = false;
            masterAudioSource.volume = masterVolume;
        }

        if (sfxAudioSource == null)
        {
            sfxAudioSource = gameObject.AddComponent<AudioSource>();
            sfxAudioSource.playOnAwake = false;
            sfxAudioSource.volume = sfxVolume;
        }

        if (musicAudioSource == null)
        {
            musicAudioSource = gameObject.AddComponent<AudioSource>();
            musicAudioSource.playOnAwake = false;
            musicAudioSource.volume = musicVolume;
            musicAudioSource.loop = true;
        }

        // Load saved volume settings
        LoadVolumeSettings();

        if (debugMode)
        {
            Debug.Log("[AudioManager] Initialized with master, SFX, and music audio sources");
        }
    }

    /// <summary>
    /// Load volume settings from PlayerPrefs
    /// </summary>
    void LoadVolumeSettings()
    {
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 0.8f);
        musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.6f);

        // Apply loaded settings
        if (masterAudioSource != null) masterAudioSource.volume = masterVolume;
        if (sfxAudioSource != null) sfxAudioSource.volume = sfxVolume;
        if (musicAudioSource != null) musicAudioSource.volume = musicVolume;
    }

    /// <summary>
    /// Play random sound from array with Photon networking support
    /// </summary>
    public void PlayRandomSound(AudioClip[] audioArray, AudioType audioType = AudioType.SFX, bool networkSync = true)
    {
        if (audioArray == null || audioArray.Length == 0) return;

        // Filter out null entries
        var validClips = new List<AudioClip>();
        foreach (var clip in audioArray)
        {
            if (clip != null) validClips.Add(clip);
        }

        if (validClips.Count == 0) return;

        AudioClip randomClip = validClips[Random.Range(0, validClips.Count)];
        PlaySound(randomClip, audioType, networkSync);
    }

    /// <summary>
    /// Play single sound with Photon networking support
    /// </summary>
    public void PlaySound(AudioClip clip, AudioType audioType = AudioType.SFX, bool networkSync = true)
    {
        if (clip == null) return;

        // Check cooldown to prevent audio spam
        string clipName = clip.name;
        if (lastPlayTime.ContainsKey(clipName) && Time.time - lastPlayTime[clipName] < audioCooldown)
        {
            return;
        }
        lastPlayTime[clipName] = Time.time;

        // Network sync for multiplayer
        if (networkSync && !PhotonNetwork.OfflineMode && PhotonNetwork.IsConnected)
        {
            photonView.RPC("PlaySoundRPC", RpcTarget.All, clipName, (int)audioType);
        }
        else
        {
            PlaySoundLocal(clip, audioType);
        }
    }

    /// <summary>
    /// Play sound locally without network sync
    /// </summary>
    public void PlaySoundLocal(AudioClip clip, AudioType audioType = AudioType.SFX)
    {
        if (clip == null) return;

        AudioSource targetSource = GetAudioSource(audioType);
        if (targetSource != null)
        {
            // Manage simultaneous sounds
            if (activeAudioSources.Count >= maxSimultaneousSounds)
            {
                // Stop oldest sound
                AudioSource oldestSource = activeAudioSources[0];
                if (oldestSource != null && oldestSource.isPlaying)
                {
                    oldestSource.Stop();
                }
                activeAudioSources.RemoveAt(0);
            }

            targetSource.PlayOneShot(clip);
            activeAudioSources.Add(targetSource);

            if (debugMode)
            {
                Debug.Log($"[AudioManager] Playing {clip.name} on {audioType} source");
            }
        }
    }

    /// <summary>
    /// Play announcement sound with network sync
    /// </summary>
    public void PlayAnnouncement(AudioClip[] announcementArray, bool networkSync = true)
    {
        if (announcementArray == null || announcementArray.Length == 0) return;

        // Filter out null entries
        var validClips = new List<AudioClip>();
        foreach (var clip in announcementArray)
        {
            if (clip != null) validClips.Add(clip);
        }

        if (validClips.Count == 0) return;

        AudioClip randomClip = validClips[Random.Range(0, validClips.Count)];
        
        if (networkSync && !PhotonNetwork.OfflineMode && PhotonNetwork.IsConnected)
        {
            photonView.RPC("PlayAnnouncementRPC", RpcTarget.All, randomClip.name);
        }
        else
        {
            PlaySoundLocal(randomClip, AudioType.Announcement);
        }
    }

    /// <summary>
    /// Set volume for specific audio type
    /// </summary>
    public void SetVolume(AudioType audioType, float volume)
    {
        volume = Mathf.Clamp01(volume);

        switch (audioType)
        {
            case AudioType.Master:
                masterVolume = volume;
                if (masterAudioSource != null) masterAudioSource.volume = volume;
                PlayerPrefs.SetFloat("MasterVolume", volume);
                break;
            case AudioType.SFX:
                sfxVolume = volume;
                if (sfxAudioSource != null) sfxAudioSource.volume = volume;
                PlayerPrefs.SetFloat("SFXVolume", volume);
                break;
            case AudioType.Music:
                musicVolume = volume;
                if (musicAudioSource != null) musicAudioSource.volume = volume;
                PlayerPrefs.SetFloat("MusicVolume", volume);
                break;
            case AudioType.Announcement:
                // Announcements use SFX volume
                if (sfxAudioSource != null) sfxAudioSource.volume = volume;
                break;
        }

        PlayerPrefs.Save();
    }

    /// <summary>
    /// Get current volume for audio type
    /// </summary>
    public float GetVolume(AudioType audioType)
    {
        switch (audioType)
        {
            case AudioType.Master: return masterVolume;
            case AudioType.SFX: return sfxVolume;
            case AudioType.Music: return musicVolume;
            case AudioType.Announcement: return sfxVolume;
            default: return 1f;
        }
    }

    /// <summary>
    /// Stop all sounds of specific type
    /// </summary>
    public void StopAllSounds(AudioType audioType = AudioType.All)
    {
        if (audioType == AudioType.All || audioType == AudioType.SFX)
        {
            if (sfxAudioSource != null) sfxAudioSource.Stop();
        }

        if (audioType == AudioType.All || audioType == AudioType.Music)
        {
            if (musicAudioSource != null) musicAudioSource.Stop();
        }

        if (audioType == AudioType.All || audioType == AudioType.Announcement)
        {
            // Announcements use SFX source
            if (sfxAudioSource != null) sfxAudioSource.Stop();
        }

        activeAudioSources.Clear();
    }

    /// <summary>
    /// Get appropriate audio source for type
    /// </summary>
    private AudioSource GetAudioSource(AudioType audioType)
    {
        switch (audioType)
        {
            case AudioType.SFX:
            case AudioType.Announcement:
                return sfxAudioSource;
            case AudioType.Music:
                return musicAudioSource;
            case AudioType.Master:
            default:
                return masterAudioSource;
        }
    }

    /// <summary>
    /// Clean up finished audio sources
    /// </summary>
    void Update()
    {
        // Remove finished audio sources from tracking
        for (int i = activeAudioSources.Count - 1; i >= 0; i--)
        {
            if (activeAudioSources[i] == null || !activeAudioSources[i].isPlaying)
            {
                activeAudioSources.RemoveAt(i);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // PHOTON NETWORKING RPCs
    // ═══════════════════════════════════════════════════════════════

    [PunRPC]
    void PlaySoundRPC(string clipName, int audioType)
    {
        AudioClip clip = Resources.Load<AudioClip>($"Audio/{clipName}");
        if (clip != null)
        {
            PlaySoundLocal(clip, (AudioType)audioType);
        }
        else if (debugMode)
        {
            Debug.LogWarning($"[AudioManager] Could not find audio clip: {clipName}");
        }
    }

    [PunRPC]
    void PlayAnnouncementRPC(string clipName)
    {
        AudioClip clip = Resources.Load<AudioClip>($"Audio/Announcements/{clipName}");
        if (clip != null)
        {
            PlaySoundLocal(clip, AudioType.Announcement);
        }
        else if (debugMode)
        {
            Debug.LogWarning($"[AudioManager] Could not find announcement clip: {clipName}");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // PUBLIC UTILITY METHODS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Play character sound with proper networking
    /// </summary>
    public void PlayCharacterSound(AudioClip[] soundArray, bool networkSync = true)
    {
        PlayRandomSound(soundArray, AudioType.SFX, networkSync);
    }

    /// <summary>
    /// Play ball sound with proper networking
    /// </summary>
    public void PlayBallSound(AudioClip[] soundArray, bool networkSync = true)
    {
        PlayRandomSound(soundArray, AudioType.SFX, networkSync);
    }

    /// <summary>
    /// Play UI sound (no network sync needed)
    /// </summary>
    public void PlayUISound(AudioClip clip)
    {
        PlaySound(clip, AudioType.SFX, false);
    }

    /// <summary>
    /// Play music with looping
    /// </summary>
    public void PlayMusic(AudioClip musicClip, bool loop = true)
    {
        if (musicAudioSource != null && musicClip != null)
        {
            musicAudioSource.clip = musicClip;
            musicAudioSource.loop = loop;
            musicAudioSource.Play();
        }
    }

    /// <summary>
    /// Stop music
    /// </summary>
    public void StopMusic()
    {
        if (musicAudioSource != null)
        {
            musicAudioSource.Stop();
        }
    }
}

/// <summary>
/// Audio types for the AudioManager
/// </summary>
public enum AudioType
{
    Master,
    SFX,
    Music,
    Announcement,
    All
}
