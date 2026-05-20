using System;

public static class SettingsManager
{
    public static event Action<float> MouseSensitivityChanged;

    public static float MouseSensitivity => CoopUserSettings.MouseSensitivity;
    public static float MinMouseSensitivity => CoopUserSettings.MinMouseSensitivity;
    public static float MaxMouseSensitivity => CoopUserSettings.MaxMouseSensitivity;

    public static void SetMouseSensitivity(float value)
    {
        float previousValue = CoopUserSettings.MouseSensitivity;
        CoopUserSettings.SetMouseSensitivity(value);
        float currentValue = CoopUserSettings.MouseSensitivity;

        if (!UnityEngine.Mathf.Approximately(previousValue, currentValue))
        {
            MouseSensitivityChanged?.Invoke(currentValue);
        }
    }
}
