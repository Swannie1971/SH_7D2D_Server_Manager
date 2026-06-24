# Telnet Monitor for 7 Days to Die Server (C# WPF, MVVM)

This document provides a full async Telnet reader pattern for detecting when a 7 Days to Die dedicated server transitions from **Starting → Running**.  
The reliable signal is the log line:


---

## Why Telnet Streaming?

- **Direct console output**: Telnet reflects the live server console, including the `GameServer.LogOn successful` line.
- **Reliable timing**: Process start and port open happen too early; only the log line confirms the world is loaded and Steam auth succeeded.
- **Cross‑platform consistency**: Log files may vary by OS/version, but Telnet always works.

---

## C# Telnet Reader Implementation

```csharp
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class TelnetMonitor
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _password;
    private readonly Action<string> _onLineReceived;
    private readonly Action _onServerRunning;

    public TelnetMonitor(string host, int port, string password,
                         Action<string> onLineReceived,
                         Action onServerRunning)
    {
        _host = host;
        _port = port;
        _password = password;
        _onLineReceived = onLineReceived;
        _onServerRunning = onServerRunning;
    }

    public async Task StartAsync(CancellationToken token)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(_host, _port);

        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII);
        using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };

        // Send password for login
        await writer.WriteLineAsync(_password);

        while (!token.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break;

            _onLineReceived?.Invoke(line);

            if (line.Contains("GameServer.LogOn successful", StringComparison.OrdinalIgnoreCase))
            {
                _onServerRunning?.Invoke();
            }
        }
    }
}
