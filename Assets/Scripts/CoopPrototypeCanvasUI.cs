using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CoopPrototypeCanvasUI : MonoBehaviour
{
    [Header("Root Panels")]
    [SerializeField] private GameObject mainMenuRoot;
    [SerializeField] private GameObject createRoomRoot;
    [SerializeField] private GameObject joinRoomRoot;
    [SerializeField] private GameObject inGameRoot;
    [SerializeField] private GameObject pauseMenuRoot;
    [SerializeField] private GameObject pauseActionsRoot;
    [SerializeField] private GameObject pauseSettingsRoot;

    [Header("Shared Text")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text mainMenuModeText;
    [SerializeField] private TMP_Text createModeText;
    [SerializeField] private TMP_Text createRelayText;
    [SerializeField] private TMP_Text joinModeText;
    [SerializeField] private TMP_Text joinRelayText;

    [Header("Main Menu Buttons")]
    [SerializeField] private Button localScenarioButton;
    [SerializeField] private Button networkScenarioButton;
    [SerializeField] private Button openCreateRoomButton;
    [SerializeField] private Button openJoinRoomButton;

    [Header("Create Room Inputs")]
    [SerializeField] private TMP_InputField createPlayerNameInput;
    [SerializeField] private TMP_InputField createHostInput;
    [SerializeField] private TMP_InputField createPortInput;
    [SerializeField] private TMP_InputField createRoomNameInput;
    [SerializeField] private Toggle createPrivateRoomToggle;
    [SerializeField] private TMP_InputField createPasswordInput;
    [SerializeField] private GameObject createLocalFieldsRoot;
    [SerializeField] private GameObject createNetworkFieldsRoot;
    [SerializeField] private GameObject createPasswordRoot;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button createBackButton;

    [Header("Join Room Inputs")]
    [SerializeField] private TMP_InputField joinPlayerNameInput;
    [SerializeField] private TMP_InputField joinHostInput;
    [SerializeField] private TMP_InputField joinPortInput;
    [SerializeField] private TMP_InputField joinRoomCodeInput;
    [SerializeField] private TMP_InputField joinPasswordInput;
    [SerializeField] private GameObject joinLocalFieldsRoot;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private Button joinBackButton;

    [Header("In-Game Overlay")]
    [SerializeField] private TMP_Text inGameModeText;
    [SerializeField] private TMP_Text inGameRelayText;
    [SerializeField] private TMP_Text inGameRoomNameText;
    [SerializeField] private TMP_Text inGameRoomCodeText;
    [SerializeField] private TMP_Text inGameAccessText;
    [SerializeField] private TMP_Text inGamePlayerIdText;
    [SerializeField] private TMP_Text inGamePingText;
    [SerializeField] private TMP_Text inGameStatusText;
    [SerializeField] private TMP_Text inGameLobbyMessageText;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button disconnectButton;

    [Header("Pause Menu")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button windowSettingsButton;
    [SerializeField] private Button leaveRoomButton;
    [SerializeField] private Button exitGameButton;
    [SerializeField] private Button fullscreenButton;
    [SerializeField] private Button windowedButton;
    [SerializeField] private Button pauseSettingsBackButton;

    private CoopPrototypeController _controller;
    private bool _listenersBound;

    private void Awake()
    {
        TryBindController();
        BindListeners();
        RefreshAll();
    }

    private void Start()
    {
        TryBindController();
        RefreshAll();
    }

    private void OnEnable()
    {
        TryBindController();
        RefreshAll();
    }

    private void OnDestroy()
    {
        if (_controller != null)
        {
            _controller.DetachCanvasUi();
        }
    }

    private void Update()
    {
        TryBindController();
        RefreshAll();
    }

    private void TryBindController()
    {
        if (_controller != null)
        {
            return;
        }

        _controller = CoopPrototypeController.Instance;
        if (_controller == null)
        {
            _controller = FindAnyObjectByType<CoopPrototypeController>();
        }

        if (_controller != null)
        {
            _controller.AttachCanvasUi();
            PushInputsToController();
        }
    }

    private void BindListeners()
    {
        if (_listenersBound)
        {
            return;
        }

        BindButton(localScenarioButton, OnLocalScenarioClicked);
        BindButton(networkScenarioButton, OnNetworkScenarioClicked);
        BindButton(openCreateRoomButton, OnOpenCreateRoomClicked);
        BindButton(openJoinRoomButton, OnOpenJoinRoomClicked);
        BindButton(createRoomButton, OnCreateRoomClicked);
        BindButton(createBackButton, OnBackToMainMenuClicked);
        BindButton(joinRoomButton, OnJoinRoomClicked);
        BindButton(joinBackButton, OnBackToMainMenuClicked);
        BindButton(startGameButton, OnStartGameClicked);
        BindButton(disconnectButton, OnDisconnectClicked);
        BindButton(resumeButton, OnResumeClicked);
        BindButton(windowSettingsButton, OnWindowSettingsClicked);
        BindButton(leaveRoomButton, OnLeaveRoomClicked);
        BindButton(exitGameButton, OnExitGameClicked);
        BindButton(fullscreenButton, OnFullscreenClicked);
        BindButton(windowedButton, OnWindowedClicked);
        BindButton(pauseSettingsBackButton, OnPauseSettingsBackClicked);

        BindInput(createPlayerNameInput, OnCreatePlayerNameChanged);
        BindInput(createHostInput, OnCreateHostChanged);
        BindInput(createPortInput, OnCreatePortChanged);
        BindInput(createRoomNameInput, OnCreateRoomNameChanged);
        BindInput(createPasswordInput, OnCreatePasswordChanged);
        BindInput(joinPlayerNameInput, OnJoinPlayerNameChanged);
        BindInput(joinHostInput, OnJoinHostChanged);
        BindInput(joinPortInput, OnJoinPortChanged);
        BindInput(joinRoomCodeInput, OnJoinRoomCodeChanged);
        BindInput(joinPasswordInput, OnJoinPasswordChanged);

        if (createPrivateRoomToggle != null)
        {
            createPrivateRoomToggle.onValueChanged.AddListener(OnCreatePrivateRoomChanged);
        }

        _listenersBound = true;
    }

    private void RefreshAll()
    {
        if (_controller == null)
        {
            SetActive(mainMenuRoot, true);
            SetActive(createRoomRoot, false);
            SetActive(joinRoomRoot, false);
            SetActive(inGameRoot, false);
            SetActive(pauseMenuRoot, false);
            return;
        }

        SyncInputsFromController();
        RefreshPanels();
        RefreshTexts();
        RefreshConditionalBlocks();
    }

    private void RefreshPanels()
    {
        SetActive(mainMenuRoot, _controller.IsMainMenu);
        SetActive(createRoomRoot, _controller.IsCreateRoomScreen);
        SetActive(joinRoomRoot, _controller.IsJoinRoomScreen);
        SetActive(inGameRoot, _controller.IsInGame || _controller.IsWaitingRoom);
        SetActive(pauseMenuRoot, _controller.IsPauseMenuOpen);
        SetActive(pauseActionsRoot, _controller.IsPauseMenuOpen && !_controller.IsSettingsMenuOpen);
        SetActive(pauseSettingsRoot, _controller.IsPauseMenuOpen && _controller.IsSettingsMenuOpen);
    }

    private void RefreshTexts()
    {
        SetText(statusText, _controller.StatusText);
        SetText(mainMenuModeText, _controller.ScenarioLabel);
        SetText(createModeText, _controller.ScenarioLabel);
        SetText(createRelayText, _controller.SelectedRelayHost + ":" + _controller.PortText);
        SetText(joinModeText, _controller.ScenarioLabel);
        SetText(joinRelayText, _controller.SelectedRelayHost + ":" + _controller.PortText);

        SetText(inGameModeText, _controller.ScenarioLabel);
        SetText(inGameRelayText, _controller.SelectedRelayHost + ":" + _controller.PortText);
        SetText(inGameRoomNameText, _controller.ActiveRoomDisplayName);
        SetText(inGameRoomCodeText, _controller.RoomCode);
        SetText(inGameAccessText, _controller.AccessLabel);
        SetText(inGamePlayerIdText, _controller.LocalPlayerId.ToString());
        SetText(inGamePingText, "Ping: " + _controller.PingDisplay);
        SetText(inGameStatusText, _controller.StatusText);
        SetText(inGameLobbyMessageText, BuildLobbyMessage());
    }

    private void RefreshConditionalBlocks()
    {
        bool isLocal = _controller.IsLocalScenario;
        bool showCreatePassword = isLocal || _controller.IsPrivateRoom;

        SetActive(createLocalFieldsRoot, isLocal);
        SetActive(createNetworkFieldsRoot, !isLocal);
        SetActive(createPasswordRoot, showCreatePassword);
        SetActive(joinLocalFieldsRoot, isLocal);

        if (startGameButton != null)
        {
            bool showStartButton = _controller.IsWaitingRoom && _controller.IsHost;
            SetActive(startGameButton.gameObject, showStartButton);
            startGameButton.interactable = _controller.CanStartGame;
        }
    }

    private void SyncInputsFromController()
    {
        SetInputValue(createPlayerNameInput, _controller.PlayerName);
        SetInputValue(createHostInput, _controller.LocalHostAddress);
        SetInputValue(createPortInput, _controller.PortText);
        SetInputValue(createRoomNameInput, _controller.RoomName);
        SetToggleValue(createPrivateRoomToggle, _controller.IsPrivateRoom);
        SetInputValue(createPasswordInput, _controller.RoomPassword);

        SetInputValue(joinPlayerNameInput, _controller.PlayerName);
        SetInputValue(joinHostInput, _controller.LocalHostAddress);
        SetInputValue(joinPortInput, _controller.PortText);
        SetInputValue(joinRoomCodeInput, _controller.RoomCode);
        SetInputValue(joinPasswordInput, _controller.RoomPassword);
    }

    private void PushInputsToController()
    {
        if (_controller == null)
        {
            return;
        }

        _controller.SetPlayerName(ReadInput(createPlayerNameInput, joinPlayerNameInput));
        _controller.SetLocalHostAddress(ReadInput(createHostInput, joinHostInput));
        _controller.SetPortText(ReadInput(createPortInput, joinPortInput));
        _controller.SetRoomName(ReadInput(createRoomNameInput));
        _controller.SetRoomCode(ReadInput(joinRoomCodeInput));
        _controller.SetRoomPassword(ReadInput(createPasswordInput, joinPasswordInput));
        _controller.SetPrivateRoom(createPrivateRoomToggle != null && createPrivateRoomToggle.isOn);
    }

    private void OnLocalScenarioClicked()
    {
        if (_controller == null)
        {
            return;
        }

        _controller.SelectLocalScenario();
        RefreshAll();
    }

    private void OnNetworkScenarioClicked()
    {
        if (_controller == null)
        {
            return;
        }

        _controller.SelectNetworkScenario();
        RefreshAll();
    }

    private void OnOpenCreateRoomClicked()
    {
        if (_controller == null)
        {
            return;
        }

        PushInputsToController();
        _controller.OpenCreateRoomScreen();
        RefreshAll();
    }

    private void OnOpenJoinRoomClicked()
    {
        if (_controller == null)
        {
            return;
        }

        PushInputsToController();
        _controller.OpenJoinRoomScreen();
        RefreshAll();
    }

    private void OnCreateRoomClicked()
    {
        if (_controller == null)
        {
            return;
        }

        _controller.SetPlayerName(ReadInput(createPlayerNameInput));
        _controller.SetLocalHostAddress(ReadInput(createHostInput));
        _controller.SetPortText(ReadInput(createPortInput));
        _controller.SetRoomName(ReadInput(createRoomNameInput));
        _controller.SetRoomPassword(ReadInput(createPasswordInput));
        _controller.SetPrivateRoom(createPrivateRoomToggle != null && createPrivateRoomToggle.isOn);
        _controller.TryCreateRoomFromUi();
        RefreshAll();
    }

    private void OnJoinRoomClicked()
    {
        if (_controller == null)
        {
            return;
        }

        _controller.SetPlayerName(ReadInput(joinPlayerNameInput));
        _controller.SetLocalHostAddress(ReadInput(joinHostInput));
        _controller.SetPortText(ReadInput(joinPortInput));
        _controller.SetRoomCode(ReadInput(joinRoomCodeInput));
        _controller.SetRoomPassword(ReadInput(joinPasswordInput));
        _controller.TryJoinRoomFromUi();
        RefreshAll();
    }

    private void OnBackToMainMenuClicked()
    {
        if (_controller == null)
        {
            return;
        }

        _controller.BackToMainMenu();
        RefreshAll();
    }

    private void OnDisconnectClicked()
    {
        if (_controller == null)
        {
            return;
        }

        _controller.DisconnectAndReturnToMenu();
        RefreshAll();
    }

    private void OnStartGameClicked()
    {
        if (_controller == null)
        {
            return;
        }

        _controller.TryStartGameFromUi();
        RefreshAll();
    }

    private void OnResumeClicked()
    {
        if (_controller == null)
        {
            return;
        }

        _controller.ResumeGameplayFromUi();
        RefreshAll();
    }

    private void OnWindowSettingsClicked()
    {
        if (_controller == null)
        {
            return;
        }

        _controller.OpenPauseSettings();
        RefreshAll();
    }

    private void OnLeaveRoomClicked()
    {
        if (_controller == null)
        {
            return;
        }

        _controller.LeaveRoom();
        RefreshAll();
    }

    private void OnExitGameClicked()
    {
        if (_controller == null)
        {
            return;
        }

        _controller.ExitGameFromUi();
    }

    private void OnFullscreenClicked()
    {
        if (_controller == null)
        {
            return;
        }

        _controller.SetFullscreenMode();
        RefreshAll();
    }

    private void OnWindowedClicked()
    {
        if (_controller == null)
        {
            return;
        }

        _controller.SetWindowedMode();
        RefreshAll();
    }

    private void OnPauseSettingsBackClicked()
    {
        if (_controller == null)
        {
            return;
        }

        _controller.ClosePauseSettings();
        RefreshAll();
    }

    private void OnCreatePlayerNameChanged(string value) => SafeControllerCall(() => _controller.SetPlayerName(value));
    private void OnCreateHostChanged(string value) => SafeControllerCall(() => _controller.SetLocalHostAddress(value));
    private void OnCreatePortChanged(string value) => SafeControllerCall(() => _controller.SetPortText(value));
    private void OnCreateRoomNameChanged(string value) => SafeControllerCall(() => _controller.SetRoomName(value));
    private void OnCreatePasswordChanged(string value) => SafeControllerCall(() => _controller.SetRoomPassword(value));
    private void OnJoinPlayerNameChanged(string value) => SafeControllerCall(() => _controller.SetPlayerName(value));
    private void OnJoinHostChanged(string value) => SafeControllerCall(() => _controller.SetLocalHostAddress(value));
    private void OnJoinPortChanged(string value) => SafeControllerCall(() => _controller.SetPortText(value));
    private void OnJoinRoomCodeChanged(string value) => SafeControllerCall(() => _controller.SetRoomCode(value));
    private void OnJoinPasswordChanged(string value) => SafeControllerCall(() => _controller.SetRoomPassword(value));
    private void OnCreatePrivateRoomChanged(bool value) => SafeControllerCall(() => _controller.SetPrivateRoom(value));

    private void SafeControllerCall(System.Action action)
    {
        if (_controller == null || action == null)
        {
            return;
        }

        action.Invoke();
        RefreshConditionalBlocks();
        RefreshTexts();
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button == null || callback == null)
        {
            return;
        }

        button.onClick.AddListener(callback);
    }

    private static void BindInput(TMP_InputField input, UnityEngine.Events.UnityAction<string> callback)
    {
        if (input == null || callback == null)
        {
            return;
        }

        input.onValueChanged.AddListener(callback);
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value ?? string.Empty;
        }
    }

    private static void SetActive(GameObject target, bool value)
    {
        if (target != null && target.activeSelf != value)
        {
            target.SetActive(value);
        }
    }

    private static void SetInputValue(TMP_InputField input, string value)
    {
        if (input != null && input.text != value)
        {
            input.SetTextWithoutNotify(value ?? string.Empty);
        }
    }

    private static void SetToggleValue(Toggle toggle, bool value)
    {
        if (toggle != null && toggle.isOn != value)
        {
            toggle.SetIsOnWithoutNotify(value);
        }
    }

    private static string ReadInput(params TMP_InputField[] inputs)
    {
        foreach (TMP_InputField input in inputs)
        {
            if (input != null)
            {
                return input.text ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private string BuildLobbyMessage()
    {
        if (_controller == null || !_controller.IsWaitingRoom)
        {
            return string.Empty;
        }

        if (_controller.IsHost)
        {
            if (_controller.RoomState == "player_joined")
            {
                return "2-й игрок подключён";
            }

            return "Ожидание подключения 2-го игрока\nКод комнаты: " + _controller.RoomCode;
        }

        return "Ожидание запуска игры от хоста";
    }
}
