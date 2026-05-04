using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class AudioController : MonoBehaviour
{
    private const string LibraryResourcePath = "CoopAudioLibrary";
    private const string LobbySceneName = "LobbyScene";
    private const string GameplaySceneName = "GameplayScene";
    private const int SfxSourceCount = 12;
    private const float DefaultFadeSeconds = 0.6f;

    private static AudioController _instance;

    [SerializeField] private AudioLibrary library;
    [SerializeField] private int sfxSourceCount = SfxSourceCount;

    private readonly Dictionary<AudioEvent, float> _lastPlayTimes = new Dictionary<AudioEvent, float>();
    private readonly List<AudioSource> _sfxSources = new List<AudioSource>();
    private AudioSource _musicSource;
    private Coroutine _musicFadeRoutine;
    private MusicTrack _currentTrack = MusicTrack.None;
    private float _musicBaseVolume = 1f;
    private int _nextSfxSourceIndex;

    public static AudioController Instance
    {
        get
        {
            EnsureInstance();
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static void Play(AudioEvent eventId)
    {
        Instance.PlaySfx(eventId);
    }

    public static void PlayAt(AudioEvent eventId, Vector3 position)
    {
        Instance.PlaySfx(eventId, position);
    }

    public static void PlayMusicTrack(MusicTrack track)
    {
        Instance.PlayMusic(track);
    }

    public void PlaySfx(AudioEvent eventId)
    {
        PlaySfx(eventId, null);
    }

    public void PlaySfx(AudioEvent eventId, Vector3 position)
    {
        PlaySfx(eventId, (Vector3?)position);
    }

    public void PlayMusic(MusicTrack track, float fadeSeconds = DefaultFadeSeconds)
    {
        if (_currentTrack == track)
        {
            RefreshVolumes();
            return;
        }

        _currentTrack = track;

        if (track == MusicTrack.None || library == null || !library.TryGetMusic(track, out MusicClipSettings settings) || settings.clip == null)
        {
            FadeToClip(null, 0f, false, fadeSeconds);
            return;
        }

        FadeToClip(settings.clip, settings.volume, settings.loop, fadeSeconds);
    }

    public void RefreshVolumes()
    {
        CoopAudioSettings.Apply();

        if (_musicSource != null)
        {
            _musicSource.volume = _musicBaseVolume * CoopAudioSettings.MusicVolume;
        }
    }

    private static void EnsureInstance()
    {
        if (_instance != null)
        {
            return;
        }

        AudioController existing = FindAnyObjectByType<AudioController>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        GameObject root = new GameObject("AudioController");
        _instance = root.AddComponent<AudioController>();
        DontDestroyOnLoad(root);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        LoadLibraryIfNeeded();
        CreateAudioSources();
        CoopAudioSettings.Apply();
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            _instance = null;
        }
    }

    private void LoadLibraryIfNeeded()
    {
        if (library == null)
        {
            library = Resources.Load<AudioLibrary>(LibraryResourcePath);
        }
    }

    private void CreateAudioSources()
    {
        if (_musicSource == null)
        {
            GameObject musicObject = new GameObject("MusicSource");
            musicObject.transform.SetParent(transform, false);
            _musicSource = musicObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.spatialBlend = 0f;
        }

        if (_sfxSources.Count > 0)
        {
            return;
        }

        int count = Mathf.Max(1, sfxSourceCount);
        for (int index = 0; index < count; index++)
        {
            GameObject sourceObject = new GameObject("SfxSource " + (index + 1));
            sourceObject.transform.SetParent(transform, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            _sfxSources.Add(source);
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshVolumes();

        if (string.Equals(scene.name, GameplaySceneName, System.StringComparison.Ordinal))
        {
            PlayMusic(MusicTrack.Gameplay);
            return;
        }

        if (string.Equals(scene.name, LobbySceneName, System.StringComparison.Ordinal))
        {
            PlayMusic(MusicTrack.Lobby);
        }
    }

    private void PlaySfx(AudioEvent eventId, Vector3? position)
    {
        if (library == null || !library.TryGetSound(eventId, out AudioClipSettings settings))
        {
            return;
        }

        AudioClip clip = PickClip(settings);
        if (clip == null || IsCoolingDown(eventId, settings.cooldown))
        {
            return;
        }

        AudioSource source = GetNextSfxSource();
        source.transform.position = position ?? transform.position;
        source.spatialBlend = settings.spatial && position.HasValue ? 1f : 0f;
        source.pitch = 1f + Random.Range(-settings.pitchJitter, settings.pitchJitter);
        source.volume = settings.volume * CoopAudioSettings.InteractionVolume;
        source.PlayOneShot(clip);
        _lastPlayTimes[eventId] = Time.unscaledTime;
    }

    private bool IsCoolingDown(AudioEvent eventId, float cooldown)
    {
        if (cooldown <= 0f || !_lastPlayTimes.TryGetValue(eventId, out float lastTime))
        {
            return false;
        }

        return Time.unscaledTime - lastTime < cooldown;
    }

    private AudioClip PickClip(AudioClipSettings settings)
    {
        if (settings.clips == null || settings.clips.Length == 0)
        {
            return null;
        }

        int startIndex = Random.Range(0, settings.clips.Length);
        for (int offset = 0; offset < settings.clips.Length; offset++)
        {
            AudioClip clip = settings.clips[(startIndex + offset) % settings.clips.Length];
            if (clip != null)
            {
                return clip;
            }
        }

        return null;
    }

    private AudioSource GetNextSfxSource()
    {
        AudioSource source = _sfxSources[_nextSfxSourceIndex];
        _nextSfxSourceIndex = (_nextSfxSourceIndex + 1) % _sfxSources.Count;
        return source;
    }

    private void FadeToClip(AudioClip clip, float baseVolume, bool loop, float fadeSeconds)
    {
        if (_musicFadeRoutine != null)
        {
            StopCoroutine(_musicFadeRoutine);
        }

        _musicFadeRoutine = StartCoroutine(FadeMusicRoutine(clip, baseVolume, loop, Mathf.Max(0f, fadeSeconds)));
    }

    private IEnumerator FadeMusicRoutine(AudioClip clip, float baseVolume, bool loop, float fadeSeconds)
    {
        _musicBaseVolume = baseVolume;
        float startVolume = _musicSource.volume;
        float elapsed = 0f;

        while (elapsed < fadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            _musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeSeconds);
            yield return null;
        }

        _musicSource.Stop();
        _musicSource.clip = clip;
        _musicSource.loop = loop;

        if (clip == null)
        {
            _musicSource.volume = 0f;
            yield break;
        }

        _musicSource.Play();

        float targetVolume = baseVolume * CoopAudioSettings.MusicVolume;
        elapsed = 0f;

        while (elapsed < fadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            _musicSource.volume = Mathf.Lerp(0f, targetVolume, elapsed / fadeSeconds);
            yield return null;
        }

        _musicSource.volume = targetVolume;
    }
}
