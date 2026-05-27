using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

public sealed partial class CoopPrototypeController : MonoBehaviour
{
    // Базовые параметры перемещения и имена рабочих сцен.
    private const float MoveSpeed = 6f;
    private const string LobbySceneName = "LobbyScene";
    private const string GameplaySceneName = "GameplayScene";

    // Внутреннее состояние экранов, которыми управляет контроллер.
    private enum MenuScreen
    {
        MainMenu,
        CreateRoom,
        JoinRoom,
        WaitingRoom,
        InGame
    }

    // Режим подключения: локальный relay или внешний сервер.
    private enum ConnectionScenario
    {
        Local,
        Network
    }

    // Состояние подключения локального клиента к relay.
    internal enum PlayerConnectionState
    {
        Disconnected,
        Connecting,
        Connected
    }

    // Главный singleton, который переживает смену сцен.
    private static CoopPrototypeController _instance;

    // Runtime-обертки игроков и ссылки на заранее размещенные сценовые аватары.
    private readonly Dictionary<int, CoopAvatarView> _avatars = new Dictionary<int, CoopAvatarView>();
    private readonly Dictionary<int, CoopScenePlayerAvatar> _sceneAvatars = new Dictionary<int, CoopScenePlayerAvatar>();
    private readonly Dictionary<int, Vector3> _sceneSpawnPositions = new Dictionary<int, Vector3>();

    private CoopEmbeddedRelayServer _localRelayServer;
    private CoopRelayClient _relayClient;
    private CoopPlayerSnapshot[] _latestSnapshots = System.Array.Empty<CoopPlayerSnapshot>();
    private MenuScreen _screen = MenuScreen.MainMenu;
    private ConnectionScenario _scenario = ConnectionScenario.Local;
    private Camera _camera;
    private bool _useSceneCameraRig;
    private CinemachineCamera _sceneCinemachineCamera;
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
    private bool _isLocalAvatarInitialized;
    private float _lastMoveSentTime;
    private float _lastPingSentTime = -10f;
    private float _currentPingMs = -1f;
    private bool _isPingAvailable;
    private PlayerMovement _localPlayerMovement;
    private CharacterController _localCharacterController;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        // Создаем управляющий объект заранее, чтобы не зависеть от сценовых экземпляров.
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
        Debug.Log($"[TRACE Lifecycle] Awake called on {name}. existingInstance={(_instance != null ? _instance.name : "null")}");

        // Если контроллер уже существует, новый компонент удаляется как дубликат.
        if (_instance != null && _instance != this)
        {
            Debug.Log($"[TRACE Lifecycle] Duplicate CoopPrototypeController on {name}. Destroying only this component.");
            Destroy(this);
            return;
        }

        // Главный контроллер сохраняется между сценами и настраивает базовую среду игры.
        _instance = this;
        DontDestroyOnLoad(gameObject);
        Application.runInBackground = true;
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        CoopUserSettings.ApplyAll();
        ApplyRelaySettings();
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void Update()
    {
        // Сначала обновляем сеть и служебное состояние UI.
        CoopUserSettings.SyncScreenModeWithRuntimeWindow();
        PumpRelayMessages();
        UpdatePingMeasurement();
        LogUiStateIfChanged();

        // Внутриигровой ввод и камера работают только в геймплейном состоянии.
        if (_screen == MenuScreen.InGame)
        {
            HandlePauseInput();
        }

        if (_screen == MenuScreen.InGame)
        {
            HandleMovement();
            UpdateCamera();
        }

        // Удаленные игроки интерполируются, локальный движется своим контроллером.
        foreach (KeyValuePair<int, CoopAvatarView> pair in _avatars)
        {
            CoopAvatarView avatar = pair.Value;
            if (avatar.SceneAvatar != null)
            {
                if (pair.Key != _localPlayerId || _screen != MenuScreen.InGame)
                {
                    avatar.SceneAvatar.Position = Vector3.Lerp(avatar.SceneAvatar.Position, avatar.TargetPosition, Time.deltaTime * 12f);
                }
                else
                {
                    avatar.TargetPosition = avatar.SceneAvatar.Position;
                }

                avatar.SceneAvatar.FaceCamera(_camera);
            }
        }
    }

    private void PumpRelayMessages()
    {
        // Все входящие сообщения relay синхронизируют локальное состояние комнаты.
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
                AudioController.Play(AudioEvent.GameStarted);
                Debug.Log($"[CoopLobby] GameStarted received. roomCode={_roomCode}, playerId={_localPlayerId}");
                TransitionToGameplayScene();
                continue;
            }

            if (message.Type == "Pong")
            {
                CompletePingMeasurement(message.PingTicks);
                continue;
            }

            if (message.Type == "MinesweeperCommand")
            {
                UpdateRoomPresence(message);
                ApplySnapshot(message.Players);
                ApplyMinesweeperCommand(message);
                continue;
            }

            if (message.Type == "RoomClosed")
            {
                _status = string.IsNullOrWhiteSpace(message.Reason) ? "Room closed." : message.Reason;
                AudioController.Play(AudioEvent.RoomClosed);
                ShutdownSession();
                ResetToMenu();
                return;
            }

            if (message.Type == "Error")
            {
                _status = string.IsNullOrWhiteSpace(message.Reason) ? "Relay server returned an error." : message.Reason;
                AudioController.Play(AudioEvent.UiError);
                Debug.LogWarning("[CoopLobby] Relay error: " + _status);
            }
        }

        if (_relayClient.ConnectionState == PlayerConnectionState.Disconnected &&
            (_screen == MenuScreen.InGame || _screen == MenuScreen.WaitingRoom))
        {
            _status = string.IsNullOrWhiteSpace(_relayClient.DisconnectStatus) ? "Connection closed." : _relayClient.DisconnectStatus;
            AudioController.Play(AudioEvent.RoomClosed);
            ShutdownSession();
            ResetToMenu();
        }
    }

    private void ResetToMenu()
    {
        // Полный сброс runtime-состояния перед возвратом в лобби.
        ClearAvatars();
        _screen = MenuScreen.MainMenu;
        _localPlayerId = 0;
        _connectedRoomName = string.Empty;
        _roomState = "waiting_for_player";
        _isHost = false;
        _canStartGame = false;
        _isPauseMenuOpen = false;
        _isSettingsMenuOpen = false;
        _isLocalAvatarInitialized = false;
        _useSceneCameraRig = false;
        _sceneCinemachineCamera = null;
        _localPlayerMovement = null;
        _localCharacterController = null;
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
        Debug.Log($"[TRACE Lifecycle] CoopPrototypeController.OnDestroy called. instanceMatch={_instance == this}");

        // Отписываемся только у активного singleton.
        if (_instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            _instance = null;
        }
    }

    private void HandleActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        Debug.Log($"[TRACE ActiveSceneChanged] {oldScene.name} -> {newScene.name}");
    }

    private static string SanitizeRoomCode(string value)
    {
        // Код комнаты нормализуется и ограничивается безопасной длиной.
        string trimmed = string.IsNullOrWhiteSpace(value) ? GenerateRoomCode() : value.Trim().ToUpperInvariant();
        return trimmed.Length > 12 ? trimmed.Substring(0, 12) : trimmed;
    }

    private static string GenerateRoomCode()
    {
        // Генерация короткого кода без неоднозначных символов.
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
        // Подтягиваем relay-настройки и локальный IP для быстрого старта локальной комнаты.
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
        string previousRoomState = _roomState;
        // Сетевое сообщение комнаты обновляет локальный снимок состояния UI и роли игрока.
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
        if (!string.Equals(previousRoomState, _roomState, System.StringComparison.Ordinal) &&
            string.Equals(_roomState, "player_joined", System.StringComparison.Ordinal))
        {
            AudioController.Play(AudioEvent.PlayerJoined);
        }

        Debug.Log($"[CoopLobby] Room presence updated. screen={_screen}, state={_roomState}, roomCode={_roomCode}, isHost={_isHost}, canStart={_canStartGame}, playerId={_localPlayerId}");
    }

    private string BuildRoomStatus()
    {
        // Единая точка сборки читаемого статуса для меню и overlay.
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
        // Пытаемся взять первый доступный IPv4 для локального сценария.
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
        Debug.Log(
            $"[TRACE SceneLoaded] scene={scene.name}, mode={mode}, screen={_screen}, roomState={_roomState}, " +
            $"localPlayerId={_localPlayerId}, snapshotsCount={(_latestSnapshots == null ? -1 : _latestSnapshots.Length)}, " +
            $"cameraMain={(Camera.main != null ? Camera.main.name : "null")}");

        // После загрузки геймплея сначала готовим мир, затем накатываем последние снапшоты.
        if (IsGameplayScene(scene.name))
        {
            _screen = MenuScreen.InGame;
            Debug.Log("[TRACE SceneLoaded] Gameplay scene detected. Starting SetupWorld().");
            SetupWorld();

            if (_latestSnapshots != null && _latestSnapshots.Length > 0)
            {
                Debug.Log($"[TRACE SceneLoaded] Applying cached snapshots after SetupWorld. Count={_latestSnapshots.Length}");
                ApplySnapshot(_latestSnapshots);
            }
            else
            {
                Debug.LogWarning("[TRACE SceneLoaded] No cached snapshots available after SetupWorld.");
            }

            return;
        }

        // Для остальных сцен очищаем только runtime-ссылки, не затрагивая сохраненные объекты.
        ClearAvatars();
        _sceneAvatars.Clear();
        _camera = Camera.main;
        _useSceneCameraRig = false;
        _sceneCinemachineCamera = null;
        _isLocalAvatarInitialized = false;
        _localPlayerMovement = null;
        _localCharacterController = null;
        HideWaitingRoomMenu();
    }

    private void TransitionToGameplayScene()
    {
        // Переход запускается только после сетевого сигнала о старте матча.
        Debug.Log(
            $"[TRACE TransitionToGameplay] roomCode={_roomCode}, playerId={_localPlayerId}, " +
            $"screenBefore={_screen}, roomState={_roomState}, " +
            $"snapshotsCount={(_latestSnapshots == null ? -1 : _latestSnapshots.Length)}");
        _isPauseMenuOpen = false;
        _isSettingsMenuOpen = false;
        Debug.Log("[TRACE TransitionToGameplay] About to call SceneManager.LoadScene(GameplayScene).");
        SceneManager.LoadScene(GameplaySceneName);
        Debug.Log("[TRACE TransitionToGameplay] SceneManager.LoadScene(GameplayScene) returned.");
    }

    private void TransitionToLobbyScene()
    {
        // Возврат в лобби нужен после disconnect и завершения сессии.
        if (SceneManager.GetActiveScene().name == LobbySceneName)
        {
            return;
        }

        Debug.Log("[CoopSceneFlow] Returning to lobby scene.");
        SceneManager.LoadScene(LobbySceneName);
    }

    private static bool IsGameplayScene(string sceneName)
    {
        // Поддерживаем текущее имя сцены и старое тестовое имя на переходный период.
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
        // Пинг измеряется только при активном соединении и наличии второго игрока.
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
        // Сглаживаем значение, чтобы UI не дергался от скачков задержки.
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
        // Пинг имеет смысл только когда в комнате есть хотя бы два игрока.
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
