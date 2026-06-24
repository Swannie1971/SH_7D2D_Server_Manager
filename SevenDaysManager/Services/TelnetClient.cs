using System.IO;
using System.Net.Sockets;
using System.Text;

namespace SevenDaysManager.Services;

public sealed class TelnetClient : IAsyncDisposable
{
    private TcpClient?           _tcp;
    private StreamReader?        _reader;
    private StreamWriter?        _writer;
    private CancellationTokenSource? _cts;

    public event Action<string>? LineReceived;
    public event Action?         Disconnected;

    public bool IsConnected { get; private set; }

    // ── Connect ───────────────────────────────────────────────────────────────

    public async Task<bool> ConnectAsync(string host, int port, string password,
                                         CancellationToken ct = default)
    {
        try
        {
            _tcp = new TcpClient { ReceiveTimeout = 0 };
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(TimeSpan.FromSeconds(8));
            await _tcp.ConnectAsync(host, port, connectCts.Token);

            var stream = _tcp.GetStream();
            _reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false,
                                       bufferSize: 4096, leaveOpen: true);
            _writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 1024, leaveOpen: true)
                { AutoFlush = true, NewLine = "\r\n" };

            // Drain initial banner / password prompt
            await DrainAsync(700, ct);

            if (!string.IsNullOrEmpty(password))
            {
                await _writer.WriteLineAsync(password);
                await DrainAsync(700, ct);
            }

            IsConnected = true;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _ = ReadLoopAsync(_cts.Token);
            return true;
        }
        catch
        {
            IsConnected = false;
            return false;
        }
    }

    // ── Send ──────────────────────────────────────────────────────────────────

    public async Task SendAsync(string command)
    {
        if (_writer is null || !IsConnected) return;
        try { await _writer.WriteLineAsync(command); }
        catch { IsConnected = false; }
    }

    // ── Disconnect ────────────────────────────────────────────────────────────

    public void Disconnect()
    {
        _cts?.Cancel();
        IsConnected = false;
        _tcp?.Close();
    }

    // ── Read loop ─────────────────────────────────────────────────────────────

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _reader is not null)
            {
                var line = await _reader.ReadLineAsync(ct);
                if (line is null) break;          // server closed connection
                LineReceived?.Invoke(line);
            }
        }
        catch (OperationCanceledException) { }
        catch { /* connection dropped */ }
        finally
        {
            IsConnected = false;
            Disconnected?.Invoke();
        }
    }

    // Reads and discards available data for a short window (auth handshake)
    private async Task DrainAsync(int ms, CancellationToken ct)
    {
        var buf = new byte[4096];
        var deadline = DateTime.UtcNow.AddMilliseconds(ms);
        var stream = _tcp?.GetStream();
        if (stream is null) return;
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            if (stream.DataAvailable) await stream.ReadAsync(buf, ct);
            else await Task.Delay(40, ct);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Disconnect();
        if (_writer is not null) await _writer.DisposeAsync();
        _reader?.Dispose();
        _tcp?.Dispose();
        _cts?.Dispose();
    }
}
