using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public sealed partial class CoopPrototypeController : MonoBehaviour
{
    private const float MoveSpeed = 6f;

    private enum MenuScreen
    {
        MainMenu,
        CreateRoom,
        JoinRoom,
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
    private int _localPlayerId;
    private bool _isPrivateRoom;
    private bool _isPauseMenuOpen;
    private bool _isSettingsMenuOpen;
    private float _lastMoveSentTime;

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
        SetupWorld();
    }

    private void Update()
    {
        PumpRelayMessages();

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
            if (avatar.SceneAvatar != null)
            {
                avatar.SceneAvatar.Position = Vector3.Lerp(avatar.SceneAvatar.Position, avatar.TargetPosition, Time.deltaTime * 12f);
                avatar.SceneAvatar.FaceCamera(_camera);
            }
        }
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
                _roomCode = SanitizeRoomCode(message.RoomCode);
                _connectedRoomName = SanitizeRoomName(message.RoomName);
                _isPrivateRoom = message.IsPrivate;
                _localPlayerId = message.PlayerId;
                _status = "Room connected.";
                ApplySnapshot(message.Players);
                continue;
            }

            if (message.Type == "Snapshot")
            {
                ApplySnapshot(message.Players);
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
            }
        }

        if (_relayClient.ConnectionState == PlayerConnectionState.Disconnected && _screen == MenuScreen.InGame)
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
        _isPauseMenuOpen = false;
        _isSettingsMenuOpen = false;
    }

    private void OnApplicationQuit()
    {
        ShutdownSession();
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
}
