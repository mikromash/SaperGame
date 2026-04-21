using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class CoopPrototypeController : MonoBehaviour
{
    private const float MoveSpeed = 6f;
    private const float LocalReconciliationThreshold = 0.75f;
    private const float MoveSendIntervalSeconds = 1f / 30f;
    private const float RemoteInterpolationMinDuration = 0.04f;
    private const float RemoteInterpolationMaxDuration = 0.12f;
    private const float RemoteExtrapolationDuration = 0.08f;
    private const string LobbySceneName = "LobbyScene";
    private const string GameplaySceneName = "GameplayScene";

    private enum MenuScreen
    {
        MainMenu,
        CreateRoom,
        JoinRoom,
        WaitingRoom,
        InGame
    }

    private enum ConnectionScenario
    {
        Local,
        Network
    }

    internal enum PlayerConnectionState
    {
        Disconnected,
        Connecting,
        Connected
    }

    private static CoopPrototypeController _instance;

    private readonly Dictionary<int, CoopAvatarView> _avatars = new Dictionary<int, CoopAvatarView>();
    private readonly Dictionary<int, CoopScenePlayerAvatar> _sceneAvatars = new Dictionary<int, CoopScenePlayerAvatar>();

    private CoopEmbeddedRelayServer _localRelayServer;
    private CoopRelayClient _relayClient;
    private CoopPlayerSnapshot[] _latestSnapshots = System.Array.Empty<CoopPlayerSnapshot>();
    private MenuScreen _screen = MenuScreen.MainMenu;
    private ConnectionScenario _scenario = ConnectionScenario.Local;
    private Camera _camera;
    private string _localHostAddress = "127.0.0.1";
    private string _relayHost = "127.0.0.1";
    private string _portText = "7777";
    private string _roomCode = string.Empty;
    private string _roomName = "My Room";
    private string _roomPassword = string.Empty;
    private string _playerName = "Player";
    private string _status = "Connect to the relay server and create or join a room.";
    private string _connectedRoomName = string.Empty;
    private string _roomState = "waiting_for_player";
    private int _localPlayerId;
    private bool _isPrivateRoom;
    private bool _isHost;
    private bool _canStartGame;
    private MenuScreen _lastLoggedScreen;
    private string _lastLoggedRoomState = string.Empty;
    private bool _lastLoggedIsHost;
    private bool _lastLoggedCanStartGame;
    private bool _isPauseMenuOpen;
    private bool _isSettingsMenuOpen;
    private float _lastMoveSentTime;
    private float _lastPingSentTime = -10f;
    private float _currentPingMs = -1f;
    private bool _isPingAvailable;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null)
        {
            return;
        }

        GameObject root = new GameObject("CoopPrototypeController");
        _instance = root.AddComponent<CoopPrototypeController>();
        DontDestroyOnLoad(root);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        Application.runInBackground = true;
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        Screen.fullScreenMode = FullScreenMode.Windowed;
        Screen.fullScreen = false;
        Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
        ApplyRelaySettings();
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void Update()
    {
        PumpRelayMessages();
        UpdatePingMeasurement();
        LogUiStateIfChanged();

        if (_screen == MenuScreen.InGame)
        {
            HandlePauseInput();
        }

        if (_screen == MenuScreen.InGame)
        {
            if (!_isPauseMenuOpen)
            {
                HandleMovement();
            }

            UpdateCamera();
        }

        foreach (KeyValuePair<int, CoopAvatarView> pair in _avatars)
        {
            CoopAvatarView avatar = pair.Value;
            if (avatar.SceneAvatar == null)
            {
                continue;
            }

            if (pair.Key == _localPlayerId)
            {
                avatar.SceneAvatar.Position = avatar.TargetPosition;
            }
            else
            {
                avatar.SceneAvatar.Position = EvaluateRemoteAvatarPosition(avatar);
            }

            avatar.SceneAvatar.FaceCamera(_camera);
        }
    }

    private Vector3 EvaluateRemoteAvatarPosition(CoopAvatarView avatar)
    {
        if (!avatar.HasRemoteInterpolation)
        {
            return avatar.TargetPosition;
        }

        float elapsed = Time.unscaledTime - avatar.InterpolationStartTime;
        if (avatar.InterpolationDuration <= 0f)
        {
            return avatar.InterpolationToPosition;
        }

        float normalized = Mathf.Clamp01(elapsed / avatar.InterpolationDuration);
        Vector3 interpolatedPosition = Vector3.Lerp(
            avatar.InterpolationFromPosition,
            avatar.InterpolationToPosition,
            normalized);

        float extrapolationTime = elapsed - avatar.InterpolationDuration;
        if (extrapolationTime > 0f && extrapolationTime <= RemoteExtrapolationDuration)
        {
            interpolatedPosition += avatar.ExtrapolatedVelocity * extrapolationTime;
        }

        return interpolatedPosition;
    }

    private void PumpRelayMessages()
    {
        if (_relayClient == null)
        {
            return;
        }

        while (_relayClient.TryDequeue(out CoopNetworkMessage message))
        {
            if (message == null)
            {
                continue;
            }

            if (message.Type == "RoomConnected")
            {
                UpdateRoomPresence(message);
                ApplySnapshot(message.Players);
                continue;
            }

            if (message.Type == "Snapshot")
            {
                UpdateRoomPresence(message);
                ApplySnapshot(message.Players);
                continue;
            }

            if (message.Type == "GameStarted")
            {
                UpdateRoomPresence(message);
                ApplySnapshot(message.Players);
                _status = "Match started.";
                Debug.Log($"[CoopLobby] GameStarted received. roomCode={_roomCode}, playerId={_localPlayerId}");
                TransitionToGameplayScene();
                continue;
            }

            if (message.Type == "Pong")
            {
                CompletePingMeasurement(message.PingTicks);
                continue;
            }

            if (message.Type == "RoomClosed")
            {
                _status = string.IsNullOrWhiteSpace(message.Reason) ? "Room closed." : message.Reason;
                ShutdownSession();
                ResetToMenu();
                return;
            }

            if (message.Type == "Error")
            {
                _status = string.IsNullOrWhiteSpace(message.Reason) ? "Relay server returned an error." : message.Reason;
                Debug.LogWarning("[CoopLobby] Relay error: " + _status);
            }
        }

        if (_relayClient.ConnectionState == PlayerConnectionState.Disconnected &&
            (_screen == MenuScreen.InGame || _screen == MenuScreen.WaitingRoom))
        {
            _status = string.IsNullOrWhiteSpace(_relayClient.DisconnectStatus) ? "Connection closed." : _relayClient.DisconnectStatus;
            ShutdownSession();
            ResetToMenu();
        }
    }

    private void ResetToMenu()
    {
        ClearAvatars();
        _screen = MenuScreen.MainMenu;
        _localPlayerId = 0;
        _connectedRoomName = string.Empty;
        _roomState = "waiting_for_player";
        _isHost = false;
        _canStartGame = false;
        _isPauseMenuOpen = false;
        _isSettingsMenuOpen = false;
        _latestSnapshots = System.Array.Empty<CoopPlayerSnapshot>();
        ResetPingState();
        HideWaitingRoomMenu();
        TransitionToLobbyScene();
    }

    private void OnApplicationQuit()
    {
        ShutdownSession();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }
    }

    private static string SanitizeRoomCode(string value)
    {
        string trimmed = string.IsNullOrWhiteSpace(value) ? GenerateRoomCode() : value.Trim().ToUpperInvariant();
        return trimmed.Length > 12 ? trimmed.Substring(0, 12) : trimmed;
    }

    private static string GenerateRoomCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        StringBuilder builder = new StringBuilder(6);
        for (int index = 0; index < 6; index++)
        {
            builder.Append(alphabet[Random.Range(0, alphabet.Length)]);
        }

        return builder.ToString();
    }

    private static string SanitizeRoomName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Room";
        }

        string trimmed = value.Trim();
        return trimmed.Length > 32 ? trimmed.Substring(0, 32) : trimmed;
    }

    private void ApplyRelaySettings()
    {
        CoopRelaySettingsData settings = CoopRelaySettings.Load();
        _localHostAddress = GetLocalIPv4();
        _relayHost = settings.relayHost;
        _portText = settings.relayPort.ToString();
    }

    private string GetSelectedRelayHost()
    {
        return _scenario == ConnectionScenario.Local ? _localHostAddress : _relayHost;
    }

    private void UpdateRoomPresence(CoopNetworkMessage message)
    {
        _roomCode = SanitizeRoomCode(message.RoomCode);
        _connectedRoomName = SanitizeRoomName(message.RoomName);
        _isPrivateRoom = message.IsPrivate;
        _localPlayerId = message.PlayerId;
        _roomState = string.IsNullOrWhiteSpace(message.RoomState) ? "waiting_for_player" : message.RoomState;
        _isHost = message.IsHost;
        _canStartGame = message.CanStartGame;

        if (_roomState != "in_game")
        {
            _screen = MenuScreen.WaitingRoom;
        }

        _status = BuildRoomStatus();
        Debug.Log($"[CoopLobby] Room presence updated. screen={_screen}, state={_roomState}, roomCode={_roomCode}, isHost={_isHost}, canStart={_canStartGame}, playerId={_localPlayerId}");
    }

    private string BuildRoomStatus()
    {
        if (_roomState == "in_game")
        {
            return "Match started.";
        }

        if (_isHost)
        {
            if (_roomState == "player_joined")
            {
                return "Second player connected.";
            }

            return "Waiting for the second player.";
        }

        return "Waiting for the host to start the match.";
    }

    private string GetScenarioLabel()
    {
        return _scenario == ConnectionScenario.Local ? "Local connection" : "Network connection";
    }

    private static string GetLocalIPv4()
    {
        try
        {
            IPHostEntry hostEntry = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress address in hostEntry.AddressList)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                {
                    return address.ToString();
                }
            }
        }
        catch
        {
        }

        return "127.0.0.1";
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[CoopSceneFlow] Scene loaded. name={scene.name}, mode={mode}, screen={_screen}");

        if (IsGameplayScene(scene.name))
        {
            SetupWorld();

            if (_latestSnapshots != null && _latestSnapshots.Length > 0)
            {
                ApplySnapshot(_latestSnapshots);
            }

            return;
        }

        ClearAvatars();
        _sceneAvatars.Clear();
        _camera = Camera.main;
        HideWaitingRoomMenu();

        if (string.Equals(scene.name, LobbySceneName, System.StringComparison.Ordinal))
        {
            TryRunPendingCustomMenuLaunch();
        }
    }

    private void TransitionToGameplayScene()
    {
        Debug.Log($"[CoopSceneFlow] Transitioning to gameplay scene. roomCode={_roomCode}, playerId={_localPlayerId}");
        _screen = MenuScreen.InGame;
        _isPauseMenuOpen = false;
        _isSettingsMenuOpen = false;
        SceneManager.LoadScene(GameplaySceneName);
    }

    private void TransitionToLobbyScene()
    {
        if (SceneManager.GetActiveScene().name == LobbySceneName)
        {
            return;
        }

        Debug.Log("[CoopSceneFlow] Returning to lobby scene.");
        SceneManager.LoadScene(LobbySceneName);
    }

    private static bool IsGameplayScene(string sceneName)
    {
        return string.Equals(sceneName, GameplaySceneName, System.StringComparison.Ordinal) ||
               string.Equals(sceneName, "SampleScene", System.StringComparison.Ordinal);
    }

    private void LogUiStateIfChanged()
    {
        if (_lastLoggedScreen == _screen &&
            string.Equals(_lastLoggedRoomState, _roomState, System.StringComparison.Ordinal) &&
            _lastLoggedIsHost == _isHost &&
            _lastLoggedCanStartGame == _canStartGame)
        {
            return;
        }

        _lastLoggedScreen = _screen;
        _lastLoggedRoomState = _roomState;
        _lastLoggedIsHost = _isHost;
        _lastLoggedCanStartGame = _canStartGame;

        Debug.Log($"[CoopLobby] UI state changed. screen={_screen}, roomState={_roomState}, isHost={_isHost}, canStart={_canStartGame}, roomCode={_roomCode}");
    }

    private void UpdatePingMeasurement()
    {
        if (_relayClient == null || _relayClient.ConnectionState != PlayerConnectionState.Connected)
        {
            ResetPingState();
            return;
        }

        if (!CanMeasurePing())
        {
            return;
        }

        const float pingIntervalSeconds = 1f;
        if (Time.unscaledTime - _lastPingSentTime < pingIntervalSeconds)
        {
            return;
        }

        long pingTicks = System.DateTime.UtcNow.Ticks;
        _relayClient.SendPing(pingTicks);
        _lastPingSentTime = Time.unscaledTime;
    }

    private void CompletePingMeasurement(long pingTicks)
    {
        if (pingTicks <= 0L)
        {
            return;
        }

        float measuredMs = (float)System.TimeSpan.FromTicks(System.DateTime.UtcNow.Ticks - pingTicks).TotalMilliseconds;
        if (measuredMs < 0f)
        {
            return;
        }

        _currentPingMs = _isPingAvailable ? Mathf.Lerp(_currentPingMs, measuredMs, 0.35f) : measuredMs;
        _isPingAvailable = true;
        Debug.Log($"[CoopPing] Updated ping: {Mathf.RoundToInt(_currentPingMs)} ms");
    }

    private bool CanMeasurePing()
    {
        return _latestSnapshots != null && _latestSnapshots.Length >= 2;
    }

    private void ResetPingState()
    {
        _lastPingSentTime = -10f;
        _currentPingMs = -1f;
        _isPingAvailable = false;
    }

    private string GetPingDisplayText()
    {
        if (_relayClient == null || _relayClient.ConnectionState != PlayerConnectionState.Connected)
        {
            return "--";
        }

        if (!CanMeasurePing())
        {
            return "--";
        }

        if (_isPingAvailable)
        {
            return Mathf.RoundToInt(_currentPingMs) + " ms";
        }

        return "measuring...";
    }
}
