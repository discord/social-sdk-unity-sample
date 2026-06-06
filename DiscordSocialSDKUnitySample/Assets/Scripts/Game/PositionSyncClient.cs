using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Owns position networking end-to-end:
///   - subscribes to GameManager.OnLobbyJoined / OnLobbyLeft so it knows
///     when to connect and disconnect from the position-sync WebSocket server
///   - drives the send loop using GameManager.LocalPlayerTransform
///   - routes incoming positions onto each remote player via
///     GameManager.GetRemotePlayer(userId).SetTarget(...)
///
/// This is *separate* from the Discord Social SDK and intentionally lives in
/// its own file — GameManager stays pure Social-SDK code so the proximity-audio
/// demo can be live-coded without this noise.
///
/// Drop this component on any persistent GameObject in the scene and set the
/// Server URL in the Inspector.
/// </summary>
public class PositionSyncClient : MonoBehaviour
{
    [Tooltip("WebSocket URL of the position sync server")]
    [SerializeField] private string serverUrl = "wss://dungeon-delvers-3d.onrender.com";

    [Tooltip("How often (seconds) the local player's position is sent to the server.")]
    [SerializeField] private float positionSendInterval = 0.1f;

    private ClientWebSocket _ws;
    private CancellationTokenSource _cts;
    private readonly ConcurrentQueue<string> _receiveQueue = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private Coroutine _sendLoop;

    public bool IsConnected => _ws?.State == WebSocketState.Open;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLobbyJoined += OnLobbyJoined;
            GameManager.Instance.OnLobbyLeft += OnLobbyLeft;
        }
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLobbyJoined -= OnLobbyJoined;
            GameManager.Instance.OnLobbyLeft -= OnLobbyLeft;
        }
        Disconnect();
    }

    void Update()
    {
        while (_receiveQueue.TryDequeue(out var json))
            ProcessMessage(json);
    }

    // ── Lobby orchestration ───────────────────────────────────────────────────

    private void OnLobbyJoined(ulong lobbyId, string secret)
    {
        Connect(secret, GameManager.Instance.MyUserId);
        _sendLoop = StartCoroutine(SendPositionLoop());
    }

    private void OnLobbyLeft()
    {
        if (_sendLoop != null) StopCoroutine(_sendLoop);
        _sendLoop = null;
        Disconnect();
    }

    private IEnumerator SendPositionLoop()
    {
        var wait = new WaitForSeconds(positionSendInterval);
        while (GameManager.Instance != null && GameManager.Instance.IsInLobby)
        {
            var t = GameManager.Instance.LocalPlayerTransform;
            if (t != null && IsConnected)
                SendPosition(t.position, t.eulerAngles.y);
            yield return wait;
        }
    }

    private void HandleWelcome(PlayerState[] players)
    {
        if (GameManager.Instance == null) return;
        foreach (var p in players)
        {
            if (!ulong.TryParse(p.userId, out var uid)) continue;
            var remote = GameManager.Instance.GetRemotePlayer(uid);
            if (remote != null) remote.SetTarget(new Vector3(p.x, p.y, p.z), p.yaw);
        }
    }

    private void HandlePositionReceived(ulong userId, Vector3 pos, float yaw)
    {
        if (GameManager.Instance == null) return;
        var remote = GameManager.Instance.GetRemotePlayer(userId);
        if (remote != null) remote.SetTarget(pos, yaw);
    }

    // ── WebSocket transport ───────────────────────────────────────────────────

    private async void Connect(string roomId, ulong userId)
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

    private async void Disconnect()
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

    private void SendPosition(Vector3 pos, float yaw)
    {
        if (!IsConnected) return;
        _ = SendRaw(JsonUtility.ToJson(new PosMsg
        {
            type = "position",
            x = pos.x, y = pos.y, z = pos.z, yaw = yaw
        }));
    }

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
                HandleWelcome(msg.players ?? Array.Empty<PlayerState>());
                break;
            case "position":
                if (ulong.TryParse(msg.userId, out var posId))
                    HandlePositionReceived(posId, new Vector3(msg.x, msg.y, msg.z), msg.yaw);
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
