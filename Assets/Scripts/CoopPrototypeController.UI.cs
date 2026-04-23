using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class CoopPrototypeController
{
    // Снимок waiting-room UI, чтобы не размазывать состояние по нескольким методам отрисовки.
    private struct WaitingRoomMenuState
    {
        public bool IsVisible;
        public bool IsHost;
        public bool CanStartGame;
        public string Title;
        public string Message;
        public string RoomCode;
    }

    private WaitingRoomMenuState _waitingRoomMenuState;

    private void OnGUI()
    {
        // Canvas обслуживает только ранние экраны, остальные окна пока рисуются fallback-логикой.
        bool useCanvasForCurrentScreen =
            _useCanvasUi &&
            (_screen == MenuScreen.MainMenu ||
             _screen == MenuScreen.CreateRoom ||
             _screen == MenuScreen.JoinRoom);

        if (useCanvasForCurrentScreen)
        {
            return;
        }

        GUI.color = Color.white;

        // В геймплее и waiting room рисуем отдельные overlay-панели.
        if (_screen == MenuScreen.InGame)
        {
            DrawRoomOverlay();
            if (_isPauseMenuOpen)
            {
                DrawPauseMenu();
            }

            return;
        }

        if (_screen == MenuScreen.WaitingRoom)
        {
            UpdateWaitingRoomMenuState();
            DrawWaitingRoomOverlay();
            return;
        }

        float width = 500f;
        float height = 430f;
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

        GUI.Box(panel, "Cooperative Connection");

        GUILayout.BeginArea(new Rect(panel.x + 20f, panel.y + 40f, panel.width - 40f, panel.height - 60f));
        GUILayout.Label("Status: " + _status, WrapLabelStyle());
        GUILayout.Space(12f);

        // Старое меню создания/подключения комнаты оставлено как fallback до полной миграции на Canvas.
        switch (_screen)
        {
            case MenuScreen.MainMenu:
                DrawMainMenu();
                break;
            case MenuScreen.CreateRoom:
                DrawCreateRoom();
                break;
            case MenuScreen.JoinRoom:
                DrawJoinRoom();
                break;
        }

        GUILayout.EndArea();
    }

    private void DrawMainMenu()
    {
        // Выбор сценария подключения и переход к действиям комнаты.
        GUILayout.Label("Choose a connection scenario:");
        GUILayout.Space(10f);

        if (GUILayout.Button("Local Connection", GUILayout.Height(42f)))
        {
            _scenario = ConnectionScenario.Local;
            _status = "Local mode selected. Enter the host address and port of the relay server.";
        }

        if (GUILayout.Button("Network Connection", GUILayout.Height(42f)))
        {
            _scenario = ConnectionScenario.Network;
            _status = "Network mode selected. Players connect through the remote relay server.";
        }

        GUILayout.Space(18f);
        GUILayout.Label("Current mode: " + GetScenarioLabel());
        GUILayout.Space(12f);

        if (GUILayout.Button("Create Room", GUILayout.Height(38f)))
        {
            _screen = MenuScreen.CreateRoom;
            _status = _scenario == ConnectionScenario.Local
                ? "Create a local room."
                : "Create a network room with a name and access type.";
        }

        if (GUILayout.Button("Join Room", GUILayout.Height(38f)))
        {
            _screen = MenuScreen.JoinRoom;
            _status = "Enter the room code to connect.";
        }
    }

    private void DrawCreateRoom()
    {
        // Форма создания комнаты меняется в зависимости от локального или сетевого режима.
        GUILayout.Label("Mode: " + GetScenarioLabel());
        GUILayout.Label("Player name:");
        _playerName = GUILayout.TextField(_playerName, 24);

        if (_scenario == ConnectionScenario.Local)
        {
            GUILayout.Label("Host address:");
            _localHostAddress = GUILayout.TextField(_localHostAddress, 32);
            GUILayout.Label("Port:");
            _portText = GUILayout.TextField(_portText, 8);
            GUILayout.Label("Room password:");
            _roomPassword = GUILayout.PasswordField(_roomPassword, '*', 24);
        }
        else
        {
            GUILayout.Space(4f);
            GUILayout.Label("Relay server: " + GetSelectedRelayHost() + ":" + _portText, WrapLabelStyle());
        }

        if (_scenario == ConnectionScenario.Network)
        {
            GUILayout.Space(8f);
            GUILayout.Label("Room name:");
            _roomName = GUILayout.TextField(_roomName, 32);
            GUILayout.Label("Room access:");

            bool publicRoom = !_isPrivateRoom;
            bool newPublicRoom = GUILayout.Toggle(publicRoom, "Public room");
            bool newPrivateRoom = GUILayout.Toggle(_isPrivateRoom, "Room with password");

            if (newPublicRoom && !publicRoom)
            {
                _isPrivateRoom = false;
                _roomPassword = string.Empty;
            }
            else if (newPrivateRoom && !_isPrivateRoom)
            {
                _isPrivateRoom = true;
            }

            if (_isPrivateRoom)
            {
                GUILayout.Label("Password:");
                _roomPassword = GUILayout.PasswordField(_roomPassword, '*', 24);
            }
        }

        GUILayout.Space(12f);
        if (GUILayout.Button("Create", GUILayout.Height(38f)))
        {
            CreateRoom();
        }

        if (GUILayout.Button("Back", GUILayout.Height(32f)))
        {
            _screen = MenuScreen.MainMenu;
            _status = "Back at the main menu.";
        }
    }

    private void DrawJoinRoom()
    {
        // Экран подключения к уже существующей комнате.
        GUILayout.Label("Mode: " + GetScenarioLabel());
        GUILayout.Label("Player name:");
        _playerName = GUILayout.TextField(_playerName, 24);

        if (_scenario == ConnectionScenario.Local)
        {
            GUILayout.Label("Host address:");
            _localHostAddress = GUILayout.TextField(_localHostAddress, 32);
            GUILayout.Label("Port:");
            _portText = GUILayout.TextField(_portText, 8);
        }
        else
        {
            GUILayout.Space(4f);
            GUILayout.Label("Relay server: " + GetSelectedRelayHost() + ":" + _portText, WrapLabelStyle());
        }

        GUILayout.Label("Room code:");
        _roomCode = GUILayout.TextField(_roomCode, 16);

        GUILayout.Label(_scenario == ConnectionScenario.Local ? "Room password:" : "Password if needed:");
        _roomPassword = GUILayout.PasswordField(_roomPassword, '*', 24);

        GUILayout.Space(12f);
        if (GUILayout.Button("Join", GUILayout.Height(38f)))
        {
            JoinRoom();
        }

        if (GUILayout.Button("Back", GUILayout.Height(32f)))
        {
            _screen = MenuScreen.MainMenu;
            _status = "Back at the main menu.";
        }
    }

    private void DrawWaitingRoomOverlay()
    {
        // Waiting room нужен до старта матча, пока для него нет отдельной Canvas-панели.
        if (!_waitingRoomMenuState.IsVisible)
        {
            HideWaitingRoomMenu();
            return;
        }

        float width = 560f;
        float height = 360f;
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

        GUI.Box(panel, "Waiting Room");
        GUILayout.BeginArea(new Rect(panel.x + 20f, panel.y + 38f, panel.width - 40f, panel.height - 56f));
        GUILayout.Label("Mode: " + GetScenarioLabel());
        GUILayout.Label("Relay: " + GetSelectedRelayHost() + ":" + _portText);
        GUILayout.Label("Room: " + ActiveRoomDisplayName);
        GUILayout.Label("Room code: " + _roomCode);
        GUILayout.Label("Access: " + (_isPrivateRoom ? "Password protected" : "Public"));
        GUILayout.Label("Ping: " + GetPingDisplayText());
        GUILayout.Space(10f);
        GUILayout.Label("Status: " + _status, WrapLabelStyle());
        GUILayout.Space(14f);

        if (_waitingRoomMenuState.IsHost)
        {
            DrawHostWaitingRoomMenu();
        }
        else
        {
            DrawClientWaitingRoomMenu();
        }

        GUILayout.Space(18f);
        if (GUILayout.Button("Disconnect", GUILayout.Height(32f)))
        {
            ShutdownSession();
            ResetToMenu();
            HideWaitingRoomMenu();
            Debug.Log("[CoopLobby] Waiting room closed by local disconnect.");
        }

        GUILayout.EndArea();
    }

    private void DrawHostWaitingRoomMenu()
    {
        // У хоста есть дополнительная кнопка старта матча.
        GUILayout.Label(_waitingRoomMenuState.Title, WrapLabelStyle());
        GUILayout.Label("Code: " + _waitingRoomMenuState.RoomCode);

        if (!string.IsNullOrWhiteSpace(_waitingRoomMenuState.Message))
        {
            GUILayout.Space(6f);
            GUILayout.Label(_waitingRoomMenuState.Message, WrapLabelStyle());
        }

        GUILayout.Space(12f);
        GUI.enabled = _waitingRoomMenuState.CanStartGame;
        if (GUILayout.Button("Start Game", GUILayout.Height(38f)))
        {
            StartGame();
        }

        GUI.enabled = true;
    }

    private void DrawClientWaitingRoomMenu()
    {
        // У клиента waiting room только информационный.
        GUILayout.Label(_waitingRoomMenuState.Title, WrapLabelStyle());

        if (!string.IsNullOrWhiteSpace(_waitingRoomMenuState.Message))
        {
            GUILayout.Space(6f);
            GUILayout.Label(_waitingRoomMenuState.Message, WrapLabelStyle());
        }
    }

    private void UpdateWaitingRoomMenuState()
    {
        // Формируем текстовое состояние waiting room из текущих данных контроллера.
        if (_screen != MenuScreen.WaitingRoom)
        {
            HideWaitingRoomMenu();
            return;
        }

        _waitingRoomMenuState.IsVisible = true;
        _waitingRoomMenuState.IsHost = _isHost;
        _waitingRoomMenuState.CanStartGame = _canStartGame;
        _waitingRoomMenuState.RoomCode = _roomCode;

        if (_isHost)
        {
            if (_roomState == "player_joined")
            {
                _waitingRoomMenuState.Title = "Second player connected";
                _waitingRoomMenuState.Message = "The host can start the match manually.";
            }
            else
            {
                _waitingRoomMenuState.Title = "Waiting for the second player";
                _waitingRoomMenuState.Message = "The room is ready. Share the room code with the second player.";
            }

            return;
        }

        _waitingRoomMenuState.Title = "Waiting for the host to start the match";
        _waitingRoomMenuState.Message = "The match will start automatically when the host presses Start Game.";
    }

    private void HideWaitingRoomMenu()
    {
        _waitingRoomMenuState = default;
    }

    private void DrawRoomOverlay()
    {
        // Базовый ingame overlay с информацией о комнате и быстрым выходом.
        Rect overlay = new Rect(16f, 16f, 460f, 240f);
        GUI.Box(overlay, "Room Connected");
        GUILayout.BeginArea(new Rect(overlay.x + 16f, overlay.y + 28f, overlay.width - 32f, overlay.height - 40f));
        GUILayout.Label("Mode: " + GetScenarioLabel());
        GUILayout.Label("Relay: " + GetSelectedRelayHost() + ":" + _portText);
        GUILayout.Label("Room: " + (string.IsNullOrWhiteSpace(_connectedRoomName) ? _roomCode : _connectedRoomName));
        GUILayout.Label("Room code: " + _roomCode);
        GUILayout.Label("Access: " + (_isPrivateRoom ? "Password protected" : "Public"));
        GUILayout.Label("Player id: " + _localPlayerId);
        GUILayout.Label("Ping: " + GetPingDisplayText());
        GUILayout.Label("Move: WASD / arrows");
        GUILayout.Label("Status: " + _status, WrapLabelStyle());

        if (GUILayout.Button("Disconnect", GUILayout.Height(28f)))
        {
            ShutdownSession();
            ResetToMenu();
            HideWaitingRoomMenu();
        }

        GUILayout.EndArea();
    }

    private void DrawPauseMenu()
    {
        // Пауза полностью локальная и не должна ломать сетевое состояние комнаты.
        DrawModalBackdrop();

        float width = 380f;
        float height = _isSettingsMenuOpen ? 320f : 260f;
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

        GUI.Box(panel, "Pause");
        GUILayout.BeginArea(new Rect(panel.x + 20f, panel.y + 40f, panel.width - 40f, panel.height - 60f));

        if (_isSettingsMenuOpen)
        {
            DrawPauseSettings();
        }
        else
        {
            if (GUILayout.Button("Resume", GUILayout.Height(38f)))
            {
                ResumeGameplay();
            }

            if (GUILayout.Button("Window Settings", GUILayout.Height(38f)))
            {
                _isSettingsMenuOpen = true;
            }

            if (GUILayout.Button("Leave Room", GUILayout.Height(38f)))
            {
                ShutdownSession();
                ResetToMenu();
            }

            if (GUILayout.Button("Exit Game", GUILayout.Height(38f)))
            {
                ExitGame();
            }
        }

        GUILayout.EndArea();
    }

    private void DrawPauseSettings()
    {
        // Простейшие оконные настройки, доступные прямо из паузы.
        GUILayout.Label("Window mode");
        GUILayout.Space(10f);

        if (GUILayout.Button("Fullscreen", GUILayout.Height(38f)))
        {
            Screen.fullScreen = true;
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
        }

        if (GUILayout.Button("Windowed", GUILayout.Height(38f)))
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.fullScreen = false;
        }

        GUILayout.Space(16f);
        if (GUILayout.Button("Back", GUILayout.Height(34f)))
        {
            _isSettingsMenuOpen = false;
        }
    }

    private void DrawModalBackdrop()
    {
        // Затемнение фона под модальным меню.
        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private void HandlePauseInput()
    {
        // Escape открывает паузу или закрывает вложенное окно настроек.
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        if (_isPauseMenuOpen)
        {
            if (_isSettingsMenuOpen)
            {
                _isSettingsMenuOpen = false;
            }
            else
            {
                ResumeGameplay();
            }

            return;
        }

        _isPauseMenuOpen = true;
        _isSettingsMenuOpen = false;
    }

    private void ResumeGameplay()
    {
        // Возврат из паузы без изменения сетевого состояния.
        _isPauseMenuOpen = false;
        _isSettingsMenuOpen = false;
    }

    private void ExitGame()
    {
        // Единая точка выхода для editor/runtime.
        ShutdownSession();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static GUIStyle WrapLabelStyle()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.wordWrap = true;
        return style;
    }
}
