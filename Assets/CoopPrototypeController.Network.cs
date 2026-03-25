public sealed partial class CoopPrototypeController
{
    private void CreateRoom()
    {
        if (!TryParsePort(out int port))
        {
            _status = "Relay port must be a number between 1 and 65535.";
            return;
        }

        if (string.IsNullOrWhiteSpace(GetSelectedRelayHost()))
        {
            _status = "Host address is required.";
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
            roomPassword);

        if (!connected)
        {
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
        _status = "Room created. Share the room code with the other player.";
        _isPauseMenuOpen = false;
        _isSettingsMenuOpen = false;
        _screen = MenuScreen.InGame;
    }

    private void JoinRoom()
    {
        if (!TryParsePort(out int port))
        {
            _status = "Relay port must be a number between 1 and 65535.";
            return;
        }

        if (string.IsNullOrWhiteSpace(GetSelectedRelayHost()))
        {
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
            string target = GetSelectedRelayHost().Trim() + ":" + port;
            _status = string.IsNullOrWhiteSpace(_relayClient.Status)
                ? "Could not join the room on " + target + "."
                : _relayClient.Status + " Target: " + target;
            ShutdownSession();
            return;
        }

        _roomCode = _relayClient.ConnectedRoomCode;
        _localPlayerId = _relayClient.LocalPlayerId;
        _status = "Joined room " + _roomCode + ".";
        _isPauseMenuOpen = false;
        _isSettingsMenuOpen = false;
        _screen = MenuScreen.InGame;
    }

    private void ShutdownSession()
    {
        _relayClient?.Disconnect();
        _relayClient = null;

        _localRelayServer?.Stop();
        _localRelayServer = null;
    }

    private bool TryParsePort(out int port)
    {
        return int.TryParse(_portText, out port) && port > 0 && port <= 65535;
    }

    private static string SanitizePlayerName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Player";
        }

        string trimmed = value.Trim();
        return trimmed.Length > 24 ? trimmed.Substring(0, 24) : trimmed;
    }
}
