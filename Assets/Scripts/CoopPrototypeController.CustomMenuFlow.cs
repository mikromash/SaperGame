using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class CoopPrototypeController
{
    private enum CustomMenuLaunchMode
    {
        None,
        OpenLobby,
        Host,
        Join
    }

    private CustomMenuLaunchMode _pendingCustomMenuLaunchMode;

    public void OpenLobbyFromCustomMenu()
    {
        QueueCustomMenuLaunch(CustomMenuLaunchMode.OpenLobby, _scenario, _playerName, string.Empty, _roomName, _isPrivateRoom, _roomPassword);
    }

    public void HostLocalGameFromCustomMenu(string playerName = "", string roomName = "", string password = "")
    {
        QueueCustomMenuLaunch(CustomMenuLaunchMode.Host, ConnectionScenario.Local, playerName, string.Empty, roomName, false, password);
    }

    public void HostNetworkGameFromCustomMenu(string playerName = "", string roomName = "", bool isPrivate = false, string password = "")
    {
        QueueCustomMenuLaunch(CustomMenuLaunchMode.Host, ConnectionScenario.Network, playerName, string.Empty, roomName, isPrivate, password);
    }

    public void JoinLocalGameFromCustomMenu(string roomCode, string playerName = "", string password = "")
    {
        QueueCustomMenuLaunch(CustomMenuLaunchMode.Join, ConnectionScenario.Local, playerName, roomCode, _roomName, false, password);
    }

    public void JoinNetworkGameFromCustomMenu(string roomCode, string playerName = "", string password = "")
    {
        QueueCustomMenuLaunch(CustomMenuLaunchMode.Join, ConnectionScenario.Network, playerName, roomCode, _roomName, false, password);
    }

    public void ExitGameFromCustomMenu()
    {
        Debug.Log("[CoopCustomMenu] Exit requested.");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void QueueCustomMenuLaunch(
        CustomMenuLaunchMode launchMode,
        ConnectionScenario scenario,
        string playerName,
        string roomCode,
        string roomName,
        bool isPrivate,
        string password)
    {
        _pendingCustomMenuLaunchMode = launchMode;
        _scenario = scenario;

        if (!string.IsNullOrWhiteSpace(playerName))
        {
            _playerName = playerName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(roomCode))
        {
            _roomCode = roomCode.Trim().ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(roomName))
        {
            _roomName = roomName.Trim();
        }

        _isPrivateRoom = isPrivate;
        _roomPassword = password ?? string.Empty;

        Debug.Log($"[CoopCustomMenu] Queued launch. mode={_pendingCustomMenuLaunchMode}, scenario={_scenario}, roomCode={_roomCode}");
        SceneManager.LoadScene(LobbySceneName);
    }

    private void TryRunPendingCustomMenuLaunch()
    {
        if (_pendingCustomMenuLaunchMode == CustomMenuLaunchMode.None)
        {
            return;
        }

        CustomMenuLaunchMode launchMode = _pendingCustomMenuLaunchMode;
        _pendingCustomMenuLaunchMode = CustomMenuLaunchMode.None;

        Debug.Log($"[CoopCustomMenu] Running pending launch in lobby. mode={launchMode}, scenario={_scenario}");

        switch (launchMode)
        {
            case CustomMenuLaunchMode.OpenLobby:
                _screen = MenuScreen.MainMenu;
                _status = "Lobby opened from custom menu.";
                break;
            case CustomMenuLaunchMode.Host:
                CreateRoom();
                break;
            case CustomMenuLaunchMode.Join:
                JoinRoom();
                break;
        }
    }
}
