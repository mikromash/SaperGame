using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class CoopPrototypeController
{
    private void OnGUI()
    {
        if (_useCanvasUi)
        {
            return;
        }

        GUI.color = Color.white;

        if (_screen == MenuScreen.InGame)
        {
            DrawInGameOverlay();
            if (_isPauseMenuOpen)
            {
                DrawPauseMenu();
            }

            return;
        }

        float width = 500f;
        float height = 430f;
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

        GUI.Box(panel, "Cooperative Connection");

        GUILayout.BeginArea(new Rect(panel.x + 20f, panel.y + 40f, panel.width - 40f, panel.height - 60f));
        GUILayout.Label("Status: " + _status, WrapLabelStyle());
        GUILayout.Space(12f);

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

    private void DrawInGameOverlay()
    {
        Rect overlay = new Rect(16f, 16f, 460f, 180f);
        GUI.Box(overlay, "Room Connected");
        GUILayout.BeginArea(new Rect(overlay.x + 16f, overlay.y + 28f, overlay.width - 32f, overlay.height - 40f));
        GUILayout.Label("Mode: " + GetScenarioLabel());
        GUILayout.Label("Relay: " + GetSelectedRelayHost() + ":" + _portText);
        GUILayout.Label("Room: " + (string.IsNullOrWhiteSpace(_connectedRoomName) ? _roomCode : _connectedRoomName));
        GUILayout.Label("Room code: " + _roomCode);
        GUILayout.Label("Access: " + (_isPrivateRoom ? "Password protected" : "Public"));
        GUILayout.Label("Player id: " + _localPlayerId);
        GUILayout.Label("Move: WASD / arrows");
        GUILayout.Label("Status: " + _status, WrapLabelStyle());

        if (GUILayout.Button("Disconnect", GUILayout.Height(28f)))
        {
            ShutdownSession();
            ResetToMenu();
        }

        GUILayout.EndArea();
    }

    private void DrawPauseMenu()
    {
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
        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private void HandlePauseInput()
    {
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
        _isPauseMenuOpen = false;
        _isSettingsMenuOpen = false;
    }

    private void ExitGame()
    {
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
