using UnityEngine;

public static class CoopAudioSettings
{
    private const string MasterVolumeKey = "Audio.MasterVolume";
    private const string MusicVolumeKey = "Audio.MusicVolume";
    private const string InteractionVolumeKey = "Audio.InteractionVolume";
    private const float DefaultVolume = 1f;

    private static bool _isLoaded;
    private static float _masterVolume = DefaultVolume;
    private static float _musicVolume = DefaultVolume;
    private static float _interactionVolume = DefaultVolume;

    public static float MasterVolume
    {
        get
        {
            EnsureLoaded();
            return _masterVolume;
        }
    }

    public static float MusicVolume
    {
        get
        {
            EnsureLoaded();
            return _musicVolume;
        }
    }

    public static float InteractionVolume
    {
        get
        {
            EnsureLoaded();
            return _interactionVolume;
        }
    }

    public static void SetMasterVolume(float value)
    {
        EnsureLoaded();
        _masterVolume = Mathf.Clamp01(value);
        AudioListener.volume = _masterVolume;
        PlayerPrefs.SetFloat(MasterVolumeKey, _masterVolume);
        PlayerPrefs.Save();
        AudioController.Instance.RefreshVolumes();
    }

    public static void SetMusicVolume(float value)
    {
        EnsureLoaded();
        _musicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicVolumeKey, _musicVolume);
        PlayerPrefs.Save();
        AudioController.Instance.RefreshVolumes();
    }

    public static void SetInteractionVolume(float value)
    {
        EnsureLoaded();
        _interactionVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(InteractionVolumeKey, _interactionVolume);
        PlayerPrefs.Save();
        AudioController.Instance.RefreshVolumes();
    }

    public static void Apply()
    {
        EnsureLoaded();
        AudioListener.volume = _masterVolume;
    }

    private static void EnsureLoaded()
    {
        if (_isLoaded)
        {
            return;
        }

        _masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, DefaultVolume));
        _musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, DefaultVolume));
        _interactionVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(InteractionVolumeKey, DefaultVolume));
        AudioListener.volume = _masterVolume;
        _isLoaded = true;
    }
}
