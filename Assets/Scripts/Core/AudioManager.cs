using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ==================== AUDIO MANAGER ====================
public class AudioManager : MonoBehaviour
{
    private static AudioManager instance;
    public static AudioManager Instance => instance;

    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource;
    public AudioSource uiSource;
    public AudioSource voiceSource;

    [Header("Music Clips")]
    public AudioClip mainMenuMusic;
    public AudioClip battleMusic;
    public AudioClip victoryMusic;
    public AudioClip defeatMusic;

    [Header("Sound Effects")]
    public Dictionary<string, AudioClip> soundEffects = new Dictionary<string, AudioClip>();

    [Header("Settings")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 0.7f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float uiVolume = 0.8f;
    [Range(0f, 1f)] public float voiceVolume = 1f;

    // Audio clip references for editor assignment
    [Header("SFX Clips")]
    public AudioClip[] sfxClips;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioManager();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        LoadAudioSettings();
        LoadAudioClips();

        // Start with main menu music
        if (mainMenuMusic != null)
        {
            PlayMusic(mainMenuMusic, 1f);
        }
    }

    void InitializeAudioManager()
    {
        // Create audio sources if they don't exist
        if (musicSource == null)
        {
            GameObject musicObj = new GameObject("MusicSource");
            musicObj.transform.SetParent(transform);
            musicSource = musicObj.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }

        if (sfxSource == null)
        {
            GameObject sfxObj = new GameObject("SFXSource");
            sfxObj.transform.SetParent(transform);
            sfxSource = sfxObj.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }

        if (uiSource == null)
        {
            GameObject uiObj = new GameObject("UISource");
            uiObj.transform.SetParent(transform);
            uiSource = uiObj.AddComponent<AudioSource>();
            uiSource.playOnAwake = false;
        }

        if (voiceSource == null)
        {
            GameObject voiceObj = new GameObject("VoiceSource");
            voiceObj.transform.SetParent(transform);
            voiceSource = voiceObj.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
        }
    }

    void LoadAudioSettings()
    {
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
        sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
        uiVolume = PlayerPrefs.GetFloat("UIVolume", 0.8f);
        voiceVolume = PlayerPrefs.GetFloat("VoiceVolume", 1f);

        UpdateAudioSourceVolumes();
    }

    void LoadAudioClips()
    {
        // Load sound effects from Resources folder
        AudioClip[] clips = Resources.LoadAll<AudioClip>("Audio/SFX");
        foreach (var clip in clips)
        {
            soundEffects[clip.name] = clip;
        }

        // Add manually assigned clips
        foreach (var clip in sfxClips)
        {
            if (clip != null)
            {
                soundEffects[clip.name] = clip;
            }
        }

        // Add common sound effect names if clips exist
        AddSoundEffect("Jump", "jump");
        AddSoundEffect("Throw", "throw");
        AddSoundEffect("Hit", "hit");
        AddSoundEffect("Catch", "catch");
        AddSoundEffect("Ultimate", "ultimate");
        AddSoundEffect("ButtonClick", "button_click");
        AddSoundEffect("CharacterSelect", "character_select");
        AddSoundEffect("BallWarning", "ball_warning");
        AddSoundEffect("Dodge", "dodge");
    }

    void AddSoundEffect(string key, string clipName)
    {
        AudioClip clip = Resources.Load<AudioClip>($"Audio/SFX/{clipName}");
        if (clip != null)
        {
            soundEffects[key] = clip;
        }
    }

    void UpdateAudioSourceVolumes()
    {
        if (musicSource != null)
            musicSource.volume = musicVolume * masterVolume;

        if (sfxSource != null)
            sfxSource.volume = sfxVolume * masterVolume;

        if (uiSource != null)
            uiSource.volume = uiVolume * masterVolume;

        if (voiceSource != null)
            voiceSource.volume = voiceVolume * masterVolume;
    }

    public void PlaySound(string soundName, float volumeScale = 1f)
    {
        if (soundEffects.ContainsKey(soundName) && sfxSource != null)
        {
            sfxSource.PlayOneShot(soundEffects[soundName], volumeScale);
        }
        else
        {
            Debug.LogWarning($"Sound effect '{soundName}' not found!");
        }
    }

    public void PlayUISound(string soundName, float volumeScale = 1f)
    {
        if (soundEffects.ContainsKey(soundName) && uiSource != null)
        {
            uiSource.PlayOneShot(soundEffects[soundName], volumeScale);
        }
    }

    public void PlayVoice(string voiceName, float volumeScale = 1f)
    {
        if (soundEffects.ContainsKey(voiceName) && voiceSource != null)
        {
            voiceSource.PlayOneShot(soundEffects[voiceName], volumeScale);
        }
    }

    public void PlayMusic(AudioClip musicClip, float fadeTime = 1f)
    {
        if (musicClip != null)
        {
            StartCoroutine(FadeMusic(musicClip, fadeTime));
        }
    }

    public void StopMusic(float fadeTime = 1f)
    {
        StartCoroutine(FadeOutMusic(fadeTime));
    }

    IEnumerator FadeMusic(AudioClip newClip, float fadeTime)
    {
        float elapsed = 0f;
        float startVolume = musicSource.volume;

        // Fade out current music
        while (elapsed < fadeTime / 2 && musicSource.isPlaying)
        {
            elapsed += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, 0, elapsed / (fadeTime / 2));
            yield return null;
        }

        // Switch track
        musicSource.clip = newClip;
        musicSource.Play();

        // Fade in new music
        elapsed = 0f;
        float targetVolume = musicVolume * masterVolume;
        while (elapsed < fadeTime / 2)
        {
            elapsed += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(0, targetVolume, elapsed / (fadeTime / 2));
            yield return null;
        }

        musicSource.volume = targetVolume;
    }

    IEnumerator FadeOutMusic(float fadeTime)
    {
        float elapsed = 0f;
        float startVolume = musicSource.volume;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, 0, elapsed / fadeTime);
            yield return null;
        }

        musicSource.Stop();
        musicSource.volume = startVolume;
    }

    // Public methods for volume control
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        UpdateAudioSourceVolumes();
        PlayerPrefs.SetFloat("MasterVolume", masterVolume);
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        UpdateAudioSourceVolumes();
        PlayerPrefs.SetFloat("MusicVolume", musicVolume);
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        UpdateAudioSourceVolumes();
        PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
    }

    // Game state music management
    public void PlayBattleMusic()
    {
        if (battleMusic != null)
        {
            PlayMusic(battleMusic, 2f);
        }
    }

    public void PlayVictoryMusic()
    {
        if (victoryMusic != null)
        {
            PlayMusic(victoryMusic, 1f);
        }
    }

    public void PlayDefeatMusic()
    {
        if (defeatMusic != null)
        {
            PlayMusic(defeatMusic, 1f);
        }
    }

    public void PlayMainMenuMusic()
    {
        if (mainMenuMusic != null)
        {
            PlayMusic(mainMenuMusic, 2f);
        }
    }
}