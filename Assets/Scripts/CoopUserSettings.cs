using UnityEngine;

public enum CoopScreenMode
{
    Windowed = 0,
    Fullscreen = 1
}

public static class CoopUserSettings
{
    private const string ScreenModeKey = "Settings.ScreenMode";
    private const string ShowPingKey = "Settings.ShowPing";
    private const string MouseSensitivityKey = "Settings.MouseSensitivity";

    public const float MinMouseSensitivity = 0.1f;
    public const float MaxMouseSensitivity = 5f;

    private const CoopScreenMode DefaultScreenMode = CoopScreenMode.Windowed;
    private const bool DefaultShowPing = true;
    private const float DefaultMouseSensitivity = 1f;

    private static bool _isLoaded;
    private static CoopScreenMode _screenMode = DefaultScreenMode;
    private static bool _showPing = DefaultShowPing;
    private static float _mouseSensitivity = DefaultMouseSensitivity;

    public static CoopScreenMode ScreenMode
    {
        get
        {
            EnsureLoaded();
            return _screenMode;
        }
    }

    public static bool ShowPing
    {
        get
        {
            EnsureLoaded();
            return _showPing;
        }
    }

    public static float MouseSensitivity
    {
        get
        {
            EnsureLoaded();
            return _mouseSensitivity;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        ApplyAll();
    }

    public static void ApplyAll()
    {
        EnsureLoaded();
        ApplyScreenMode();
    }

    public static void SetScreenMode(CoopScreenMode mode)
    {
        EnsureLoaded();
        _screenMode = ValidateScreenMode(mode);
        PlayerPrefs.SetInt(ScreenModeKey, (int)_screenMode);
        PlayerPrefs.Save();
        ApplyScreenMode();
    }

    public static void SetShowPing(bool value)
    {
        EnsureLoaded();
        _showPing = value;
        PlayerPrefs.SetInt(ShowPingKey, _showPing ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void SetMouseSensitivity(float value)
    {
        EnsureLoaded();
        _mouseSensitivity = Mathf.Clamp(value, MinMouseSensitivity, MaxMouseSensitivity);
        PlayerPrefs.SetFloat(MouseSensitivityKey, _mouseSensitivity);
        PlayerPrefs.Save();
    }

    public static string GetScreenModeLabel()
    {
        return ScreenMode == CoopScreenMode.Fullscreen ? "Fullscreen" : "Windowed";
    }

    private static void EnsureLoaded()
    {
        if (_isLoaded)
        {
            return;
        }

        _screenMode = ValidateScreenMode((CoopScreenMode)PlayerPrefs.GetInt(ScreenModeKey, (int)DefaultScreenMode));
        _showPing = PlayerPrefs.GetInt(ShowPingKey, DefaultShowPing ? 1 : 0) != 0;
        _mouseSensitivity = Mathf.Clamp(
            PlayerPrefs.GetFloat(MouseSensitivityKey, DefaultMouseSensitivity),
            MinMouseSensitivity,
            MaxMouseSensitivity);

        _isLoaded = true;
    }

    private static CoopScreenMode ValidateScreenMode(CoopScreenMode mode)
    {
        return mode == CoopScreenMode.Fullscreen ? CoopScreenMode.Fullscreen : CoopScreenMode.Windowed;
    }

    private static void ApplyScreenMode()
    {
        if (_screenMode == CoopScreenMode.Fullscreen)
        {
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            Screen.fullScreen = true;
            return;
        }

        Screen.fullScreenMode = FullScreenMode.Windowed;
        Screen.fullScreen = false;
        Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
    }
}
