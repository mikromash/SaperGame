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
        if (_controller != null) _controller.DetachCanvasUi();
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
