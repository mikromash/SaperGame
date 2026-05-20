using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineInputAxisController))]
public sealed class MouseSensitivityApplier : MonoBehaviour
{
    private readonly Dictionary<object, float> _baseInputGains = new Dictionary<object, float>();
    private readonly Dictionary<object, float> _baseLegacyGains = new Dictionary<object, float>();
    private CinemachineInputAxisController _axisController;

    private void Awake()
    {
        _axisController = GetComponent<CinemachineInputAxisController>();
    }

    private void OnEnable()
    {
        if (_axisController == null)
        {
            _axisController = GetComponent<CinemachineInputAxisController>();
        }

        _axisController.SynchronizeControllers();
        SettingsManager.MouseSensitivityChanged += ApplySensitivity;
        ApplySensitivity(SettingsManager.MouseSensitivity);
    }

    private void OnDisable()
    {
        SettingsManager.MouseSensitivityChanged -= ApplySensitivity;
    }

    private void ApplySensitivity(float sensitivity)
    {
        if (_axisController == null)
        {
            return;
        }

        foreach (var controller in _axisController.Controllers)
        {
            if (controller == null || controller.Input == null || !IsMouseLookAxis(controller.Name))
            {
                continue;
            }

            if (!_baseInputGains.TryGetValue(controller, out float baseGain))
            {
                baseGain = controller.Input.Gain;
                _baseInputGains.Add(controller, baseGain);
            }

            controller.Input.Gain = baseGain * sensitivity;

#if ENABLE_LEGACY_INPUT_MANAGER
            if (!_baseLegacyGains.TryGetValue(controller, out float baseLegacyGain))
            {
                baseLegacyGain = controller.Input.LegacyGain;
                _baseLegacyGains.Add(controller, baseLegacyGain);
            }

            controller.Input.LegacyGain = baseLegacyGain * sensitivity;
#endif
        }
    }

    private static bool IsMouseLookAxis(string axisName)
    {
        return !string.IsNullOrEmpty(axisName) &&
               axisName.IndexOf("Look", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
