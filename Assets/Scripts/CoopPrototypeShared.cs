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
    public string Type = string.Empty;
    public string RoomCode = string.Empty;
    public string RoomName = string.Empty;
    public string PlayerName = string.Empty;
    public string Password = string.Empty;
    public string Reason = string.Empty;
    public int PlayerId = 0;
    public float X = 0f;
    public float Z = 0f;
    public bool IsPrivate = false;
    public string RoomState = string.Empty;
    public bool IsHost = false;
    public bool CanStartGame = false;
    public long PingTicks = 0L;
    public CoopPlayerSnapshot[] Players = Array.Empty<CoopPlayerSnapshot>();
}

[Serializable]
internal sealed class CoopPlayerSnapshot
{
    public int PlayerId = 0;
    public string PlayerName = string.Empty;
    public float X = 0f;
    public float Z = 0f;
}

internal sealed class CoopAvatarView
{
    public CoopScenePlayerAvatar SceneAvatar;
    public Vector3 TargetPosition;
}

[Serializable]
internal sealed class CoopRelaySettingsData
{
    public string relayHost = "127.0.0.1";
    public int relayPort = 7777;
}

internal static class CoopRelaySettings
{
    private static CoopRelaySettingsData _cached;

    public static CoopRelaySettingsData Load()
    {
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

internal sealed class CoopRelayClient
{
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

    public bool CreateRoom(string host, int port, string playerName, string roomName, bool isPrivate, string password)
    {
        return Connect(host, port, new CoopNetworkMessage
        {
            Type = "CreateRoomRequest",
            PlayerName = playerName,
            RoomName = roomName,
            IsPrivate = isPrivate,
            Password = password ?? string.Empty
        });
    }

    public bool JoinRoom(string host, int port, string roomCode, string playerName, string password)
    {
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

    public bool TryDequeue(out CoopNetworkMessage message)
    {
        return _incoming.TryDequeue(out message);
    }

    private bool Connect(string host, int port, CoopNetworkMessage request)
    {
        Disconnect();

        try
        {
            ConnectionState = CoopPrototypeController.PlayerConnectionState.Connecting;
            Status = "Connecting to relay server...";
            DisconnectStatus = null;

            _client = new TcpClient();
            _client.NoDelay = true;
            _client.Connect(host, port);

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
        while (_running)
        {
            try
            {
                TcpClient client = _listener.AcceptTcpClient();
                client.NoDelay = true;
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

                    CoopNetworkMessage message = JsonUtility.FromJson<CoopNetworkMessage>(line);
                    if (message != null && message.Type == "Move")
                    {
                        room.UpdatePosition(player.PlayerId, message.X, message.Z);
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
                request.IsPrivate ? request.Password ?? string.Empty : string.Empty);
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
    private const string RoomStateWaitingForPlayer = "waiting_for_player";
    private const string RoomStatePlayerJoined = "player_joined";
    private const string RoomStateInGame = "in_game";

    private readonly object _sync = new object();
    private readonly Dictionary<int, CoopEmbeddedRelayPlayer> _players = new Dictionary<int, CoopEmbeddedRelayPlayer>();
    private readonly Dictionary<int, CoopEmbeddedRelayConnection> _connections = new Dictionary<int, CoopEmbeddedRelayConnection>();

    public CoopEmbeddedRelayRoom(string roomCode, string roomName, bool isPrivate, string password)
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

    public void UpdatePosition(int playerId, float x, float z)
    {
        lock (_sync)
        {
            if (_players.TryGetValue(playerId, out CoopEmbeddedRelayPlayer player))
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
            foreach (CoopEmbeddedRelayPlayer player in _players.Values)
            {
                snapshots.Add(new CoopPlayerSnapshot
                {
                    PlayerId = player.PlayerId,
                    PlayerName = player.PlayerName,
                    X = player.X,
                    Z = player.Z
                });
            }

            snapshots.Sort((left, right) => left.PlayerId.CompareTo(right.PlayerId));
            return snapshots.ToArray();
        }
    }

    public void BroadcastSnapshot()
    {
        CoopEmbeddedRelayConnection[] connections;
        CoopPlayerSnapshot[] snapshot;

        lock (_sync)
        {
            List<CoopEmbeddedRelayConnection> list = new List<CoopEmbeddedRelayConnection>(_connections.Values);
            connections = list.ToArray();
            snapshot = BuildSnapshot();
        }

        List<int> toRemove = new List<int>();
        foreach (CoopEmbeddedRelayConnection connection in connections)
        {
            try
            {
                connection.Send(BuildRoomMessage("Snapshot", snapshot, connection.PlayerId));
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

    public void BroadcastGameStarted()
    {
        CoopEmbeddedRelayConnection[] connections;
        CoopPlayerSnapshot[] snapshot;

        lock (_sync)
        {
            List<CoopEmbeddedRelayConnection> list = new List<CoopEmbeddedRelayConnection>(_connections.Values);
            connections = list.ToArray();
            snapshot = BuildSnapshot();
        }

        List<int> toRemove = new List<int>();
        foreach (CoopEmbeddedRelayConnection connection in connections)
        {
            try
            {
                connection.Send(BuildRoomMessage("GameStarted", snapshot, connection.PlayerId));
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
        return new CoopEmbeddedRelayPlayer
        {
            PlayerId = playerId,
            PlayerName = playerName,
            X = playerId == 1 ? -2f : 2f,
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

        if (_players.Count >= 2)
        {
            return RoomStatePlayerJoined;
        }

        return RoomStateWaitingForPlayer;
    }
}

internal sealed class CoopEmbeddedRelayConnection : IDisposable
{
    private readonly TcpClient _client;
    private readonly StreamWriter _writer;
    private readonly object _writeLock = new object();

    public CoopEmbeddedRelayConnection(TcpClient client, StreamWriter writer, string roomCode, int playerId)
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
            _writer.WriteLine(JsonUtility.ToJson(message));
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

internal sealed class CoopEmbeddedRelayPlayer
{
    public int PlayerId;
    public string PlayerName;
    public float X;
    public float Z;
}
