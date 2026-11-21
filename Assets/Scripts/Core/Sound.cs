using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sonidos y música del juego (archivos en Resources/Audio/). Se crea solo la
/// primera vez que se usa y vive entre escenas. Música y efectos se pueden
/// apagar por separado en Ajustes.
/// </summary>
public class Sound : MonoBehaviour
{
    public enum Track { None, Menu, Game }

    private const string MusicKey = "crystalclash.sound.music";
    private const string SfxKey = "crystalclash.sound.sfx";
    private const float MusicVolume = 0.35f;
    private const int Voices = 10;

    private static Sound instance;
    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private readonly List<AudioSource> voices = new List<AudioSource>();
    private readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
    private AudioSource music;
    private Track currentTrack = Track.None;
    private int nextVoice;

    public static bool MusicOn
    {
        get => PlayerPrefs.GetInt(MusicKey, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(MusicKey, value ? 1 : 0);
            PlayerPrefs.Save();
            Instance.ApplyMusic();
        }
    }

    public static bool SfxOn
    {
        get => PlayerPrefs.GetInt(SfxKey, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(SfxKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    private static Sound Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("Sonido");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Sound>();
                instance.Setup();
            }
            return instance;
        }
    }

    private void Setup()
    {
        music = gameObject.AddComponent<AudioSource>();
        music.loop = true;
        music.playOnAwake = false;
        music.volume = MusicVolume;

        for (int i = 0; i < Voices; i++)
        {
            var voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voices.Add(voice);
        }
    }

    // ------------------------------------------------------------------
    // Efectos
    // ------------------------------------------------------------------

    /// <summary>Toca un efecto ("match", "bomb"...). pitch cambia el tono (1 = normal).</summary>
    public static void Play(string name, float pitch = 1f, float volume = 1f)
    {
        if (!SfxOn) return;
        Instance.PlayInternal(name, pitch, volume);
    }

    private void PlayInternal(string name, float pitch, float volume)
    {
        // El mismo sonido muchas veces en el mismo instante suena feo: uno basta.
        if (lastPlayed.TryGetValue(name, out float last) && Time.unscaledTime - last < 0.04f) return;
        lastPlayed[name] = Time.unscaledTime;

        AudioClip clip = Clip(name);
        if (clip == null) return;

        AudioSource voice = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Count;
        voice.pitch = pitch;
        voice.volume = volume;
        voice.clip = clip;
        voice.Play();
    }

    public static void Click() => Play("click", 1f, 0.7f);

    /// <summary>Agrega el sonido de clic a un botón.</summary>
    public static void AddClick(Button button)
    {
        if (button != null) button.onClick.AddListener(Click);
    }

    // ------------------------------------------------------------------
    // Música
    // ------------------------------------------------------------------

    /// <summary>Cambia la música (si ya suena esa, no la reinicia).</summary>
    public static void PlayMusic(Track track)
    {
        Instance.SetTrack(track);
    }

    private void SetTrack(Track track)
    {
        if (track == currentTrack && (music.isPlaying || !MusicOn)) return;
        currentTrack = track;
        ApplyMusic();
    }

    private void ApplyMusic()
    {
        if (!MusicOn || currentTrack == Track.None)
        {
            music.Stop();
            return;
        }

        AudioClip clip = Clip(currentTrack == Track.Menu ? "music_menu" : "music_game");
        if (clip == null) return;
        if (music.clip != clip || !music.isPlaying)
        {
            music.clip = clip;
            music.Play();
        }
    }

    private AudioClip Clip(string name)
    {
        if (!clips.TryGetValue(name, out AudioClip clip))
        {
            clip = Resources.Load<AudioClip>("Audio/" + name);
            clips[name] = clip;
            if (clip == null) Debug.LogWarning("Sonido no encontrado: Audio/" + name);
        }
        return clip;
    }
}
