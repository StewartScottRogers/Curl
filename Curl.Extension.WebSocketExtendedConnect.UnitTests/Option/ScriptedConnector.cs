using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WebSocketExtendedConnect.Option;

/// <summary>A connector that gives scripted results and records the targets asked for.</summary>
internal sealed class ScriptedConnector(ConnectResult? connected = null, MultiplexedConnectResult? multiplexed = null) : IConnector
{
    public List<ConnectTarget> Targets { get; } = [];

    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Targets.Add(target);
        return ValueTask.FromResult(connected!);
    }

    public ValueTask<MultiplexedConnectResult> ConnectMultiplexedAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Targets.Add(target);
        return ValueTask.FromResult(multiplexed!);
    }
}

/// <summary>An <see cref="IConnection" /> over a scripted stream, recording flushes and disposal.</summary>
internal sealed class StreamConnection(Stream stream) : IConnection
{
    public bool IsSecure => true;

    public EndPoint? RemoteEndPoint => null;

    public int Flushes { get; private set; }

    public bool IsDisposed { get; private set; }

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => stream.ReadAsync(buffer, cancellationToken);

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => stream.WriteAsync(buffer, cancellationToken);

    public ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        Flushes++;
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
