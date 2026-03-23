using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Manages a WebSocket connection to the position-sync server.
/// Call Connect() after joining a lobby and Disconnect() when leaving.
/// Sends position updates via SendPosition(); fires events on the main thread
/// when the server pushes welcome / player-joined / player-left / position messages.
///
/// Attach this component to any persistent GameObject and assign it to GameManager
/// via the Inspector. Set the Server URL field to your deployed server's wss:// address.
/// </summary>
public class PositionSyncClient : MonoBehaviour
{
    [Tooltip("WebSocket URL of the position sync server")]
    [SerializeField] private string serverUrl = "wss://dungeon-delvers-3d.onrender.com";

    private ClientWebSocket _ws;
    private CancellationTokenSource _cts;
    private readonly ConcurrentQueue<string> _receiveQueue = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public bool IsConnected => _ws?.State == WebSocketState.Open;

    // ── Events (dispatched on the Unity main thread in Update) ────────────────
    public event Action<PlayerState[]> OnWelcome;
    public event Action<ulong> OnPlayerJoined;
    public event Action<ulong> OnPlayerLeft;
    public event Action<ulong, Vector3, float> OnPositionReceived;

    // ── Public API ────────────────────────────────────────────────────────────

    public async void Connect(string roomId, ulong userId)
    {
        if (IsConnected) return;
        if (string.IsNullOrEmpty(serverUrl) || serverUrl.Contains("your-app"))
        {
            Debug.LogWarning("[PositionSyncClient] Server URL not configured. Set it in the Inspector.");
            return;
        }

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _ws?.Dispose();
        _ws = new ClientWebSocket();

        try
        {
            await _ws.ConnectAsync(new Uri(serverUrl), _cts.Token);
            await SendRaw(JsonUtility.ToJson(new JoinMsg
            {
                type = "join",
                lobbyId = roomId,
                userId = userId.ToString()
            }));
            _ = ReceiveLoop();
            Debug.Log($"[PositionSyncClient] Connected to {serverUrl}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[PositionSyncClient] Connect failed: {e.Message}");
        }
    }

    public async void Disconnect()
    {
        _cts?.Cancel();
        if (_ws != null && _ws.State == WebSocketState.Open)
        {
            try { await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); }
            catch { }
        }
        _ws?.Dispose();
        _ws = null;
        Debug.Log("[PositionSyncClient] Disconnected");
    }

    public void SendPosition(Vector3 pos, float yaw)
    {
        if (!IsConnected) return;
        _ = SendRaw(JsonUtility.ToJson(new PosMsg
        {
            type = "position",
            x = pos.x, y = pos.y, z = pos.z, yaw = yaw
        }));
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Update()
    {
        while (_receiveQueue.TryDequeue(out var json))
            ProcessMessage(json);
    }

    void OnDestroy() => Disconnect();

    // ── Internal ─────────────────────────────────────────────────────────────

    private async Task ReceiveLoop()
    {
        var buffer = new byte[4096];
        var sb = new StringBuilder();

        try
        {
            while (_ws?.State == WebSocketState.Open && !(_cts?.IsCancellationRequested ?? true))
            {
                sb.Clear();
                WebSocketReceiveResult result;
                do
                {
                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!result.EndOfMessage);

                _receiveQueue.Enqueue(sb.ToString());
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogWarning($"[PositionSyncClient] Receive loop ended: {e.Message}");
        }
    }

    private async Task SendRaw(string json)
    {
        await _sendLock.WaitAsync();
        try
        {
            if (_ws == null || _ws.State != WebSocketState.Open) return;
            var bytes = Encoding.UTF8.GetBytes(json);
            await _ws.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                _cts?.Token ?? CancellationToken.None);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PositionSyncClient] Send failed: {e.Message}");
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private void ProcessMessage(string json)
    {
        ServerMsg msg;
        try { msg = JsonUtility.FromJson<ServerMsg>(json); }
        catch { return; }

        switch (msg.type)
        {
            case "welcome":
                OnWelcome?.Invoke(msg.players ?? Array.Empty<PlayerState>());
                break;
            case "joined":
                if (ulong.TryParse(msg.userId, out var joinedId))
                    OnPlayerJoined?.Invoke(joinedId);
                break;
            case "left":
                if (ulong.TryParse(msg.userId, out var leftId))
                    OnPlayerLeft?.Invoke(leftId);
                break;
            case "position":
                if (ulong.TryParse(msg.userId, out var posId))
                    OnPositionReceived?.Invoke(posId, new Vector3(msg.x, msg.y, msg.z), msg.yaw);
                break;
        }
    }

    // ── Serializable types ────────────────────────────────────────────────────

    [Serializable]
    public class PlayerState
    {
        public string userId;
        public float x, y, z, yaw;
    }

    [Serializable]
    private class JoinMsg
    {
        public string type;
        public string lobbyId;
        public string userId;
    }

    [Serializable]
    private class PosMsg
    {
        public string type;
        public float x, y, z, yaw;
    }

    [Serializable]
    private class ServerMsg
    {
        public string type;
        public string userId;
        public float x, y, z, yaw;
        public PlayerState[] players;
    }
}
