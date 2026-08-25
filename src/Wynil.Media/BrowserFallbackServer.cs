using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using Wynil.Core.Models;

namespace Wynil.Media;

public sealed class BrowserFallbackServer : IAsyncDisposable
{
    public const int Port = 17842;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _listenTask;
    private readonly ConcurrentDictionary<WebSocket, byte> _viewers = new();

    public BrowserFallbackServer(string? tokenPath = null)
    {
        tokenPath ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Wynil", "browser-token.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(tokenPath)!);
        Token = File.Exists(tokenPath) ? File.ReadAllText(tokenPath).Trim() : Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        if (!File.Exists(tokenPath)) File.WriteAllText(tokenPath, Token);
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/wynil/");
    }

    public string Token { get; }
    public event EventHandler<Core.Models.MediaTrack>? TrackReceived;

    public Task StartAsync()
    {
        if (_listenTask is not null) return Task.CompletedTask;
        _listener.Start();
        _listenTask = ListenAsync(_shutdown.Token);
        return Task.CompletedTask;
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested) { break; }
            _ = HandleAsync(context, cancellationToken);
        }
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var supplied = context.Request.QueryString["token"] ?? string.Empty;
        var validToken = supplied.Length == Token.Length && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(Token));
        if (!context.Request.IsWebSocketRequest || !validToken)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.Close();
            return;
        }

        var socketContext = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
        using var socket = socketContext.WebSocket;
        if (context.Request.QueryString["role"] == "viewer")
        {
            _viewers.TryAdd(socket, 0);
            try
            {
                var viewerBuffer = new byte[256];
                while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var viewerResult = await socket.ReceiveAsync(viewerBuffer, cancellationToken).ConfigureAwait(false);
                    if (viewerResult.MessageType == WebSocketMessageType.Close) break;
                }
            }
            finally { _viewers.TryRemove(socket, out _); }
            return;
        }
        var buffer = new byte[64 * 1024];
        using var message = new MemoryStream();
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close) break;
            if (result.MessageType != WebSocketMessageType.Text) continue;
            message.Write(buffer, 0, result.Count);
            if (message.Length > BrowserMessageValidator.MaximumMessageBytes)
            {
                await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Message too large", cancellationToken).ConfigureAwait(false);
                return;
            }
            if (!result.EndOfMessage) continue;

            try
            {
                var payload = JsonSerializer.Deserialize<BrowserMediaMessage>(message.ToArray(), JsonOptions);
                if (BrowserMessageValidator.TryCreateTrack(payload, out var track)) TrackReceived?.Invoke(this, track);
            }
            catch (JsonException) { }
            message.SetLength(0);
        }
    }

    public async Task BroadcastTrackAsync(MediaTrack track, CancellationToken cancellationToken = default)
    {
        var artworkUrl = track.ArtworkPath is null
            ? null
            : new Uri(track.ArtworkPath).AbsoluteUri;
        var json = JsonSerializer.SerializeToUtf8Bytes(new
        {
            version = 1,
            type = "track",
            payload = new
            {
                track.Title,
                track.Artist,
                track.Album,
                track.SourceApplication,
                artworkUrl,
                track.IsPlaying,
                positionSeconds = track.Position.TotalSeconds,
                durationSeconds = track.Duration.TotalSeconds
            }
        }, JsonOptions);

        foreach (var viewer in _viewers.Keys)
        {
            if (viewer.State != WebSocketState.Open) { _viewers.TryRemove(viewer, out _); continue; }
            try { await viewer.SendAsync(json, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false); }
            catch (WebSocketException) { _viewers.TryRemove(viewer, out _); }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        foreach (var viewer in _viewers.Keys)
        {
            try { viewer.Abort(); } catch (ObjectDisposedException) { }
        }
        _listener.Close();
        if (_listenTask is not null)
        {
            try { await _listenTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        _shutdown.Dispose();
    }
}
