using UnityEngine;
using System;
using TMPro;

public class MusicPlayer : MonoBehaviour
{
    [Header("=== PSVita Music Player - Full SaveAndLoad Compatible (Unity 2018.2.19f1 + PSP2) ===")]
    [Tooltip("Drag all your music tracks here in the order you want")]
    public AudioClip[] playlist;

    [Tooltip("Default volume when no save exists")]
    [Range(0f, 1f)]
    public float defaultVolume = 0.7f;

    [Header("UI")]
    [Tooltip("Optional - assign a TextMeshProUGUI to display the current song name")]
    public TextMeshProUGUI songNameText;

    private AudioSource audioSource;
    private int currentTrackIndex = 0;
    private bool isPaused = false;

    private const string PREF_TRACK_INDEX = "Music_LastTrack";
    private const string PREF_VOLUME = "Music_Volume";

    public static MusicPlayer Instance { get; private set; }

    // ──────────────────────────────────────────────────────────────
    // EXACT INTERFACE EXPECTED BY SaveAndLoad.cs
    // ──────────────────────────────────────────────────────────────
    [Serializable]
    public class MusicSaveData
    {
        public int currentTrackIndex;
        public float volume;
        public float playbackTime;
    }

    public MusicSaveData GetSaveData()
    {
        MusicSaveData data = new MusicSaveData();
        data.currentTrackIndex = currentTrackIndex;
        data.volume = audioSource != null ? audioSource.volume : defaultVolume;
        data.playbackTime = audioSource != null ? audioSource.time : 0f;
        return data;
    }

    public void LoadSaveData(MusicSaveData data)
    {
        if (data == null) return;

        currentTrackIndex = Mathf.Clamp(data.currentTrackIndex, 0, playlist.Length - 1);
        if (audioSource != null)
        {
            audioSource.volume = data.volume;
            if (audioSource.clip != null)
                audioSource.time = data.playbackTime;
        }

        SaveMusicState();
    }

    // Settings.cs compatibility
    public float Volume
    {
        get { return audioSource != null ? audioSource.volume : defaultVolume; }
        set
        {
            if (audioSource != null)
            {
                audioSource.volume = Mathf.Clamp01(value);
                SaveMusicState();
            }
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = false;
        audioSource.playOnAwake = false;

        LoadSavedMusicState();
    }

    private void Start()
    {
        if (playlist.Length == 0)
        {
            Debug.LogWarning("[MusicPlayer] No tracks in playlist!");
            return;
        }

        PlayTrack(currentTrackIndex, true);
    }

    private void Update()
    {
        if (!isPaused && audioSource.isPlaying == false && playlist.Length > 0)
        {
            NextTrack();
        }
    }

    private void LoadSavedMusicState()
    {
        currentTrackIndex = PlayerPrefs.GetInt(PREF_TRACK_INDEX, 0);
        float savedVolume = PlayerPrefs.GetFloat(PREF_VOLUME, defaultVolume);

        currentTrackIndex = Mathf.Clamp(currentTrackIndex, 0, playlist.Length - 1);
        if (audioSource != null)
            audioSource.volume = savedVolume;
    }

    private void SaveMusicState()
    {
        PlayerPrefs.SetInt(PREF_TRACK_INDEX, currentTrackIndex);
        PlayerPrefs.SetFloat(PREF_VOLUME, audioSource.volume);
        PlayerPrefs.Save();
    }

    public void PlayTrack(int index, bool resumeTime = false)
    {
        if (playlist.Length == 0) return;

        currentTrackIndex = Mathf.Clamp(index, 0, playlist.Length - 1);
        audioSource.clip = playlist[currentTrackIndex];

        if (resumeTime && PlayerPrefs.HasKey("Music_LastTime"))
        {
            audioSource.time = PlayerPrefs.GetFloat("Music_LastTime", 0f);
        }
        else
        {
            audioSource.time = 0f;
        }

        audioSource.Play();
        isPaused = false;
        SaveMusicState();
        UpdateSongNameUI();
    }

    private void UpdateSongNameUI()
    {
        if (songNameText == null) return;
        if (audioSource.clip != null)
            songNameText.text = audioSource.clip.name;
        else
            songNameText.text = string.Empty;
    }

    public void NextTrack()
    {
        PlayTrack((currentTrackIndex + 1) % playlist.Length);
    }

    public void PreviousTrack()
    {
        int prev = currentTrackIndex - 1;
        if (prev < 0) prev = playlist.Length - 1;
        PlayTrack(prev);
    }

    public void SetVolume(float volume)
    {
        Volume = volume;
    }

    public void TogglePause()
    {
        if (isPaused)
        {
            audioSource.UnPause();
            isPaused = false;
        }
        else
        {
            audioSource.Pause();
            isPaused = true;
            PlayerPrefs.SetFloat("Music_LastTime", audioSource.time);
            SaveMusicState();
        }
    }

    public void StopMusic()
    {
        audioSource.Stop();
        isPaused = true;
    }

    public void Play() { if (audioSource != null) audioSource.Play(); }
    public void Pause() { if (audioSource != null) audioSource.Pause(); }
}