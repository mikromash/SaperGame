using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

var options = RelayServerOptions.Parse(args);
using var server = new RelayServer(options.Port);

Console.WriteLine($"Relay server listening on 0.0.0.0:{options.Port}");
Console.WriteLine("Press Ctrl+C to stop.");

using CancellationTokenSource cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};

await server.RunAsync(cts.Token);

internal sealed class RelayServerOptions
{
    public int Port { get; private init; } = 7777;

    public static RelayServerOptions Parse(string[] args)
    {
        int port = 7777;

        for (int index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--port", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                if (!int.TryParse(args[index + 1], out port) || port <= 0 || port > 65535)
                {
                    throw new ArgumentException("Port must be between 1 and 65535.");
                }

                index++;
            }
        }

        return new RelayServerOptions { Port = port };
    }
}

internal sealed class RelayServer : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        IncludeFields = true
    };

    private readonly TcpListener _listener;
    private readonly ConcurrentDictionary<string, RelayRoom> _rooms = new ConcurrentDictionary<string, RelayRoom>(StringComparer.OrdinalIgnoreCase);

    public RelayServer(int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _listener.Start();

        Task acceptTask = AcceptLoopAsync(cancellationToken);
        Task broadcastTask = BroadcastLoopAsync(cancellationToken);

        await Task.WhenAny(acceptTask, broadcastTask);
        _listener.Stop();
    }

    public void Dispose()
    {
        _listener.Stop();

        foreach (RelayRoom room in _rooms.Values)
        {
            room.Dispose();
        }
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;

            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            client.NoDelay = true;
            _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
        }
    }

    private async Task BroadcastLoopAsync(CancellationToken cancellationToken)
    {
        PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                foreach (RelayRoom room in _rooms.Values)
                {
                    await room.BroadcastSnapshotAsync(cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            timer.Dispose();
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        RelayConnection? connection = null;

        try
        {
            using NetworkStream stream = client.GetStream();
            using StreamReader reader = new StreamReader(stream, Encoding.UTF8);
            using StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            string? firstLine = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(firstLine))
            {
                return;
            }

            RelayMessage? request = Deserialize(firstLine);
            if (request is null)
            {
                await SendAsync(writer, new RelayMessage
                {
                    Type = "Error",
                    Reason = "Invalid request payload."
                }, cancellationToken);
                return;
            }

            (RelayRoom room, RelayPlayer player, string? error) = RegisterConnection(request);
            if (room is null || player is null)
            {
                await SendAsync(writer, new RelayMessage
                {
                    Type = "Error",
                    Reason = string.IsNullOrWhiteSpace(error) ? "Request rejected." : error
                }, cancellationToken);
                return;
            }

            connection = new RelayConnection(client, writer, room.RoomCode, player.PlayerId);
            room.AttachConnection(connection);

            await SendAsync(writer, new RelayMessage
            {
                Type = "RoomConnected",
                RoomCode = room.RoomCode,
                RoomName = room.RoomName,
                PlayerId = player.PlayerId,
                IsPrivate = room.IsPrivate,
                Players = room.BuildSnapshot()
            }, cancellationToken);

            await room.BroadcastSnapshotAsync(cancellationToken);

            while (!cancellationToken.IsCancellationRequested && client.Connected)
            {
                string? line = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(line))
                {
                    break;
                }

                RelayMessage? message = Deserialize(line);
                if (message?.Type == "Move")
                {
                    room.UpdatePosition(player.PlayerId, message.X, message.Z);
                }
            }
        }
        catch (IOException)
        {
        }
        catch (SocketException)
        {
        }
        catch (OperationCanceledException)
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
                    await room.BroadcastSnapshotAsync(CancellationToken.None);
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

    private (RelayRoom room, RelayPlayer player, string error) RegisterConnection(RelayMessage request)
    {
        if (request.Type == "CreateRoomRequest")
        {
            string roomCode = CreateUniqueRoomCode();
            string roomName = SanitizeRoomName(request.RoomName);
            bool isPrivate = request.IsPrivate;
            string password = isPrivate ? request.Password ?? string.Empty : string.Empty;
            RelayRoom room = new RelayRoom(roomCode, roomName, isPrivate, password);
            RelayPlayer player = room.AddPlayer(SanitizePlayerName(request.PlayerName));
            _rooms[roomCode] = room;
            Console.WriteLine($"Room created: {roomCode}");
            return (room, player, string.Empty);
        }

        if (request.Type == "JoinRoomRequest")
        {
            string roomCode = SanitizeRoomCode(request.RoomCode);
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                return (null!, null!, "Room code is required.");
            }

            if (!_rooms.TryGetValue(roomCode, out RelayRoom? room))
            {
                return (null!, null!, "Room not found.");
            }

            RelayPlayer? player = room.TryAddPlayer(
                SanitizePlayerName(request.PlayerName),
                request.Password ?? string.Empty,
                out string? error);
            if (player is null)
            {
                return (null!, null!, string.IsNullOrWhiteSpace(error) ? "Could not join room." : error);
            }

            Console.WriteLine($"Player joined room: {roomCode}");
            return (room, player, string.Empty);
        }

        return (null!, null!, "Unknown request type.");
    }

    private string CreateUniqueRoomCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        while (true)
        {
            char[] chars = new char[6];
            for (int index = 0; index < chars.Length; index++)
            {
                chars[index] = alphabet[Random.Shared.Next(alphabet.Length)];
            }

            string roomCode = new string(chars);
            if (!_rooms.ContainsKey(roomCode))
            {
                return roomCode;
            }
        }
    }

    private static RelayMessage? Deserialize(string payload)
    {
        return JsonSerializer.Deserialize<RelayMessage>(payload, JsonOptions);
    }

    private static async Task SendAsync(StreamWriter writer, RelayMessage message, CancellationToken cancellationToken)
    {
        string payload = JsonSerializer.Serialize(message, JsonOptions);
        await writer.WriteLineAsync(payload.AsMemory(), cancellationToken);
    }

    private static string SanitizeRoomCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
    }

    private static string SanitizePlayerName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Player";
        }

        string trimmed = value.Trim();
        return trimmed.Length > 24 ? trimmed[..24] : trimmed;
    }

    private static string SanitizeRoomName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Room";
        }

        string trimmed = value.Trim();
        return trimmed.Length > 32 ? trimmed[..32] : trimmed;
    }
}

internal sealed class RelayRoom : IDisposable
{
    private readonly object _sync = new object();
    private readonly Dictionary<int, RelayPlayer> _players = new Dictionary<int, RelayPlayer>();
    private readonly Dictionary<int, RelayConnection> _connections = new Dictionary<int, RelayConnection>();

    public RelayRoom(string roomCode, string roomName, bool isPrivate, string password)
    {
        RoomCode = roomCode;
        RoomName = roomName;
        IsPrivate = isPrivate;
        Password = password;
    }

    public string RoomCode { get; }

    public string RoomName { get; }

    public bool IsPrivate { get; }

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
            return player;
        }
    }

    public RelayPlayer? TryAddPlayer(string playerName, string password, out string? error)
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
            error = null;
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

    public void UpdatePosition(int playerId, float x, float z)
    {
        lock (_sync)
        {
            if (_players.TryGetValue(playerId, out RelayPlayer? player))
            {
                player.X = x;
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
        }
    }

    public RelayPlayerSnapshot[] BuildSnapshot()
    {
        lock (_sync)
        {
            return _players.Values
                .OrderBy(player => player.PlayerId)
                .Select(player => new RelayPlayerSnapshot
                {
                    PlayerId = player.PlayerId,
                    PlayerName = player.PlayerName,
                    X = player.X,
                    Z = player.Z
                })
                .ToArray();
        }
    }

    public async Task BroadcastSnapshotAsync(CancellationToken cancellationToken)
    {
        RelayConnection[] connections;
        RelayPlayerSnapshot[] snapshot;

        lock (_sync)
        {
            connections = _connections.Values.ToArray();
            snapshot = BuildSnapshot();
        }

        RelayMessage message = new RelayMessage
        {
            Type = "Snapshot",
            RoomCode = RoomCode,
            RoomName = RoomName,
            IsPrivate = IsPrivate,
            Players = snapshot
        };

        List<int> disconnectedPlayers = new List<int>();
        foreach (RelayConnection connection in connections)
        {
            try
            {
                await connection.SendAsync(message, cancellationToken);
            }
            catch
            {
                disconnectedPlayers.Add(connection.PlayerId);
            }
        }

        if (disconnectedPlayers.Count == 0)
        {
            return;
        }

        lock (_sync)
        {
            foreach (int playerId in disconnectedPlayers)
            {
                _connections.Remove(playerId);
                _players.Remove(playerId);
            }
        }
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

    private static RelayPlayer CreatePlayer(int playerId, string playerName)
    {
        return new RelayPlayer
        {
            PlayerId = playerId,
            PlayerName = playerName,
            X = playerId == 1 ? -2f : 2f,
            Z = 0f
        };
    }
}

internal sealed class RelayConnection : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        IncludeFields = true
    };

    private readonly TcpClient _client;
    private readonly StreamWriter _writer;
    private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);

    public RelayConnection(TcpClient client, StreamWriter writer, string roomCode, int playerId)
    {
        _client = client;
        _writer = writer;
        RoomCode = roomCode;
        PlayerId = playerId;
    }

    public string RoomCode { get; }

    public int PlayerId { get; }

    public async Task SendAsync(RelayMessage message, CancellationToken cancellationToken)
    {
        string payload = JsonSerializer.Serialize(message, JsonOptions);
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await _writer.WriteLineAsync(payload.AsMemory(), cancellationToken);
        }
        finally
        {
            _writeLock.Release();
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
    public int PlayerId { get; set; }

    public string PlayerName { get; set; } = string.Empty;

    public float X { get; set; }

    public float Z { get; set; }
}

internal sealed class RelayMessage
{
    public string Type = string.Empty;

    public string? RoomCode;

    public string? RoomName = string.Empty;

    public string? PlayerName = string.Empty;

    public string? Password = string.Empty;

    public string? Reason;

    public int PlayerId = 0;

    public float X = 0f;

    public float Z = 0f;

    public bool IsPrivate = false;

    public RelayPlayerSnapshot[]? Players;
}

internal sealed class RelayPlayerSnapshot
{
    public int PlayerId;

    public string PlayerName = string.Empty;

    public float X;

    public float Z;
}
