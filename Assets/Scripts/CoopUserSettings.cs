using System;
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
    private const int WindowedWidth = 1280;
    private const int WindowedHeight = 720;
    private const float RuntimeSyncDelaySeconds = 0.75f;

    public const float MinMouseSensitivity = 0.1f;
    public const float MaxMouseSensitivity = 5f;

    private const CoopScreenMode DefaultScreenMode = CoopScreenMode.Windowed;
    private const bool DefaultShowPing = true;
    private const float DefaultMouseSensitivity = 1f;

    private static bool _isLoaded;
    private static CoopScreenMode _screenMode = DefaultScreenMode;
    private static bool _showPing = DefaultShowPing;
    private static float _mouseSensitivity = DefaultMouseSensitivity;
    private static float _runtimeSyncBlockedUntil = -1f;

    public static event Action<CoopScreenMode> ScreenModeChanged;

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
        SetScreenModeInternal(ValidateScreenMode(mode), true);
    }

    public static void SyncScreenModeWithRuntimeWindow()
    {
        EnsureLoaded();

#if UNITY_EDITOR
        return;
#else
        if (Time.unscaledTime < _runtimeSyncBlockedUntil)
        {
            return;
        }

        CoopScreenMode runtimeMode = GetRuntimeScreenMode();
        if (runtimeMode == _screenMode)
        {
            return;
        }

        SetScreenModeInternal(runtimeMode, true);
#endif
    }

    private static void SetScreenModeInternal(CoopScreenMode mode, bool apply)
    {
        bool changed = _screenMode != mode;
        _screenMode = mode;
        PlayerPrefs.SetInt(ScreenModeKey, (int)_screenMode);
        PlayerPrefs.Save();

        if (apply)
        {
            ApplyScreenMode();
        }

        if (changed)
        {
            ScreenModeChanged?.Invoke(_screenMode);
        }
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
        _runtimeSyncBlockedUntil = Time.unscaledTime + RuntimeSyncDelaySeconds;

        if (_screenMode == CoopScreenMode.Fullscreen)
        {
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            Screen.fullScreen = true;
            return;
        }

        Screen.fullScreenMode = FullScreenMode.Windowed;
        Screen.fullScreen = false;
        Screen.SetResolution(WindowedWidth, WindowedHeight, FullScreenMode.Windowed);
    }

    private static CoopScreenMode GetRuntimeScreenMode()
    {
        if (Screen.fullScreen || Screen.fullScreenMode != FullScreenMode.Windowed)
        {
            return CoopScreenMode.Fullscreen;
        }

        if (IsWindowCoveringDisplay())
        {
            return CoopScreenMode.Fullscreen;
        }

        return CoopScreenMode.Windowed;
    }

    private static bool IsWindowCoveringDisplay()
    {
        if (Display.main == null || Display.main.systemWidth <= 0 || Display.main.systemHeight <= 0)
        {
            return false;
        }

        float widthRatio = Screen.width / (float)Display.main.systemWidth;
        float heightRatio = Screen.height / (float)Display.main.systemHeight;
        return widthRatio >= 0.98f && heightRatio >= 0.9f;
    }
}
