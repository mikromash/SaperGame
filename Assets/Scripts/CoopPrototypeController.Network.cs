public sealed partial class CoopPrototypeController
{
    private void CreateRoom()
    {
        // Создание комнаты поднимает локальный relay при необходимости и открывает host-сессию.
        if (!TryParsePort(out int port))
        {
            AudioController.Play(AudioEvent.UiError);
            _status = "Relay port must be a number between 1 and 65535.";
            return;
        }

        if (string.IsNullOrWhiteSpace(GetSelectedRelayHost()))
        {
            AudioController.Play(AudioEvent.UiError);
            _status = "Host address is required.";
            return;
        }

        if (!TryValidateMineCount(out int mineCount, out string mineCountError))
        {
            AudioController.Play(AudioEvent.UiError);
            _status = mineCountError;
            return;
        }

        if (!TryValidateFieldSize(out int fieldSize, out string fieldSizeError))
        {
            AudioController.Play(AudioEvent.UiError);
            _status = fieldSizeError;
            return;
        }

        ShutdownSession();
        ClearAvatars();

        string roomName = _scenario == ConnectionScenario.Network ? SanitizeRoomName(_roomName) : "Local Room";
        string roomPassword = _scenario == ConnectionScenario.Network && _isPrivateRoom
            ? _roomPassword
            : (_scenario == ConnectionScenario.Local ? _roomPassword : string.Empty);

        if (_scenario == ConnectionScenario.Network && _isPrivateRoom && string.IsNullOrWhiteSpace(roomPassword))
        {
            AudioController.Play(AudioEvent.UiError);
            _status = "Private network rooms require a password.";
            return;
        }

        if (_scenario == ConnectionScenario.Local)
        {
            _localRelayServer = new CoopEmbeddedRelayServer();

            try
            {
                _localRelayServer.Start(port);
            }
            catch (System.Exception exception)
            {
                AudioController.Play(AudioEvent.UiError);
                _status = "Could not start local relay on port " + port + ": " + exception.Message;
                _localRelayServer = null;
                return;
            }
        }

        _relayClient = new CoopRelayClient();
        bool connected = _relayClient.CreateRoom(
            GetSelectedRelayHost().Trim(),
            port,
            SanitizePlayerName(_playerName),
            roomName,
            _scenario == ConnectionScenario.Network ? _isPrivateRoom : !string.IsNullOrWhiteSpace(roomPassword),
            roomPassword,
            mineCount,
            fieldSize);

        if (!connected)
        {
            AudioController.Play(AudioEvent.UiError);
            string target = GetSelectedRelayHost().Trim() + ":" + port;
            _status = string.IsNullOrWhiteSpace(_relayClient.Status)
                ? "Could not create a room on " + target + "."
                : _relayClient.Status + " Target: " + target;
            ShutdownSession();
            return;
        }

        _roomCode = _relayClient.ConnectedRoomCode;
        _connectedRoomName = roomName;
        _localPlayerId = _relayClient.LocalPlayerId;
        _roomState = "waiting_for_player";
        _isHost = true;
        _canStartGame = false;
        _mineCount = mineCount;
        _mineCountText = mineCount.ToString();
        _fieldSize = fieldSize;
        _hasSelectedFieldSize = true;
        _status = BuildRoomStatus();
        AudioController.Play(AudioEvent.RoomCreated);
        UnityEngine.Debug.Log($"[CoopLobby] Host created room. roomCode={_roomCode}, localPlayerId={_localPlayerId}, nextScreen={MenuScreen.WaitingRoom}, scenario={_scenario}");
        _isPauseMenuOpen = false;
        _isSettingsMenuOpen = false;
        _screen = MenuScreen.WaitingRoom;
    }

    private void JoinRoom()
    {
        // Подключение к комнате выполняется через relay-клиент по существующему room code.
        if (!TryParsePort(out int port))
        {
            AudioController.Play(AudioEvent.UiError);
            _status = "Relay port must be a number between 1 and 65535.";
            return;
        }

        if (string.IsNullOrWhiteSpace(GetSelectedRelayHost()))
        {
            AudioController.Play(AudioEvent.UiError);
            _status = "Host address is required.";
            return;
        }

        ShutdownSession();
        ClearAvatars();

        string sanitizedRoomCode = SanitizeRoomCode(_roomCode);
        _relayClient = new CoopRelayClient();
        bool connected = _relayClient.JoinRoom(
            GetSelectedRelayHost().Trim(),
            port,
            sanitizedRoomCode,
            SanitizePlayerName(_playerName),
            _roomPassword);

        if (!connected)
        {
            AudioController.Play(AudioEvent.UiError);
            string target = GetSelectedRelayHost().Trim() + ":" + port;
            _status = string.IsNullOrWhiteSpace(_relayClient.Status)
                ? "Could not join the room on " + target + "."
                : _relayClient.Status + " Target: " + target;
            ShutdownSession();
            return;
        }

        _roomCode = _relayClient.ConnectedRoomCode;
        _localPlayerId = _relayClient.LocalPlayerId;
        _roomState = "player_joined";
        _isHost = false;
        _canStartGame = false;
        _status = BuildRoomStatus();
        AudioController.Play(AudioEvent.RoomJoined);
        UnityEngine.Debug.Log($"[CoopLobby] Guest joined room. roomCode={_roomCode}, localPlayerId={_localPlayerId}, nextScreen={MenuScreen.WaitingRoom}, scenario={_scenario}");
        _isPauseMenuOpen = false;
        _isSettingsMenuOpen = false;
        _screen = MenuScreen.WaitingRoom;
    }

    private void StartGame()
    {
        // Старт матча разрешен только хосту и только когда второй игрок уже подключился.
        if (_relayClient == null)
        {
            AudioController.Play(AudioEvent.UiError);
            _status = "\u041d\u0435\u0442 \u0430\u043a\u0442\u0438\u0432\u043d\u043e\u0433\u043e \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0438\u044f \u043a \u043a\u043e\u043c\u043d\u0430\u0442\u0435.";
            UnityEngine.Debug.LogWarning("[CoopLobby] StartGame ignored because relay client is null.");
            return;
        }

        if (!_isHost)
        {
            AudioController.Play(AudioEvent.UiError);
            _status = "\u0422\u043e\u043b\u044c\u043a\u043e \u0445\u043e\u0441\u0442 \u043c\u043e\u0436\u0435\u0442 \u043d\u0430\u0447\u0430\u0442\u044c \u0438\u0433\u0440\u0443.";
            UnityEngine.Debug.LogWarning($"[CoopLobby] StartGame rejected for non-host playerId={_localPlayerId}.");
            return;
        }

        if (!_canStartGame)
        {
            AudioController.Play(AudioEvent.UiError);
            _status = "\u0412\u0442\u043e\u0440\u043e\u0439 \u0438\u0433\u0440\u043e\u043a \u0435\u0449\u0451 \u043d\u0435 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0438\u043b\u0441\u044f.";
            UnityEngine.Debug.LogWarning($"[CoopLobby] StartGame blocked because canStartGame=false. roomCode={_roomCode}, roomState={_roomState}");
            return;
        }

        UnityEngine.Debug.Log($"[CoopLobby] Host requested game start. roomCode={_roomCode}, roomState={_roomState}, playerId={_localPlayerId}");
        AudioController.Play(AudioEvent.GameStarted);
        _relayClient.SendStartGameRequest();
        _status = "\u0417\u0430\u043f\u0443\u0441\u043a \u043c\u0430\u0442\u0447\u0430...";
    }

    public bool TryUpdateRoomSettings(int mineCount, int fieldSize)
    {
        if (_relayClient == null || _relayClient.ConnectionState != PlayerConnectionState.Connected)
        {
            _status = "There is no active room connection.";
            AudioController.Play(AudioEvent.UiError);
            return false;
        }

        if (!_isHost)
        {
            _status = "Only the host can change room settings.";
            AudioController.Play(AudioEvent.UiError);
            return false;
        }

        if (_roomState == "in_game")
        {
            _status = "Room settings cannot be changed after the game starts.";
            AudioController.Play(AudioEvent.UiError);
            return false;
        }

        if (mineCount < MinMineCount || mineCount > MaxMineCount)
        {
            _status = $"Mine count must be between {MinMineCount} and {MaxMineCount}.";
            AudioController.Play(AudioEvent.UiError);
            return false;
        }

        if (!IsValidFieldSize(fieldSize))
        {
            _status = fieldSize == 0 ? "Field size is not specified" : "Invalid field size.";
            AudioController.Play(AudioEvent.UiError);
            return false;
        }

        _relayClient.SendRoomSettingsUpdate(mineCount, fieldSize);
        _status = "Updating room settings...";
        return true;
    }

    public bool IsMinesweeperSyncActive
    {
        get
        {
            return _screen == MenuScreen.InGame &&
                   _relayClient != null &&
                   _relayClient.ConnectionState == PlayerConnectionState.Connected;
        }
    }

    public void SendMinesweeperCommand(string action, int cellX = -1, int cellY = -1, int boardSeed = 0)
    {
        if (!IsMinesweeperSyncActive)
        {
            return;
        }

        _relayClient.SendMinesweeperCommand(action, cellX, cellY, boardSeed);
    }

    private void ApplyMinesweeperCommand(CoopNetworkMessage message)
    {
        Minesweeper.GameController controller = UnityEngine.Object.FindAnyObjectByType<Minesweeper.GameController>();
        if (controller == null)
        {
            return;
        }

        controller.HandleNetworkCommand(
            message.MinesweeperAction,
            message.CellX,
            message.CellY,
            message.BoardSeed);
    }

    private void ShutdownSession()
    {
        // Корректно закрываем и клиент, и локальный relay-сервер.
        _relayClient?.Disconnect();
        _relayClient = null;

        _localRelayServer?.Stop();
        _localRelayServer = null;
    }

    private bool TryParsePort(out int port)
    {
        // Принимаем только валидный пользовательский порт.
        return int.TryParse(_portText, out port) && port > 0 && port <= 65535;
    }

    private bool TryValidateMineCount(out int mineCount, out string error)
    {
        mineCount = DefaultMineCount;

        if (string.IsNullOrWhiteSpace(_mineCountText))
        {
            error = $"Mine count is required. Enter a number from {MinMineCount} to {MaxMineCount}.";
            return false;
        }

        if (!int.TryParse(_mineCountText.Trim(), out mineCount))
        {
            error = $"Mine count must be a whole number from {MinMineCount} to {MaxMineCount}.";
            return false;
        }

        if (mineCount < MinMineCount)
        {
            error = $"Mine count must be at least {MinMineCount}.";
            return false;
        }

        if (mineCount > MaxMineCount)
        {
            error = $"Mine count must be no more than {MaxMineCount}.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool TryValidateFieldSize(out int fieldSize, out string error)
    {
        fieldSize = _fieldSize;

        if (!_hasSelectedFieldSize)
        {
            error = "Field size is not specified";
            return false;
        }

        if (!IsValidFieldSize(fieldSize))
        {
            error = "Invalid field size.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string SanitizePlayerName(string value)
    {
        // Ограничиваем имя игрока по длине и не допускаем пустое значение.
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Player";
        }

        string trimmed = value.Trim();
        return trimmed.Length > 24 ? trimmed.Substring(0, 24) : trimmed;
    }
}
