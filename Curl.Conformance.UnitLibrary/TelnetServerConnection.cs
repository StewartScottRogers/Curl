using System.Net;
using System.Runtime.InteropServices;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// One client connection to a <see cref="TelnetServerConnector"/>, answering as upstream's
/// <c>tests/negtelnetserver.py</c> (at <c>curl-8_22_0</c>) does: it sends <see cref="Negotiation"/> on
/// connect, reads the client's bytes past its TELNET commands until a write leaves some data, echoes
/// that data with surrounding whitespace removed, and closes.
/// </summary>
/// <remarks>
/// As negtelnetserver.py's <c>Negotiator</c> reads them, <c>IAC</c> followed by <c>WILL</c>, <c>WONT</c>,
/// <c>DO</c> or <c>DONT</c> takes one more byte, the option, and <c>IAC</c> followed by any other byte
/// takes that byte only; nothing is answered to them. A read with nothing waiting waits for the client's
/// next write, and returns 0 once the echo is read. Bytes written after the echo are dropped.
/// </remarks>
internal sealed class TelnetServerConnection : IConnection
{
    /// <summary>
    /// The bytes negtelnetserver.py sends first: <c>IAC WILL NEW_ENVIRON</c>, <c>IAC WONT NAWS</c>,
    /// <c>IAC DO NEW_ENVIRON</c> and <c>IAC DONT NAWS</c>.
    /// </summary>
    public static readonly byte[] Negotiation = [255, 251, 39, 255, 252, 31, 255, 253, 39, 255, 254, 31];

    private const byte InterpretAsCommand = 255;

    private const byte Will = 251;

    private const byte Dont = 254;

    private readonly Lock gate = new();

    private readonly List<byte> unread = [.. Negotiation];

    private readonly List<byte> data = [];

    private TaskCompletionSource nextWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private CommandState state;

    private bool answered;

    private enum CommandState
    {
        Data,
        Command,
        Option,
    }

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint { get; init; }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        Task written;
        lock (gate)
        {
            if (unread.Count > 0 || answered)
            {
                int count = Math.Min(buffer.Length, unread.Count);
                CollectionsMarshal.AsSpan(unread)[..count].CopyTo(buffer.Span);
                unread.RemoveRange(0, count);
                return count;
            }

            written = nextWrite.Task;
        }

        await written.WaitAsync(cancellationToken);
        return await ReadAsync(buffer, cancellationToken);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        TaskCompletionSource written;
        lock (gate)
        {
            if (!answered)
            {
                ReadData(buffer.Span);
            }

            written = nextWrite;
            nextWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        written.TrySetResult();
        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // negtelnetserver.py's Negotiator.recv: keep reading until a read leaves data, then echo it stripped.
    private void ReadData(ReadOnlySpan<byte> received)
    {
        foreach (byte value in received)
        {
            state = NextState(value);
        }

        if (data.Count > 0)
        {
            answered = true;
            ReadOnlySpan<byte> echo = CollectionsMarshal.AsSpan(data);
            unread.AddRange(echo.Trim(" \t\n\r\v\f"u8));
        }
    }

    private CommandState NextState(byte value)
    {
        switch (state)
        {
            case CommandState.Data when value == InterpretAsCommand:
                return CommandState.Command;
            case CommandState.Data:
                data.Add(value);
                return CommandState.Data;
            case CommandState.Command when value is >= Will and <= Dont:
                return CommandState.Option;
            default:
                return CommandState.Data;
        }
    }
}
