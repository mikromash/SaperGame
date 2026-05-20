using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

int port = ParsePort(args, 7777);
using RelayServer server = new RelayServer();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    server.Stop();
};

server.Start(port);
Console.WriteLine($"Relay server started on 0.0.0.0:{port}");
Console.WriteLine("Press Ctrl+C to stop.");

while (server.IsRunning)
{
    Thread.Sleep(250);
}

static int ParsePort(string[] args, int defaultPort)
{
    for (int index = 0; index < args.Length; index++)
    {
        if (string.Equals(args[index], "--port", StringComparison.OrdinalIgnoreCase) &&
            index + 1 < args.Length &&
            int.TryParse(args[index + 1], out int parsedPort) &&
            parsedPort > 0 &&
            parsedPort <= 65535)
        {
            return parsedPort;
        }
    }

    return defaultPort;
}

internal sealed class RelayServer : IDisposable
{
    private const string RoomStateWaitingForPlayer = "waiting_for_player";
    private const string RoomStatePlayerJoined = "player_joined";
    private const string RoomStateInGame = "in_game";

    private static readonly object RoomCodeLock = new object();
    private static readonly Random RoomCodeRandom = new Random();

    private readonly ConcurrentDictionary<string, RelayRoom> _rooms =
        new ConcurrentDictionary<string, RelayRoom>(StringComparer.OrdinalIgnoreCase);

    private TcpListener? _listener;
    private Thread? _acceptThread;
    private Thread? _broadcastThread;
    private volatile bool _running;

    public bool IsRunning => _running;

    public void Start(int port)
    {
        Stop();

        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        _running = true;

        _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "Relay Accept" };
        _broadcastThread = new Thread(BroadcastLoop) { IsBackground = true, Name = "Relay Broadcast" };
        _acceptThread.Start();
        _broadcastThread.Start();
    }

    public void Stop()
    {
        _running = false;

        try
        {
            _listener?.Stop();
        }
        catch
        {
        }

        foreach (RelayRoom room in _rooms.Values)
        {
            room.Dispose();
        }

        _rooms.Clear();
    }

    public void Dispose()
    {
        Stop();
    }

    private void AcceptLoop()
    {
        while (_running)
        {
            try
            {
                TcpClient client = _listener!.AcceptTcpClient();
                client.NoDelay = true;
                Thread thread = new Thread(() => HandleClient(client)) { IsBackground = true, Name = "Relay Client" };
                thread.Start();
            }
            catch (SocketException exception)
            {
                if (_running)
                {
                    Console.WriteLine("Accept socket error: " + exception.Message);
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
        while (_running)
        {
            foreach (RelayRoom room in _rooms.Values)
            {
                room.BroadcastSnapshot();
            }

            Thread.Sleep(100);
        }
    }

    private void HandleClient(TcpClient client)
    {
        RelayConnection? connection = null;

        try
        {
            using NetworkStream stream = client.GetStream();
            using StreamReader reader = new StreamReader(stream, Encoding.UTF8);
            using StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            string? firstLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(firstLine))
            {
                return;
            }

            CoopNetworkMessage? request = JsonRelay.FromJson(firstLine);
            if (request == null)
            {
                Send(writer, new CoopNetworkMessage
                {
                    Type = "Error",
                    Reason = "Invalid request payload."
                });
                return;
            }

            if (!TryRegisterConnection(request, out RelayRoom? room, out RelayPlayer? player, out string error))
            {
                Send(writer, new CoopNetworkMessage
                {
                    Type = "Error",
                    Reason = error
                });
                return;
            }

            if (room == null || player == null)
            {
                Send(writer, new CoopNetworkMessage
                {
                    Type = "Error",
                    Reason = "Could not join relay room."
                });
                return;
            }

            connection = new RelayConnection(client, writer, room.RoomCode, player.PlayerId);
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
                Players = room.BuildSnapshot()
            });

            room.BroadcastSnapshot();

            while (_running && client.Connected)
            {
                string? line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                {
                    break;
                }

                CoopNetworkMessage? message = JsonRelay.FromJson(line);
                if (message == null)
                {
                    continue;
                }

                if (message.Type == "Move")
                {
                    room.UpdatePosition(player.PlayerId, message.X, message.Y, message.Z);
                    continue;
                }

                if (message.Type == "Ping")
                {
                    Send(writer, new CoopNetworkMessage
                    {
                        Type = "Pong",
                        PingTicks = message.PingTicks
                    });
                    continue;
                }

                if (message.Type == "MinesweeperCommand")
                {
                    room.BroadcastMinesweeperCommand(
                        message.MinesweeperAction,
                        message.CellX,
                        message.CellY,
                        message.BoardSeed);
                    continue;
                }

                if (message.Type == "StartGameRequest")
                {
                    if (!room.TryStartGame(player.PlayerId, out string startError))
                    {
                        Send(writer, new CoopNetworkMessage
                        {
                            Type = "Error",
                            Reason = startError
                        });
                        continue;
                    }

                    room.BroadcastGameStarted();
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
            if (connection != null && _rooms.TryGetValue(connection.RoomCode, out RelayRoom? room))
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
        out RelayRoom? room,
        out RelayPlayer? player,
        out string error)
    {
        room = null;
        player = null;
        error = string.Empty;

        if (request.Type == "CreateRoomRequest")
        {
            string roomCode = CreateUniqueRoomCode();
            room = new RelayRoom(
                roomCode,
                string.IsNullOrWhiteSpace(request.RoomName) ? "Local Room" : request.RoomName.Trim(),
                request.IsPrivate,
                request.IsPrivate ? request.Password ?? string.Empty : string.Empty);
            player = room.AddPlayer(string.IsNullOrWhiteSpace(request.PlayerName) ? "Player" : request.PlayerName.Trim());
            _rooms[roomCode] = room;
            Console.WriteLine($"Room created: {roomCode}, private={room.IsPrivate}");
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
        writer.WriteLine(JsonRelay.ToJson(message));
    }
}

internal sealed class RelayRoom : IDisposable
{
    private const string RoomStateWaitingForPlayer = "waiting_for_player";
    private const string RoomStatePlayerJoined = "player_joined";
    private const string RoomStateInGame = "in_game";

    private readonly object _sync = new object();
    private readonly Dictionary<int, RelayPlayer> _players = new Dictionary<int, RelayPlayer>();
    private readonly Dictionary<int, RelayConnection> _connections = new Dictionary<int, RelayConnection>();

    public RelayRoom(string roomCode, string roomName, bool isPrivate, string password)
    {
        RoomCode = roomCode;
        RoomName = roomName;
        IsPrivate = isPrivate;
        Password = password ?? string.Empty;
        State = RoomStateWaitingForPlayer;
    }

    public string RoomCode { get; }
    public string RoomName { get; }
    public bool IsPrivate { get; }
    public string State { get; private set; }
    private string Password { get; }

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

    public RelayPlayer AddPlayer(string playerName)
    {
        lock (_sync)
        {
            RelayPlayer player = CreatePlayer(1, playerName);
            _players[player.PlayerId] = player;
            State = DetermineState();
            return player;
        }
    }

    public RelayPlayer? TryAddPlayer(string playerName, string password, out string error)
    {
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
            RelayPlayer player = CreatePlayer(playerId, playerName);
            _players[player.PlayerId] = player;
            State = DetermineState();
            error = string.Empty;
            return player;
        }
    }

    public void AttachConnection(RelayConnection connection)
    {
        lock (_sync)
        {
            _connections[connection.PlayerId] = connection;
        }
    }

    public void UpdatePosition(int playerId, float x, float y, float z)
    {
        lock (_sync)
        {
            if (_players.TryGetValue(playerId, out RelayPlayer? player))
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
        lock (_sync)
        {
            List<CoopPlayerSnapshot> snapshots = new List<CoopPlayerSnapshot>();
            foreach (RelayPlayer player in _players.Values)
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
        RelayConnection[] connections;
        CoopPlayerSnapshot[] snapshot;

        lock (_sync)
        {
            connections = _connections.Values.ToArray();
            snapshot = BuildSnapshot();
        }

        BroadcastToConnections(connections, connection => BuildRoomMessage("Snapshot", snapshot, connection.PlayerId));
    }

    public void BroadcastGameStarted()
    {
        RelayConnection[] connections;
        CoopPlayerSnapshot[] snapshot;

        lock (_sync)
        {
            connections = _connections.Values.ToArray();
            snapshot = BuildSnapshot();
        }

        BroadcastToConnections(connections, connection => BuildRoomMessage("GameStarted", snapshot, connection.PlayerId));
    }

    public void BroadcastMinesweeperCommand(string action, int cellX, int cellY, int boardSeed)
    {
        RelayConnection[] connections;
        CoopPlayerSnapshot[] snapshot;

        lock (_sync)
        {
            if (State != RoomStateInGame)
            {
                return;
            }

            connections = _connections.Values.ToArray();
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
            foreach (RelayConnection connection in _connections.Values)
            {
                connection.Dispose();
            }

            _connections.Clear();
            _players.Clear();
        }
    }

    private void BroadcastToConnections(RelayConnection[] connections, Func<RelayConnection, CoopNetworkMessage> buildMessage)
    {
        List<int> toRemove = new List<int>();
        foreach (RelayConnection connection in connections)
        {
            try
            {
                connection.Send(buildMessage(connection));
            }
            catch
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
                _connections.Remove(playerId);
                _players.Remove(playerId);
            }

            State = DetermineState();
        }
    }

    private static RelayPlayer CreatePlayer(int playerId, string playerName)
    {
        return new RelayPlayer
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
            Players = snapshot
        };
    }

    private string DetermineState()
    {
        if (State == RoomStateInGame)
        {
            return RoomStateInGame;
        }

        return _players.Count >= 2 ? RoomStatePlayerJoined : RoomStateWaitingForPlayer;
    }
}

internal sealed class RelayConnection : IDisposable
{
    private readonly TcpClient _client;
    private readonly StreamWriter _writer;
    private readonly object _writeLock = new object();

    public RelayConnection(TcpClient client, StreamWriter writer, string roomCode, int playerId)
    {
        _client = client;
        _writer = writer;
        RoomCode = roomCode;
        PlayerId = playerId;
    }

    public string RoomCode { get; }
    public int PlayerId { get; }

    public void Send(CoopNetworkMessage message)
    {
        lock (_writeLock)
        {
            _writer.WriteLine(JsonRelay.ToJson(message));
        }
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

internal sealed class RelayPlayer
{
    public int PlayerId;
    public string PlayerName = string.Empty;
    public float X;
    public float Y;
    public float Z;
}

#pragma warning disable CS0649
internal sealed class CoopNetworkMessage
{
    public string Type = string.Empty;
    public string RoomCode = string.Empty;
    public string RoomName = string.Empty;
    public string PlayerName = string.Empty;
    public string Password = string.Empty;
    public string Reason = string.Empty;
    public int PlayerId;
    public float X;
    public float Y;
    public float Z;
    public bool IsPrivate;
    public string RoomState = string.Empty;
    public bool IsHost;
    public bool CanStartGame;
    public long PingTicks;
    public string MinesweeperAction = string.Empty;
    public int CellX = -1;
    public int CellY = -1;
    public int BoardSeed;
    public CoopPlayerSnapshot[] Players = Array.Empty<CoopPlayerSnapshot>();
}

internal sealed class CoopPlayerSnapshot
{
    public int PlayerId;
    public string PlayerName = string.Empty;
    public float X;
    public float Y;
    public float Z;
}
#pragma warning restore CS0649

internal static class JsonRelay
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        IncludeFields = true
    };

    public static CoopNetworkMessage? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<CoopNetworkMessage>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string ToJson(CoopNetworkMessage message)
    {
        return JsonSerializer.Serialize(message, Options);
    }
}
