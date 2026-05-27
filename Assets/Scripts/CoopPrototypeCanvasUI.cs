using System.Collections; // Додано для Coroutine
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CoopPrototypeCanvasUI : MonoBehaviour
{
    private enum MenuWindow { Main, Settings, ConnectionType, Create, Join, WaitingRoom, InGame }

    [Header("Global UI Elements")]
    [SerializeField] private GameObject darkBackgroundOverlay; 

    [Header("Screens (Root Panels)")]
    [SerializeField] private GameObject mainMenuScreen;       
    [SerializeField] private GameObject settingsScreen;       
    [SerializeField] private GameObject connectionTypeScreen; 
    [SerializeField] private GameObject createRoomScreen;     
    [SerializeField] private GameObject joinRoomScreen;       
    [SerializeField] private GameObject waitingRoomScreen;    

    [Header("1. Main Menu")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsMenuButton;
    [SerializeField] private Button quitButton;

    [Header("2. Settings")]
    [SerializeField] private Slider bgSoundsSlider;
    [SerializeField] private Slider interactionSoundsSlider;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Dropdown screenModeDropdown;
    [SerializeField] private Toggle showPingToggle;
    [SerializeField] private Slider mouseSensitivitySlider;
    [SerializeField] private TMP_Text mouseSensitivityValueText;
    [SerializeField] private Button settingsBackButton; 

    [Header("3. Connection Type")]
    [SerializeField] private Button localConnButton;
    [SerializeField] private Button onlineConnButton; 
    [SerializeField] private Button createRoomModeButton;
    [SerializeField] private Button connectRoomModeButton;
    [SerializeField] private Button connTypeBackButton;

    [Header("4. Create Room")]
    [SerializeField] private TMP_Text createStatusText;    
    [SerializeField] private TMP_Text createModeLabelText; 
    [SerializeField] private TMP_InputField createPlayerNameInput;
    [SerializeField] private TMP_InputField createLocalHostAddressInput;
    [SerializeField] private TMP_InputField createPortInput;
    [SerializeField] private TMP_InputField createRoomNameInput;   
    [SerializeField] private TMP_InputField createRoomCodeInput;   
    [SerializeField] private TMP_InputField createPasswordInput;
    [SerializeField] private Toggle publicRoomToggle;              
    [SerializeField] private Toggle passwordRoomToggle;            
    [SerializeField] private Button startRoomButton;
    [SerializeField] private Button createBackButton;

    [Header("5. Join Room")]
    [SerializeField] private TMP_Text joinStatusText;      
    [SerializeField] private TMP_Text joinModeLabelText;
    [SerializeField] private TMP_InputField joinPlayerNameInput;
    [SerializeField] private TMP_InputField joinLocalHostAddressInput;
    [SerializeField] private TMP_InputField joinPortInput;
    [SerializeField] private TMP_InputField joinRoomCodeInput;     
    [SerializeField] private TMP_InputField joinPasswordInput;     
    [SerializeField] private Button connectButton;
    [SerializeField] private Button joinBackButton;

    // --- ОНОВЛЕНИЙ БЛОК ВІКНА ОЧІКУВАННЯ ---
    [Header("6. Waiting Room")]
    [SerializeField] private TMP_Text waitingRoomModeTitleText;   // ДОДАНО: Текст для "Network mode waiting room"
    [SerializeField] private GameObject waitingRoomNameContainer; // Контейнер або сам текст назви кімнати (щоб ховати в Local)
    [SerializeField] private TMP_Text waitingRoomNameText;        // Назва кімнати
    [SerializeField] private TMP_Text waitingRoomCodeText;        // Код кімнати
    [SerializeField] private TMP_Text waitingRoomAccessText;      // Access (Public/Private)
    
    [SerializeField] private Button waitingRoomCopyCodeButton;    // Кнопка скопіювати код
    [SerializeField] private TMP_Text waitingRoomCopyButtonText;  // Текст всередині кнопки копіювання ("Copy" -> "Скопійовано")

    [SerializeField] private Button waitingRoomStartButton;       // Кнопка керування грою
    [SerializeField] private TMP_Text waitingRoomStartButtonText; // Текст всередині кнопки керування ("Очікування" / "Почати гру")
    
    [SerializeField] private Button waitingRoomExitButton;        // Кнопка "Вийти"

    private CoopPrototypeController _controller;
    private MenuWindow _currentWindow = MenuWindow.Main;
    
    private string _originalCopyButtonText = "Copy"; // Збереження оригінального тексту кнопки копіювання

    private void Awake()
    {
        CoopAudioSettings.Apply();
        CoopUserSettings.ApplyAll();
        CoopUserSettings.ScreenModeChanged += HandleScreenModeChanged;
        EnsureRuntimeSettingsControls();
        RefreshSettingsControls();
        BindListeners();
        InitializeToggles(); 
    }

    private void Start()
    {
        TryBindController();
        ChangeWindow(MenuWindow.Main); 
    }

    private void Update()
    {
        TryBindController();
        
        if (_controller != null)
        {
            UpdateStatusText();

            if (_controller.IsInGame)
            {
                ChangeWindow(MenuWindow.InGame);
            }
            else if (_controller.IsWaitingRoom)
            {
                ChangeWindow(MenuWindow.WaitingRoom);
                UpdateWaitingRoomUI();
            }
            else if (_currentWindow == MenuWindow.WaitingRoom || _currentWindow == MenuWindow.InGame)
            {
                ChangeWindow(MenuWindow.Main);
            }
        }
    }

    private void TryBindController()
    {
        if (_controller != null) return;
        
        _controller = CoopPrototypeController.Instance;
        if (_controller == null) _controller = FindAnyObjectByType<CoopPrototypeController>();
        
        if (_controller != null) _controller.AttachCanvasUi();
    }

    private void OnDestroy()
    {
        CoopUserSettings.ScreenModeChanged -= HandleScreenModeChanged;
        if (_controller != null) _controller.DetachCanvasUi();
    }

    private void HandleScreenModeChanged(CoopScreenMode mode)
    {
        RefreshScreenModeDropdown();
    }

    // --- ІНІЦІАЛІЗАЦІЯ ---

    private void InitializeToggles()
    {
        if (publicRoomToggle != null) publicRoomToggle.isOn = false;
        if (passwordRoomToggle != null) passwordRoomToggle.isOn = false;
        
        SetActive(createPasswordInput, false);
    }

    // --- НАВІГАЦІЯ ТА ВІДОБРАЖЕННЯ ---

    private void ChangeWindow(MenuWindow window)
    {
        _currentWindow = window;

        SetActive(mainMenuScreen, window == MenuWindow.Main);
        SetActive(settingsScreen, window == MenuWindow.Settings);
        SetActive(connectionTypeScreen, window == MenuWindow.ConnectionType);
        SetActive(createRoomScreen, window == MenuWindow.Create);
        SetActive(joinRoomScreen, window == MenuWindow.Join);
        SetActive(waitingRoomScreen, window == MenuWindow.WaitingRoom);

        bool showOverlay = window != MenuWindow.InGame && window != MenuWindow.Main;
        SetActive(darkBackgroundOverlay, showOverlay); 

        if (window == MenuWindow.Create || window == MenuWindow.Join)
        {
            RefreshDynamicFields();
        }

        if (window == MenuWindow.Settings)
        {
            RefreshSettingsControls();
        }
    }

    private void RefreshSettingsControls()
    {
        RefreshAudioSliders();
        RefreshScreenModeDropdown();
        RefreshPingToggle();
        RefreshMouseSensitivitySlider();
    }

    private void RefreshAudioSliders()
    {
        if (bgSoundsSlider != null)
        {
            bgSoundsSlider.SetValueWithoutNotify(CoopAudioSettings.MusicVolume);
        }

        if (interactionSoundsSlider != null)
        {
            interactionSoundsSlider.SetValueWithoutNotify(CoopAudioSettings.InteractionVolume);
        }

        if (volumeSlider != null)
        {
            volumeSlider.SetValueWithoutNotify(CoopAudioSettings.MasterVolume);
        }
    }

    private void RefreshScreenModeDropdown()
    {
        if (screenModeDropdown == null)
        {
            return;
        }

        bool needsOptions =
            screenModeDropdown.options.Count != 2 ||
            screenModeDropdown.options[0].text != "Windowed" ||
            screenModeDropdown.options[1].text != "Fullscreen";

        if (needsOptions)
        {
            screenModeDropdown.ClearOptions();
            screenModeDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "Windowed",
                "Fullscreen"
            });
        }

        screenModeDropdown.SetValueWithoutNotify((int)CoopUserSettings.ScreenMode);
        screenModeDropdown.RefreshShownValue();
    }

    private void RefreshPingToggle()
    {
        if (showPingToggle != null)
        {
            showPingToggle.SetIsOnWithoutNotify(CoopUserSettings.ShowPing);
        }
    }

    private void RefreshMouseSensitivitySlider()
    {
        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.minValue = SettingsManager.MinMouseSensitivity;
            mouseSensitivitySlider.maxValue = SettingsManager.MaxMouseSensitivity;
            mouseSensitivitySlider.SetValueWithoutNotify(SettingsManager.MouseSensitivity);
        }

        RefreshMouseSensitivityValueText(SettingsManager.MouseSensitivity);
    }

    private void RefreshMouseSensitivityValueText(float value)
    {
        if (mouseSensitivityValueText != null)
        {
            mouseSensitivityValueText.text = value.ToString("0.00");
        }
    }

    private void RefreshDynamicFields()
    {
        if (_controller == null) return;

        bool isLocal = _controller.IsLocalScenario;

        if (createModeLabelText) 
            createModeLabelText.text = isLocal ? "Mode: Local (Create Room)" : "Mode: Online (Create Room)";
            
        if (joinModeLabelText) 
            joinModeLabelText.text = isLocal ? "Mode: Local (Connect to Room)" : "Mode: Online (Connect to Room)";

        SetActive(createRoomNameInput, !isLocal);  
        SetActive(createLocalHostAddressInput, isLocal);
        SetActive(createPortInput, isLocal);
        SetActive(createRoomCodeInput, isLocal);   
        SetActive(publicRoomToggle, !isLocal);     
        SetActive(passwordRoomToggle, !isLocal);

        SetActive(joinLocalHostAddressInput, isLocal);
        SetActive(joinPortInput, isLocal);
        SetActive(joinPasswordInput, !isLocal);

        RefreshConnectionFields();
        UpdatePasswordVisibility();
    }

    private void RefreshConnectionFields()
    {
        if (_controller == null)
        {
            return;
        }

        SetInputTextWithoutNotify(createLocalHostAddressInput, _controller.LocalHostAddress);
        SetInputTextWithoutNotify(joinLocalHostAddressInput, _controller.LocalHostAddress);
        SetInputTextWithoutNotify(createPortInput, _controller.PortText);
        SetInputTextWithoutNotify(joinPortInput, _controller.PortText);
    }

    private void UpdatePasswordVisibility()
    {
        if (_controller == null) return;

        bool isLocal = _controller.IsLocalScenario;
        
        if (isLocal)
        {
            SetActive(createPasswordInput, false);
        }
        else
        {
            bool showPassword = passwordRoomToggle != null && passwordRoomToggle.isOn;
            SetActive(createPasswordInput, showPassword);
        }
    }

    private void UpdateStatusText()
    {
        string statusMessage = "Status: " + _controller.StatusText;
        
        if (createStatusText != null) createStatusText.text = statusMessage;
        if (joinStatusText != null) joinStatusText.text = statusMessage;
    }

    // --- ОНОВЛЕНИЙ МЕТОД ---
    private void UpdateWaitingRoomUI()
    {
        if (_controller == null) return;

        bool isNetwork = _controller.IsNetworkScenario;
        bool isHost = _controller.IsHost;
        bool canStart = _controller.CanStartGame;

        // ДОДАНО: Зміна заголовку на "Network mode waiting room" або "Local mode waiting room"
        if (waitingRoomModeTitleText != null)
        {
            waitingRoomModeTitleText.text = isNetwork ? "Network mode waiting room" : "Local mode waiting room";
        }

        // 1. Room name (Показується тільки в Мережевій грі)
        if (waitingRoomNameContainer != null) 
            SetActive(waitingRoomNameContainer, isNetwork);
        else if (waitingRoomNameText != null) 
            SetActive(waitingRoomNameText, isNetwork);

        if (isNetwork && waitingRoomNameText != null) 
            waitingRoomNameText.text = _controller.ActiveRoomDisplayName;

        // 2. Room code
        if (waitingRoomCodeText != null) 
            waitingRoomCodeText.text =_controller.RoomCode;

        // 3. Access (Public/Private)
        if (waitingRoomAccessText != null) 
            waitingRoomAccessText.text = (_controller.IsPrivateRoom ? "Private" : "Public");

        // 4. Кнопка керування грою (Тільки для Хоста)
        SetActive(waitingRoomStartButton, isHost);

        if (isHost && waitingRoomStartButton != null)
        {
            waitingRoomStartButton.interactable = canStart; // Disabled якщо не можна стартувати

            if (waitingRoomStartButtonText != null)
            {
                waitingRoomStartButtonText.text = canStart ? "Start Game" : "Waiting for another player";
            }
        }
    }

    // --- ПРИВ'ЯЗКА ПОДІЙ ---

    private void BindListeners()
    {
        ButtonAnimator localAnim = localConnButton != null ? localConnButton.GetComponent<ButtonAnimator>() : null;
        ButtonAnimator onlineAnim = onlineConnButton != null ? onlineConnButton.GetComponent<ButtonAnimator>() : null;

        BindButton(localConnButton, () => {
            AudioController.Play(AudioEvent.UiClick);
            _controller?.SelectLocalScenario();
            if (localAnim) localAnim.SetSelected(true);
            if (onlineAnim) onlineAnim.SetSelected(false);
        });

        BindButton(onlineConnButton, () => {
            AudioController.Play(AudioEvent.UiClick);
            _controller?.SelectNetworkScenario();
            if (onlineAnim) onlineAnim.SetSelected(true);
            if (localAnim) localAnim.SetSelected(false);
        });

        BindButton(playButton, () => {
            AudioController.Play(AudioEvent.UiConfirm);
            ChangeWindow(MenuWindow.ConnectionType);
        });
        BindButton(settingsMenuButton, () => {
            AudioController.Play(AudioEvent.UiConfirm);
            ChangeWindow(MenuWindow.Settings);
        });
        BindButton(quitButton, Application.Quit);

        BindButton(settingsBackButton, () => {
            AudioController.Play(AudioEvent.UiBack);
            ChangeWindow(MenuWindow.Main);
        });
        BindButton(createRoomModeButton, () => {
            AudioController.Play(AudioEvent.UiConfirm);
            ChangeWindow(MenuWindow.Create);
        });
        BindButton(connectRoomModeButton, () => {
            AudioController.Play(AudioEvent.UiConfirm);
            ChangeWindow(MenuWindow.Join);
        });
        BindButton(connTypeBackButton, () => {
            AudioController.Play(AudioEvent.UiBack);
            ChangeWindow(MenuWindow.Main);
        });

        BindButton(startRoomButton, OnStartRoomClicked);
        BindButton(createBackButton, () => {
            AudioController.Play(AudioEvent.UiBack);
            ChangeWindow(MenuWindow.ConnectionType);
        });

        BindButton(connectButton, OnJoinRoomClicked);
        BindButton(joinBackButton, () => {
            AudioController.Play(AudioEvent.UiBack);
            ChangeWindow(MenuWindow.ConnectionType);
        });

        // Логіка кнопок вікна очікування
        BindButton(waitingRoomStartButton, () => {
            AudioController.Play(AudioEvent.GameStarted);
            _controller?.TryStartGameFromUi();
        }); 
        BindButton(waitingRoomExitButton, () => {
            AudioController.Play(AudioEvent.UiCancel);
            _controller?.DisconnectAndReturnToMenu();
        });

        // Збереження оригінального тексту кнопки копіювання
        if (waitingRoomCopyButtonText != null)
        {
            _originalCopyButtonText = waitingRoomCopyButtonText.text;
        }

        BindButton(waitingRoomCopyCodeButton, () => {
            if (_controller != null && !string.IsNullOrEmpty(_controller.RoomCode))
            {
                GUIUtility.systemCopyBuffer = _controller.RoomCode;
                AudioController.Play(AudioEvent.UiCopy);
                StartCoroutine(CopyButtonConfirmationRoutine()); 
            }
        });

        if (bgSoundsSlider) bgSoundsSlider.onValueChanged.AddListener(v => {
            CoopAudioSettings.SetMusicVolume(v);
            AudioController.Play(AudioEvent.UiSlider);
        });
        if (interactionSoundsSlider) interactionSoundsSlider.onValueChanged.AddListener(v => {
            CoopAudioSettings.SetInteractionVolume(v);
            AudioController.Play(AudioEvent.UiSlider);
        });
        if (volumeSlider) volumeSlider.onValueChanged.AddListener(v => {
            CoopAudioSettings.SetMasterVolume(v);
            AudioController.Play(AudioEvent.UiSlider);
        });

        if (screenModeDropdown != null)
        {
            screenModeDropdown.onValueChanged.AddListener(v => {
                CoopUserSettings.SetScreenMode((CoopScreenMode)v);
                AudioController.Play(AudioEvent.UiToggle);
            });
        }

        if (showPingToggle != null)
        {
            showPingToggle.onValueChanged.AddListener(value => {
                if (_controller != null)
                {
                    _controller.SetShowPing(value);
                }
                else
                {
                    CoopUserSettings.SetShowPing(value);
                }

                AudioController.Play(AudioEvent.UiToggle);
            });
        }

        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.onValueChanged.AddListener(value => {
                SettingsManager.SetMouseSensitivity(value);
                RefreshMouseSensitivityValueText(SettingsManager.MouseSensitivity);
            });
        }

        if (publicRoomToggle != null)
        {
            publicRoomToggle.onValueChanged.AddListener((isOn) => {
                AudioController.Play(AudioEvent.UiToggle);
                if (isOn && passwordRoomToggle != null) passwordRoomToggle.isOn = false;
            });
        }

        if (passwordRoomToggle != null)
        {
            passwordRoomToggle.onValueChanged.AddListener((isOn) => {
                AudioController.Play(AudioEvent.UiToggle);
                if (isOn && publicRoomToggle != null) publicRoomToggle.isOn = false;
                UpdatePasswordVisibility();
            });
        }
    }

    // --- КОРУТИНА: Ефект кнопки копіювання ---
    private IEnumerator CopyButtonConfirmationRoutine()
    {
        if (waitingRoomCopyButtonText == null) yield break;

        // ВИПРАВЛЕНО: Спочатку міняємо текст
        waitingRoomCopyButtonText.text = "Copied!";

        // Чекаємо 1.5 секунди
        yield return new WaitForSeconds(1.5f);

        // Повертаємо початковий текст (наприклад "Copy")
        if (waitingRoomCopyButtonText != null)
        {
            waitingRoomCopyButtonText.text = _originalCopyButtonText;
        }
    }

    // --- ЛОГІКА МЕРЕЖІ ---

    private void EnsureRuntimeSettingsControls()
    {
        if (settingsScreen == null || (showPingToggle != null && mouseSensitivitySlider != null && mouseSensitivityValueText != null))
        {
            return;
        }

        Transform existing = settingsScreen.transform.Find("RuntimeSettingsControls");
        Transform root = existing != null ? existing : CreateSettingsExtensionRoot(settingsScreen.transform);

        if (showPingToggle == null)
        {
            showPingToggle = CreatePingToggle(root);
        }

        if (mouseSensitivitySlider == null || mouseSensitivityValueText == null)
        {
            CreateMouseSensitivityControl(root);
        }
    }

    private Transform CreateSettingsExtensionRoot(Transform parent)
    {
        GameObject rootObject = new GameObject("RuntimeSettingsControls");
        rootObject.transform.SetParent(parent, false);

        RectTransform rectTransform = rootObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0f);
        rectTransform.anchorMax = new Vector2(0.5f, 0f);
        rectTransform.pivot = new Vector2(0.5f, 0f);
        rectTransform.anchoredPosition = new Vector2(0f, 92f);
        rectTransform.sizeDelta = new Vector2(520f, 118f);

        VerticalLayoutGroup layout = rootObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = rootObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rootObject.transform;
    }

    private Toggle CreatePingToggle(Transform parent)
    {
        GameObject row = CreateSettingsRow(parent, "ShowPingRow");
        TMP_Text label = CreateSettingsLabel(row.transform, "Show ping");
        label.rectTransform.sizeDelta = new Vector2(390f, 34f);

        GameObject toggleObject = new GameObject("ShowPingToggle");
        toggleObject.transform.SetParent(row.transform, false);
        RectTransform toggleRect = toggleObject.AddComponent<RectTransform>();
        toggleRect.sizeDelta = new Vector2(34f, 34f);

        Image background = toggleObject.AddComponent<Image>();
        background.color = new Color(0.12f, 0.13f, 0.15f, 0.95f);

        GameObject checkObject = new GameObject("Checkmark");
        checkObject.transform.SetParent(toggleObject.transform, false);
        RectTransform checkRect = checkObject.AddComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0.18f, 0.18f);
        checkRect.anchorMax = new Vector2(0.82f, 0.82f);
        checkRect.offsetMin = Vector2.zero;
        checkRect.offsetMax = Vector2.zero;

        Image checkmark = checkObject.AddComponent<Image>();
        checkmark.color = new Color(0.25f, 0.8f, 0.45f, 1f);

        Toggle toggle = toggleObject.AddComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = checkmark;
        return toggle;
    }

    private void CreateMouseSensitivityControl(Transform parent)
    {
        GameObject row = CreateSettingsRow(parent, "MouseSensitivityRow");
        TMP_Text label = CreateSettingsLabel(row.transform, "Mouse sensitivity");
        label.rectTransform.sizeDelta = new Vector2(210f, 34f);

        mouseSensitivitySlider = CreateRuntimeSlider(row.transform);
        mouseSensitivityValueText = CreateSettingsLabel(row.transform, "1.00");
        mouseSensitivityValueText.alignment = TextAlignmentOptions.Right;
        mouseSensitivityValueText.rectTransform.sizeDelta = new Vector2(70f, 34f);
    }

    private GameObject CreateSettingsRow(Transform parent, string name)
    {
        GameObject rowObject = new GameObject(name);
        rowObject.transform.SetParent(parent, false);
        RectTransform rectTransform = rowObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(520f, 38f);

        HorizontalLayoutGroup layout = rowObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return rowObject;
    }

    private TMP_Text CreateSettingsLabel(Transform parent, string text)
    {
        GameObject textObject = new GameObject("Label");
        textObject.transform.SetParent(parent, false);
        RectTransform rectTransform = textObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(200f, 34f);

        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 22f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Left;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    private Slider CreateRuntimeSlider(Transform parent)
    {
        GameObject sliderObject = new GameObject("MouseSensitivitySlider");
        sliderObject.transform.SetParent(parent, false);
        RectTransform sliderRect = sliderObject.AddComponent<RectTransform>();
        sliderRect.sizeDelta = new Vector2(210f, 34f);

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = SettingsManager.MinMouseSensitivity;
        slider.maxValue = SettingsManager.MaxMouseSensitivity;

        GameObject backgroundObject = new GameObject("Background");
        backgroundObject.transform.SetParent(sliderObject.transform, false);
        RectTransform backgroundRect = backgroundObject.AddComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(1f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(0f, 8f);
        Image background = backgroundObject.AddComponent<Image>();
        background.color = new Color(0.12f, 0.13f, 0.15f, 0.95f);

        GameObject fillAreaObject = new GameObject("Fill Area");
        fillAreaObject.transform.SetParent(sliderObject.transform, false);
        RectTransform fillAreaRect = fillAreaObject.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
        fillAreaRect.offsetMin = new Vector2(8f, -4f);
        fillAreaRect.offsetMax = new Vector2(-8f, 4f);

        GameObject fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(fillAreaObject.transform, false);
        RectTransform fillRect = fillObject.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fill = fillObject.AddComponent<Image>();
        fill.color = new Color(0.25f, 0.55f, 0.95f, 1f);

        GameObject handleAreaObject = new GameObject("Handle Slide Area");
        handleAreaObject.transform.SetParent(sliderObject.transform, false);
        RectTransform handleAreaRect = handleAreaObject.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(8f, 0f);
        handleAreaRect.offsetMax = new Vector2(-8f, 0f);

        GameObject handleObject = new GameObject("Handle");
        handleObject.transform.SetParent(handleAreaObject.transform, false);
        RectTransform handleRect = handleObject.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(22f, 22f);
        Image handle = handleObject.AddComponent<Image>();
        handle.color = Color.white;

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private void OnStartRoomClicked()
    {
        if (_controller == null) return;
        AudioController.Play(AudioEvent.UiConfirm);

        _controller.SetPlayerName(createPlayerNameInput != null ? createPlayerNameInput.text : "Player");
        _controller.SetRoomPassword(createPasswordInput != null ? createPasswordInput.text : "");
        
        if (_controller.IsLocalScenario)
        {
            _controller.SetLocalHostAddress(createLocalHostAddressInput != null ? createLocalHostAddressInput.text : _controller.LocalHostAddress);
            _controller.SetPortText(createPortInput != null ? createPortInput.text : _controller.PortText);
            _controller.SetRoomCode(createRoomCodeInput != null ? createRoomCodeInput.text : "");
            _controller.SetPrivateRoom(false); 
        }
        else
        {
            _controller.SetRoomName(createRoomNameInput != null ? createRoomNameInput.text : "My Room");
            _controller.SetPrivateRoom(passwordRoomToggle != null && passwordRoomToggle.isOn);
        }

        _controller.TryCreateRoomFromUi();
    }

    private void OnJoinRoomClicked()
    {
        if (_controller == null) return;
        AudioController.Play(AudioEvent.UiConfirm);

        _controller.SetPlayerName(joinPlayerNameInput != null ? joinPlayerNameInput.text : "Player");
        if (_controller.IsLocalScenario)
        {
            _controller.SetLocalHostAddress(joinLocalHostAddressInput != null ? joinLocalHostAddressInput.text : _controller.LocalHostAddress);
            _controller.SetPortText(joinPortInput != null ? joinPortInput.text : _controller.PortText);
        }

        _controller.SetRoomCode(joinRoomCodeInput != null ? joinRoomCodeInput.text : "");
        _controller.SetRoomPassword(joinPasswordInput != null ? joinPasswordInput.text : "");
        
        _controller.TryJoinRoomFromUi();
    }

    // --- БЕЗПЕЧНІ ДОПОМІЖНІ МЕТОДИ ---

    private static void BindButton(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button != null && callback != null)
        {
            button.onClick.AddListener(callback);
        }
    }

    private static void SetInputTextWithoutNotify(TMP_InputField input, string value)
    {
        if (input == null)
        {
            return;
        }

        string safeValue = value ?? string.Empty;
        if (input.text != safeValue)
        {
            input.SetTextWithoutNotify(safeValue);
        }
    }

    private static void SetActive(GameObject target, bool value)
    {
        if (target != null && target.activeSelf != value)
        {
            target.SetActive(value);
        }
    }

    private static void SetActive(Component target, bool value)
    {
        if (target != null && target.gameObject != null && target.gameObject.activeSelf != value)
        {
            target.gameObject.SetActive(value);
        }
    }
}
