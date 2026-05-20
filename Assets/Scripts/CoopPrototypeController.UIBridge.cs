using UnityEngine;

public sealed partial class CoopPrototypeController
{
    // Публичный мост между внутренним состоянием контроллера и Canvas UI.
    private bool _useCanvasUi;

    // Canvas UI читает только готовые свойства, не работая напрямую с приватными полями.
    public static CoopPrototypeController Instance => _instance;

    public bool UseCanvasUi => _useCanvasUi;
    public bool IsWaitingRoom => _screen == MenuScreen.WaitingRoom;
    public bool IsInGame => _screen == MenuScreen.InGame;
    public bool IsMainMenu => _screen == MenuScreen.MainMenu;
    public bool IsCreateRoomScreen => _screen == MenuScreen.CreateRoom;
    public bool IsJoinRoomScreen => _screen == MenuScreen.JoinRoom;
    public bool IsPauseMenuOpen => _isPauseMenuOpen;
    public bool IsSettingsMenuOpen => _isSettingsMenuOpen;
    public bool IsLocalScenario => _scenario == ConnectionScenario.Local;
    public bool IsNetworkScenario => _scenario == ConnectionScenario.Network;
    public bool IsPrivateRoom => _isPrivateRoom;
    public string StatusText => _status;
    public string ScenarioLabel => GetScenarioLabel();
    public string SelectedRelayHost => GetSelectedRelayHost();
    public string PortText => _portText;
    public string LocalHostAddress => _localHostAddress;
    public string RelayHostAddress => _relayHost;
    public string RoomCode => _roomCode;
    public string RoomName => _roomName;
    public string ConnectedRoomName => _connectedRoomName;
    public string RoomPassword => _roomPassword;
    public string PlayerName => _playerName;
    public int LocalPlayerId => _localPlayerId;
    public string AccessLabel => _isPrivateRoom ? "Password protected" : "Public";
    public string ActiveRoomDisplayName => string.IsNullOrWhiteSpace(_connectedRoomName) ? _roomCode : _connectedRoomName;
    public string RoomState => _roomState;
    public bool IsHost => _isHost;
    public bool CanStartGame => _canStartGame;
    public string PingDisplay => GetPingDisplayText();
    public CoopScreenMode ScreenMode => CoopUserSettings.ScreenMode;
    public bool ShowPing => CoopUserSettings.ShowPing;
    public float MouseSensitivity => SettingsManager.MouseSensitivity;

    public void AttachCanvasUi()
    {
        // Когда Canvas подключен, часть экранов перестает рисоваться через OnGUI.
        _useCanvasUi = true;
    }

    public void DetachCanvasUi()
    {
        _useCanvasUi = false;
    }

    public void SelectLocalScenario()
    {
        // Команды ниже вызываются кнопками и полями Canvas UI.
        _scenario = ConnectionScenario.Local;
        _status = "Local mode selected. Enter the host address and port of the relay server.";
    }

    public void SelectNetworkScenario()
    {
        _scenario = ConnectionScenario.Network;
        _status = "Network mode selected. Players connect through the remote relay server.";
    }

    public void OpenCreateRoomScreen()
    {
        _screen = MenuScreen.CreateRoom;
        _status = _scenario == ConnectionScenario.Local
            ? "Create a local room."
            : "Create a network room with a name and access type.";
    }

    public void OpenJoinRoomScreen()
    {
        _screen = MenuScreen.JoinRoom;
        _status = "Enter the room code to connect.";
    }

    public void BackToMainMenu()
    {
        _screen = MenuScreen.MainMenu;
        _status = "Back at the main menu.";
    }

    public void DisconnectAndReturnToMenu()
    {
        // Для UI не важно, как именно закрывается сессия, поэтому держим единый публичный метод.
        ShutdownSession();
        ResetToMenu();
    }

    public void LeaveRoom()
    {
        ShutdownSession();
        ResetToMenu();
    }

    public void OpenPauseSettings()
    {
        _isSettingsMenuOpen = true;
    }

    public void ClosePauseSettings()
    {
        _isSettingsMenuOpen = false;
    }

    public void SetFullscreenMode()
    {
        CoopUserSettings.SetScreenMode(CoopScreenMode.Fullscreen);
    }

    public void SetWindowedMode()
    {
        CoopUserSettings.SetScreenMode(CoopScreenMode.Windowed);
    }

    public void SetScreenMode(CoopScreenMode mode)
    {
        CoopUserSettings.SetScreenMode(mode);
    }

    public void SetShowPing(bool value)
    {
        CoopUserSettings.SetShowPing(value);
    }

    public void SetMouseSensitivity(float value)
    {
        SettingsManager.SetMouseSensitivity(value);
    }

    public void SetPlayerName(string value)
    {
        _playerName = value ?? string.Empty;
    }

    public void SetLocalHostAddress(string value)
    {
        _localHostAddress = value ?? string.Empty;
    }

    public void SetPortText(string value)
    {
        _portText = value ?? string.Empty;
    }

    public void SetRoomCode(string value)
    {
        _roomCode = value ?? string.Empty;
    }

    public void SetRoomName(string value)
    {
        _roomName = value ?? string.Empty;
    }

    public void SetRoomPassword(string value)
    {
        _roomPassword = value ?? string.Empty;
    }

    public void SetPrivateRoom(bool value)
    {
        // Для публичной сетевой комнаты пароль сразу очищаем, чтобы UI не тянул лишнее состояние.
        _isPrivateRoom = value;
        if (!_isPrivateRoom && _scenario == ConnectionScenario.Network)
        {
            _roomPassword = string.Empty;
        }
    }

    public void TryCreateRoomFromUi()
    {
        CreateRoom();
    }

    public void TryJoinRoomFromUi()
    {
        JoinRoom();
    }

    public void TryStartGameFromUi()
    {
        StartGame();
    }

    public void ResumeGameplayFromUi()
    {
        ResumeGameplay();
    }

    public void ExitGameFromUi()
    {
        ExitGame();
    }
}
