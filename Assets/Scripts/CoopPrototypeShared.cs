using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

[Serializable]
internal sealed class CoopNetworkMessage
{
    // Универсальный сетевой пакет для relay-клиента и embedded relay-сервера.
    public string Type = string.Empty;
    public string RoomCode = string.Empty;
    public string RoomName = string.Empty;
    public string PlayerName = string.Empty;
    public string Password = string.Empty;
    public string Reason = string.Empty;
    public int PlayerId = 0;
    public float X = 0f;
    public float Y = 0f;
    public float Z = 0f;
    public bool IsPrivate = false;
    public string RoomState = string.Empty;
    public bool IsHost = false;
    public bool CanStartGame = false;
    public long PingTicks = 0L;
    public int MineCount = 40;
    public string MinesweeperAction = string.Empty;
    public int CellX = -1;
    public int CellY = -1;
    public int BoardSeed = 0;
    public CoopPlayerSnapshot[] Players = Array.Empty<CoopPlayerSnapshot>();
}

[Serializable]
internal sealed class CoopPlayerSnapshot
{
    // Сжатое описание игрока, которое регулярно рассылается всем участникам комнаты.
    public int PlayerId = 0;
    public string PlayerName = string.Empty;
    public float X = 0f;
    public float Y = 0f;
    public float Z = 0f;
}

internal sealed class CoopAvatarView
{
    // Runtime-связка сценового аватара и его целевой сетевой позиции.
    public CoopScenePlayerAvatar SceneAvatar;
    public Vector3 TargetPosition;
}

[Serializable]
internal sealed class CoopRelaySettingsData
{
    // Настройки relay по умолчанию, которые можно переопределить через Resources.
    public string relayHost = "127.0.0.1";
    public int relayPort = 7777;
}

internal static class CoopRelaySettings
{
    private static CoopRelaySettingsData _cached;

    public static CoopRelaySettingsData Load()
    {
        // Конфиг загружается лениво и кешируется на время работы приложения.
        if (_cached != null)
        {
            return _cached;
        }

        TextAsset asset = Resources.Load<TextAsset>("CoopRelayConfig");
        if (asset == null || string.IsNullOrWhiteSpace(asset.text))
        {
            _cached = new CoopRelaySettingsData();
            return _cached;
        }

        CoopRelaySettingsData data = JsonUtility.FromJson<CoopRelaySettingsData>(asset.text);
        _cached = data ?? new CoopRelaySettingsData();

        if (string.IsNullOrWhiteSpace(_cached.relayHost))
        {
            _cached.relayHost = "127.0.0.1";
        }

        if (_cached.relayPort <= 0 || _cached.relayPort > 65535)
        {
            _cached.relayPort = 7777;
        }

        return _cached;
    }
}

internal static class CoopNetworkSocketUtility
{
    public static void ConfigureTcpClient(TcpClient client)
    {
        if (client == null)
        {
            return;
        }

        client.NoDelay = true;

        try
        {
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        }
        catch
        {
        }
    }
}

internal sealed class CoopRelayClient
{
    // Клиент подключается к relay, читает сообщения в фоне и складывает их в очередь для Unity-потока.
    private readonly ConcurrentQueue<CoopNetworkMessage> _incoming = new ConcurrentQueue<CoopNetworkMessage>();
    private readonly object _writeLock = new object();

    private TcpClient _client;
    private StreamWriter _writer;
    private Thread _readThread;

    public string Status { get; private set; }
    public string DisconnectStatus { get; private set; }
    public CoopPrototypeController.PlayerConnectionState ConnectionState { get; private set; }
    public int LocalPlayerId { get; private set; }
    public string ConnectedRoomCode { get; private set; }

    public bool CreateRoom(string host, int port, string playerName, string roomName, bool isPrivate, string password, int mineCount)
    {
        // Хост запрашивает создание новой комнаты.
        return Connect(host, port, new CoopNetworkMessage
        {
            Type = "CreateRoomRequest",
            PlayerName = playerName,
            RoomName = roomName,
            IsPrivate = isPrivate,
            Password = password ?? string.Empty,
            MineCount = mineCount
        });
    }

    public bool JoinRoom(string host, int port, string roomCode, string playerName, string password)
    {
        // Клиент подключается к уже существующей комнате по коду.
        return Connect(host, port, new CoopNetworkMessage
        {
            Type = "JoinRoomRequest",
            RoomCode = roomCode,
            PlayerName = playerName,
            Password = password ?? string.Empty
        });
    }

    public void Disconnect()
    {
        // Локально сбрасываем состояние и закрываем сокет, если он еще открыт.
        ConnectionState = CoopPrototypeController.PlayerConnectionState.Disconnected;
        LocalPlayerId = 0;
        ConnectedRoomCode = null;

        try
        {
            _client?.Close();
        }
        catch
        {
        }

        _client = null;
        _writer = null;
    }

    public void SendMove(Vector3 position)
    {
        // Передаем серверу фактическую мировую позицию локального игрока, включая ось Y.
        if (ConnectionState != CoopPrototypeController.PlayerConnectionState.Connected)
        {
            return;
        }

        try
        {
            Send(new CoopNetworkMessage
            {
                Type = "Move",
                X = position.x,
                Y = position.y,
                Z = position.z
            });
        }
        catch (Exception exception)
        {
            Status = "Connection lost: " + exception.Message;
            Disconnect();
        }
    }

    public void SendStartGameRequest()
    {
        // Отдельная команда старта матча, доступная только хосту.
        if (ConnectionState != CoopPrototypeController.PlayerConnectionState.Connected)
        {
            return;
        }

        try
        {
            Send(new CoopNetworkMessage
            {
                Type = "StartGameRequest"
            });
        }
        catch (Exception exception)
        {
            Status = "Connection lost: " + exception.Message;
            Disconnect();
        }
    }

    public void SendPing(long pingTicks)
    {
        // Пинг измеряется простым echo-механизмом через relay.
        if (ConnectionState != CoopPrototypeController.PlayerConnectionState.Connected)
        {
            return;
        }

        try
        {
            Send(new CoopNetworkMessage
            {
                Type = "Ping",
                PingTicks = pingTicks
            });
        }
        catch (Exception exception)
        {
            Status = "Connection lost: " + exception.Message;
            Disconnect();
        }
    }

    public void SendMinesweeperCommand(string action, int cellX, int cellY, int boardSeed)
    {
        if (ConnectionState != CoopPrototypeController.PlayerConnectionState.Connected)
        {
            return;
        }

        try
        {
            Send(new CoopNetworkMessage
            {
                Type = "MinesweeperCommand",
                MinesweeperAction = action ?? string.Empty,
                CellX = cellX,
                CellY = cellY,
                BoardSeed = boardSeed
            });
        }
        catch (Exception exception)
        {
            Status = "Connection lost: " + exception.Message;
            Disconnect();
        }
    }

    public bool TryDequeue(out CoopNetworkMessage message)
    {
        return _incoming.TryDequeue(out message);
    }

    private bool Connect(string host, int port, CoopNetworkMessage request)
    {
        // Подключаемся, выполняем стартовый handshake и после этого запускаем фоновый read loop.
        Disconnect();

        try
        {
            ConnectionState = CoopPrototypeController.PlayerConnectionState.Connecting;
            Status = "Connecting to relay server...";
            DisconnectStatus = null;

            _client = new TcpClient();
            CoopNetworkSocketUtility.ConfigureTcpClient(_client);
            _client.Connect(host, port);
            CoopNetworkSocketUtility.ConfigureTcpClient(_client);

            NetworkStream stream = _client.GetStream();
            StreamReader reader = new StreamReader(stream, Encoding.UTF8);
            _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            Send(request);

            string responseLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(responseLine))
            {
                Status = "Relay server did not answer.";
                Disconnect();
                return false;
            }

            CoopNetworkMessage response = JsonUtility.FromJson<CoopNetworkMessage>(responseLine);
            if (response == null)
            {
                Status = "Relay server returned an invalid response.";
                Disconnect();
                return false;
            }

            if (response.Type == "Error")
            {
                Status = string.IsNullOrWhiteSpace(response.Reason) ? "Relay server rejected the request." : response.Reason;
                Disconnect();
                return false;
            }

            if (response.Type != "RoomConnected")
            {
                Status = "Relay server returned an unexpected response.";
                Disconnect();
                return false;
            }

            LocalPlayerId = response.PlayerId;
            ConnectedRoomCode = response.RoomCode;
            ConnectionState = CoopPrototypeController.PlayerConnectionState.Connected;
            Status = "Connected.";
            _incoming.Enqueue(response);

            _readThread = new Thread(() => ReadLoop(reader)) { IsBackground = true };
            _readThread.Start();
            return true;
        }
        catch (Exception exception)
        {
            Status = "Connection failed: " + exception.Message;
            Disconnect();
            return false;
        }
    }

    private void ReadLoop(StreamReader reader)
    {
        // Сетевой поток только читает данные и не касается Unity API напрямую.
        try
        {
            while (_client != null && _client.Connected)
            {
                string line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                {
                    break;
                }

                CoopNetworkMessage message = JsonUtility.FromJson<CoopNetworkMessage>(line);
                if (message != null)
                {
                    _incoming.Enqueue(message);
                }
            }
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            DisconnectStatus = "Disconnected from relay server.";
            Disconnect();
        }
    }

    private void Send(CoopNetworkMessage message)
    {
        // Запись в сокет защищена lock, чтобы фоновые и main-thread отправки не конфликтовали.
        if (_writer == null)
        {
            return;
        }

        lock (_writeLock)
        {
            _writer.WriteLine(JsonUtility.ToJson(message));
        }
    }
}

internal sealed class CoopEmbeddedRelayServer
{
    // Локальный relay-сервер нужен для сценария без внешнего backend.
    private const string RoomStateWaitingForPlayer = "waiting_for_player";
    private const string RoomStatePlayerJoined = "player_joined";
    private const string RoomStateInGame = "in_game";

    private static readonly object RoomCodeLock = new object();
    private static readonly System.Random RoomCodeRandom = new System.Random();

    private readonly ConcurrentDictionary<string, CoopEmbeddedRelayRoom> _rooms =
        new ConcurrentDictionary<string, CoopEmbeddedRelayRoom>(StringComparer.OrdinalIgnoreCase);

    private TcpListener _listener;
    private Thread _acceptThread;
    private Thread _broadcastThread;
    private volatile bool _running;

    public string Status { get; private set; } = "Stopped";

    public void Start(int port)
    {
        // Поднимаем listener и два служебных потока: прием клиентов и рассылку снапшотов.
        Stop();

        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        _running = true;
        Status = "Local relay started.";

        _acceptThread = new Thread(AcceptLoop) { IsBackground = true };
        _broadcastThread = new Thread(BroadcastLoop) { IsBackground = true };
        _acceptThread.Start();
        _broadcastThread.Start();
    }

    public void Stop()
    {
        // Полная остановка локального relay и очистка всех комнат.
        _running = false;

        try
        {
            _listener?.Stop();
        }
        catch
        {
        }

        foreach (CoopEmbeddedRelayRoom room in _rooms.Values)
        {
            room.Dispose();
        }

        _rooms.Clear();
        Status = "Stopped";
    }

    private void AcceptLoop()
    {
        // Каждое входящее подключение обслуживается в отдельном фоне.
        while (_running)
        {
            try
            {
                TcpClient client = _listener.AcceptTcpClient();
                CoopNetworkSocketUtility.ConfigureTcpClient(client);
                Thread thread = new Thread(() => HandleClient(client)) { IsBackground = true };
                thread.Start();
            }
            catch (SocketException)
            {
                if (_running)
                {
                    Status = "Local relay socket error.";
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }
    }

    private void BroadcastLoop()
    {
        // Периодическая отправка снапшотов всем комнатам.
        while (_running)
        {
            foreach (CoopEmbeddedRelayRoom room in _rooms.Values)
            {
                room.BroadcastSnapshot();
            }

            Thread.Sleep(100);
        }
    }

    private void HandleClient(TcpClient client)
    {
        // Один клиент: регистрация в комнате, затем цикл чтения Move/Ping/StartGame.
        CoopEmbeddedRelayConnection connection = null;

        try
        {
            using (NetworkStream stream = client.GetStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
            {
                string firstLine = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(firstLine))
                {
                    return;
                }

                CoopNetworkMessage request = JsonUtility.FromJson<CoopNetworkMessage>(firstLine);
                if (request == null)
                {
                    Send(writer, new CoopNetworkMessage
                    {
                        Type = "Error",
                        Reason = "Invalid request payload."
                    });
                    return;
                }

                CoopEmbeddedRelayRoom room;
                CoopEmbeddedRelayPlayer player;
                string error;

                if (!TryRegisterConnection(request, out room, out player, out error))
                {
                    Send(writer, new CoopNetworkMessage
                    {
                        Type = "Error",
                        Reason = error
                    });
                    return;
                }

                connection = new CoopEmbeddedRelayConnection(client, writer, room.RoomCode, player.PlayerId);
                room.AttachConnection(connection);

                Send(writer, new CoopNetworkMessage
                {
                    Type = "RoomConnected",
                    RoomCode = room.RoomCode,
                    RoomName = room.RoomName,
                    PlayerId = player.PlayerId,
                    Password = string.Empty,
                    IsPrivate = room.IsPrivate,
                    RoomState = room.State,
                    IsHost = player.PlayerId == 1,
                    CanStartGame = player.PlayerId == 1 && room.State == RoomStatePlayerJoined,
                    MineCount = room.MineCount,
                    Players = room.BuildSnapshot()
                });

                room.BroadcastSnapshot();

                while (_running && client.Connected)
                {
                    string line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        break;
                    }

                    connection.MarkReceived();

                    CoopNetworkMessage message = JsonUtility.FromJson<CoopNetworkMessage>(line);
                    if (message != null && message.Type == "Move")
                    {
                        room.UpdatePosition(player.PlayerId, message.X, message.Y, message.Z);
                        continue;
                    }

                    if (message != null && message.Type == "Ping")
                    {
                        Send(writer, new CoopNetworkMessage
                        {
                            Type = "Pong",
                            PingTicks = message.PingTicks
                        });
                        continue;
                    }

                    if (message != null && message.Type == "MinesweeperCommand")
                    {
                        room.BroadcastMinesweeperCommand(
                            message.MinesweeperAction,
                            message.CellX,
                            message.CellY,
                            message.BoardSeed);
                        continue;
                    }

                    if (message != null && message.Type == "StartGameRequest")
                    {
                        string errorMessage;
                        if (!room.TryStartGame(player.PlayerId, out errorMessage))
                        {
                            Send(writer, new CoopNetworkMessage
                            {
                                Type = "Error",
                                Reason = errorMessage
                            });
                            continue;
                        }

                        room.BroadcastGameStarted();
                    }
                }
            }
        }
        catch (IOException)
        {
        }
        catch (SocketException)
        {
        }
        finally
        {
            if (connection != null && _rooms.TryGetValue(connection.RoomCode, out CoopEmbeddedRelayRoom room))
            {
                room.RemovePlayer(connection.PlayerId);
                if (room.IsEmpty)
                {
                    _rooms.TryRemove(connection.RoomCode, out _);
                    room.Dispose();
                }
                else
                {
                    room.BroadcastSnapshot();
                }
            }

            try
            {
                client.Close();
            }
            catch
            {
            }
        }
    }

    private bool TryRegisterConnection(
        CoopNetworkMessage request,
        out CoopEmbeddedRelayRoom room,
        out CoopEmbeddedRelayPlayer player,
        out string error)
    {
        // Здесь определяется, создаем новую комнату или присоединяемся к существующей.
        room = null;
        player = null;
        error = string.Empty;

        if (request.Type == "CreateRoomRequest")
        {
            string roomCode = CreateUniqueRoomCode();
            room = new CoopEmbeddedRelayRoom(
                roomCode,
                string.IsNullOrWhiteSpace(request.RoomName) ? "Local Room" : request.RoomName.Trim(),
                request.IsPrivate,
                request.IsPrivate ? request.Password ?? string.Empty : string.Empty,
                request.MineCount);
            player = room.AddPlayer(string.IsNullOrWhiteSpace(request.PlayerName) ? "Player" : request.PlayerName.Trim());
            _rooms[roomCode] = room;
            return true;
        }

        if (request.Type == "JoinRoomRequest")
        {
            string roomCode = string.IsNullOrWhiteSpace(request.RoomCode) ? string.Empty : request.RoomCode.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                error = "Room code is required.";
                return false;
            }

            if (!_rooms.TryGetValue(roomCode, out room))
            {
                error = "Room not found.";
                return false;
            }

            player = room.TryAddPlayer(
                string.IsNullOrWhiteSpace(request.PlayerName) ? "Player" : request.PlayerName.Trim(),
                request.Password ?? string.Empty,
                out error);

            return player != null;
        }

        error = "Unknown request type.";
        return false;
    }

    private static string CreateUniqueRoomCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        char[] chars = new char[6];

        lock (RoomCodeLock)
        {
            for (int index = 0; index < chars.Length; index++)
            {
                chars[index] = alphabet[RoomCodeRandom.Next(alphabet.Length)];
            }
        }

        return new string(chars);
    }

    private static void Send(StreamWriter writer, CoopNetworkMessage message)
    {
        writer.WriteLine(JsonUtility.ToJson(message));
    }
}

internal sealed class CoopEmbeddedRelayRoom : IDisposable
{
    // Комната хранит игроков, их позиции и активные подключения.
    private const string RoomStateWaitingForPlayer = "waiting_for_player";
    private const string RoomStatePlayerJoined = "player_joined";
    private const string RoomStateInGame = "in_game";

    private readonly object _sync = new object();
    private readonly Dictionary<int, CoopEmbeddedRelayPlayer> _players = new Dictionary<int, CoopEmbeddedRelayPlayer>();
    private readonly Dictionary<int, CoopEmbeddedRelayConnection> _connections = new Dictionary<int, CoopEmbeddedRelayConnection>();

    public CoopEmbeddedRelayRoom(string roomCode, string roomName, bool isPrivate, string password, int mineCount)
    {
        RoomCode = roomCode;
        RoomName = roomName;
        IsPrivate = isPrivate;
        Password = password ?? string.Empty;
        MineCount = Mathf.Clamp(mineCount, 5, 40);
        State = RoomStateWaitingForPlayer;
    }

    public string RoomCode { get; }

    public string RoomName { get; }

    public bool IsPrivate { get; }

    public int MineCount { get; }

    private string Password { get; }

    public string State { get; private set; }

    public bool IsEmpty
    {
        get
        {
            lock (_sync)
            {
                return _players.Count == 0;
            }
        }
    }

    public CoopEmbeddedRelayPlayer AddPlayer(string playerName)
    {
        // Первый игрок в комнате всегда получает роль хоста.
        lock (_sync)
        {
            CoopEmbeddedRelayPlayer player = CreatePlayer(1, playerName);
            _players[player.PlayerId] = player;
            State = DetermineState();
            return player;
        }
    }

    public CoopEmbeddedRelayPlayer TryAddPlayer(string playerName, string password, out string error)
    {
        // Второй игрок может войти только если есть место и проходит проверку пароля.
        lock (_sync)
        {
            if (_players.Count >= 2)
            {
                error = "Room is full.";
                return null;
            }

            if (IsPrivate && !string.Equals(Password, password, StringComparison.Ordinal))
            {
                error = "Wrong room password.";
                return null;
            }

            int playerId = _players.ContainsKey(1) ? 2 : 1;
            CoopEmbeddedRelayPlayer player = CreatePlayer(playerId, playerName);
            _players[player.PlayerId] = player;
            State = DetermineState();
            error = string.Empty;
            return player;
        }
    }

    public void AttachConnection(CoopEmbeddedRelayConnection connection)
    {
        lock (_sync)
        {
            _connections[connection.PlayerId] = connection;
        }
    }

    public void UpdatePosition(int playerId, float x, float y, float z)
    {
        // Сервер хранит последнюю известную позицию каждого игрока.
        lock (_sync)
        {
            if (_players.TryGetValue(playerId, out CoopEmbeddedRelayPlayer player))
            {
                player.X = x;
                player.Y = y;
                player.Z = z;
            }
        }
    }

    public void RemovePlayer(int playerId)
    {
        lock (_sync)
        {
            _connections.Remove(playerId);
            _players.Remove(playerId);
            State = DetermineState();
        }
    }

    public bool TryStartGame(int requestingPlayerId, out string error)
    {
        // Валидация старта матча выполняется на стороне комнаты/сервера.
        lock (_sync)
        {
            if (!_players.ContainsKey(requestingPlayerId))
            {
                error = "Player is not part of this room.";
                return false;
            }

            if (requestingPlayerId != 1)
            {
                error = "Only the host can start the game.";
                return false;
            }

            if (_players.Count < 2)
            {
                error = "A second player has not connected yet.";
                return false;
            }

            if (State == RoomStateInGame)
            {
                error = "The game has already started.";
                return false;
            }

            State = RoomStateInGame;
            error = string.Empty;
            return true;
        }
    }

    public CoopPlayerSnapshot[] BuildSnapshot()
    {
        // Строим упорядоченный массив снапшотов, одинаковый для всех клиентов.
        lock (_sync)
        {
            List<CoopPlayerSnapshot> snapshots = new List<CoopPlayerSnapshot>();
            foreach (CoopEmbeddedRelayPlayer player in _players.Values)
            {
                snapshots.Add(new CoopPlayerSnapshot
                {
                    PlayerId = player.PlayerId,
                    PlayerName = player.PlayerName,
                    X = player.X,
                    Y = player.Y,
                    Z = player.Z
                });
            }

            snapshots.Sort((left, right) => left.PlayerId.CompareTo(right.PlayerId));
            return snapshots.ToArray();
        }
    }

    public void BroadcastSnapshot()
    {
        // Регулярная синхронизация состояния комнаты.
        CoopEmbeddedRelayConnection[] connections;
        CoopPlayerSnapshot[] snapshot;

        lock (_sync)
        {
            List<CoopEmbeddedRelayConnection> list = new List<CoopEmbeddedRelayConnection>(_connections.Values);
            connections = list.ToArray();
            snapshot = BuildSnapshot();
        }

        BroadcastToConnections(connections, connection => BuildRoomMessage("Snapshot", snapshot, connection.PlayerId));
    }

    public void BroadcastGameStarted()
    {
        // Одноразовое событие начала матча с последним актуальным снапшотом.
        CoopEmbeddedRelayConnection[] connections;
        CoopPlayerSnapshot[] snapshot;

        lock (_sync)
        {
            List<CoopEmbeddedRelayConnection> list = new List<CoopEmbeddedRelayConnection>(_connections.Values);
            connections = list.ToArray();
            snapshot = BuildSnapshot();
        }

        BroadcastToConnections(connections, connection => BuildRoomMessage("GameStarted", snapshot, connection.PlayerId));
    }

    public void BroadcastMinesweeperCommand(string action, int cellX, int cellY, int boardSeed)
    {
        CoopEmbeddedRelayConnection[] connections;
        CoopPlayerSnapshot[] snapshot;

        lock (_sync)
        {
            if (State != RoomStateInGame)
            {
                return;
            }

            List<CoopEmbeddedRelayConnection> list = new List<CoopEmbeddedRelayConnection>(_connections.Values);
            connections = list.ToArray();
            snapshot = BuildSnapshot();
        }

        BroadcastToConnections(connections, connection =>
        {
            CoopNetworkMessage message = BuildRoomMessage("MinesweeperCommand", snapshot, connection.PlayerId);
            message.MinesweeperAction = action ?? string.Empty;
            message.CellX = cellX;
            message.CellY = cellY;
            message.BoardSeed = boardSeed;
            return message;
        });
    }

    public void Dispose()
    {
        lock (_sync)
        {
            foreach (CoopEmbeddedRelayConnection connection in _connections.Values)
            {
                connection.Dispose();
            }

            _connections.Clear();
            _players.Clear();
        }
    }

    private static CoopEmbeddedRelayPlayer CreatePlayer(int playerId, string playerName)
    {
        // Начальные позиции игроков задаются сервером, чтобы клиенты стартовали согласованно.
        return new CoopEmbeddedRelayPlayer
        {
            PlayerId = playerId,
            PlayerName = playerName,
            X = playerId == 1 ? -2f : 2f,
            Y = 0.9f,
            Z = 0f
        };
    }

    private CoopNetworkMessage BuildRoomMessage(string type, CoopPlayerSnapshot[] snapshot, int recipientPlayerId)
    {
        // Каждому клиенту отправляется сообщение с его ролью и общим состоянием комнаты.
        return new CoopNetworkMessage
        {
            Type = type,
            RoomCode = RoomCode,
            RoomName = RoomName,
            PlayerId = recipientPlayerId,
            IsPrivate = IsPrivate,
            RoomState = State,
            IsHost = recipientPlayerId == 1,
            CanStartGame = recipientPlayerId == 1 && State == RoomStatePlayerJoined,
            MineCount = MineCount,
            Players = snapshot
        };
    }

    private void BroadcastToConnections(
        CoopEmbeddedRelayConnection[] connections,
        Func<CoopEmbeddedRelayConnection, CoopNetworkMessage> buildMessage)
    {
        List<int> toRemove = new List<int>();
        foreach (CoopEmbeddedRelayConnection connection in connections)
        {
            if (connection.ShouldDisconnect())
            {
                toRemove.Add(connection.PlayerId);
                continue;
            }

            if (!connection.TrySend(buildMessage(connection)) && connection.ShouldDisconnect())
            {
                toRemove.Add(connection.PlayerId);
            }
        }

        if (toRemove.Count == 0)
        {
            return;
        }

        lock (_sync)
        {
            foreach (int playerId in toRemove)
            {
                if (_connections.TryGetValue(playerId, out CoopEmbeddedRelayConnection connection))
                {
                    connection.Dispose();
                }

                _connections.Remove(playerId);
                _players.Remove(playerId);
            }

            State = DetermineState();
        }
    }

    private string DetermineState()
    {
        if (State == RoomStateInGame)
        {
            return RoomStateInGame;
        }

        if (_players.Count >= 2)
        {
            return RoomStatePlayerJoined;
        }

        return RoomStateWaitingForPlayer;
    }
}

internal sealed class CoopEmbeddedRelayConnection : IDisposable
{
    // Обертка над сокетом конкретного игрока внутри локальной комнаты.
    private const int MaxConsecutiveSendFailures = 3;
    private const double ReceiveTimeoutSeconds = 20.0;

    private readonly TcpClient _client;
    private readonly StreamWriter _writer;
    private readonly object _writeLock = new object();
    private DateTime _lastReceivedUtc;
    private int _consecutiveSendFailures;

    public CoopEmbeddedRelayConnection(TcpClient client, StreamWriter writer, string roomCode, int playerId)
    {
        _client = client;
        _writer = writer;
        RoomCode = roomCode;
        PlayerId = playerId;
        _lastReceivedUtc = DateTime.UtcNow;
    }

    public string RoomCode { get; }

    public int PlayerId { get; }

    public void MarkReceived()
    {
        _lastReceivedUtc = DateTime.UtcNow;
        _consecutiveSendFailures = 0;
    }

    public bool TrySend(CoopNetworkMessage message)
    {
        try
        {
            lock (_writeLock)
            {
                _writer.WriteLine(JsonUtility.ToJson(message));
            }

            _lastReceivedUtc = DateTime.UtcNow;
            _consecutiveSendFailures = 0;
            return true;
        }
        catch
        {
            _consecutiveSendFailures++;
            return false;
        }
    }

    public bool ShouldDisconnect()
    {
        if (_consecutiveSendFailures >= MaxConsecutiveSendFailures)
        {
            return true;
        }

        return (DateTime.UtcNow - _lastReceivedUtc).TotalSeconds > ReceiveTimeoutSeconds;
    }

    public void Dispose()
    {
        try
        {
            _client.Close();
        }
        catch
        {
        }
    }
}

internal sealed class CoopEmbeddedRelayPlayer
{
    // Серверная модель игрока внутри комнаты.
    public int PlayerId;
    public string PlayerName;
    public float X;
    public float Y;
    public float Z;
}
