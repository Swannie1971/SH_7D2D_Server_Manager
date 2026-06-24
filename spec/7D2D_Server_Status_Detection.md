# 7D2D Server Status Detection

For 7D2D specifically, I would not trust any of the following as a "server is fully running" signal:

- Process started
- Process CPU settles
- Process stdout becomes active
- Telnet port opens
- Game port opens
- Query port responds

All of those can happen before the world is loaded and before Steam registration completes.

The message:

```text
[Steamworks.NET] GameServer.LogOn successful
```

is actually one of the best indicators available because it occurs after:

1. Server initialization
2. World load/generation
3. Steam Game Server login

So your current approach is quite reasonable.

## Option 1: Capture stdout directly (best if possible)

If you're launching the server yourself via `Process.Start`, first verify whether the server writes this line to standard output.

Example:

```csharp
var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = serverExe,
        Arguments = args,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    },
    EnableRaisingEvents = true
};

process.OutputDataReceived += Process_OutputDataReceived;
process.ErrorDataReceived += Process_OutputDataReceived;

process.Start();

process.BeginOutputReadLine();
process.BeginErrorReadLine();
```

Then:

```csharp
private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
{
    if (string.IsNullOrEmpty(e.Data))
        return;

    if (e.Data.Contains("GameServer.LogOn successful"))
    {
        ServerStatus = ServerStatus.Running;
    }
}
```

If that line appears there, this is the cleanest solution because you're already attached to the process.

The catch:

- Unity servers sometimes bypass stdout.
- Some builds write to Unity logs only.
- Some dedicated server versions produce incomplete stdout.

So you need to verify whether you actually receive that message.

---

## Option 2: Live Telnet monitoring (probably the most practical)

If the message is definitely visible via Telnet, I'd actually consider this a solid production solution.

Architecture:

```text
Process Start
        ↓
Wait for Telnet port
        ↓
Connect Telnet
        ↓
Authenticate
        ↓
Stream lines continuously
        ↓
Detect:
    GameServer.LogOn successful
        ↓
Status = Running
```

This is effectively what tools like LGSM and other server managers end up doing.

---

## Pitfalls with Telnet

### 1. Telnet reconnects

Sometimes the server may:

- Restart internally
- Reload networking
- Drop the connection

Handle:

```csharp
while (!token.IsCancellationRequested)
{
    try
    {
        await MonitorTelnetAsync();
    }
    catch
    {
        await Task.Delay(5000, token);
    }
}
```

---

### 2. Partial lines

Don't assume:

```csharp
ReadAsync()
```

returns complete log lines.

Use:

```csharp
StreamReader.ReadLineAsync()
```

or implement buffering.

---

### 3. Telnet negotiation bytes

Some Telnet servers emit negotiation commands:

```text
FF FD xx
FF FB xx
```

If you're doing raw socket reads, strip them.

If you're using a Telnet library, it handles this automatically.

---

### 4. Server already running

Suppose your manager starts and attaches to a server that's already running.

You won't see:

```text
GameServer.LogOn successful
```

because it happened earlier.

You'll want a fallback:

```text
State = Unknown
```

Connect Telnet and issue:

```text
version
```

or

```text
lp
```

(or another harmless command)

If you get a valid response, assume Running.

---

### 5. Steam failure

Watch for failure messages too:

```text
Steam initialization failed
SteamGameServer_Init failed
```

Then you can transition:

```text
Starting → Error
```

instead of waiting forever.

---

## Option 3: Query game readiness via Telnet command

An even cleaner approach is:

1. Wait for Telnet.
2. Authenticate.
3. Poll a command every few seconds.

Examples:

```text
version
```

```text
gettime
```

```text
lp
```

```text
mem
```

Once commands return valid game-world data, you're effectively running.

However, depending on the server version, some commands may respond before Steam login is complete, so this is not always as reliable as watching for the actual log message.

---

## What I would implement

A small state machine:

```text
Stopped
    ↓
Process started
    ↓
Starting
    ↓
Telnet connected
    ↓
Initializing
    ↓
GameServer.LogOn successful
    ↓
Running
```

With timeout handling:

```text
Starting > 10 minutes
    ↓
Failed
```

And monitor both:

- Process exit
- Telnet stream

Once you see:

```text
GameServer.LogOn successful
```

mark the server as Running and keep the Telnet connection alive for ongoing log monitoring.

For a 7D2D server manager, I'd consider the Telnet log stream the most reliable external readiness signal unless you've confirmed that the same line is available through redirected stdout.
