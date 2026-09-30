using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// A fake connector that answers each connect with the next scripted connection: its local
/// end point on the <see cref="ConnectResult" />, its remote end point on the connection, and
/// the bytes it reads back one chunk per read, then end of stream. A connect past the last
/// scripted connection fails with exit 7.
/// </summary>
/// <param name="connections">The connections, in the order they are opened.</param>
internal sealed class EndPointScriptedConnector(params EndPointScriptedConnector.Script[] connections) : IConnector
{
    private readonly Queue<Script> pending = new(connections);

    /// <summary>Gets every target connected to, in order.</summary>
    public List<ConnectTarget> Targets { get; } = [];

    /// <inheritdoc />
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Targets.Add(target);
        if (!pending.TryDequeue(out Script? script))
        {
            return ValueTask.FromResult(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect"));
        }

        return ValueTask.FromResult(
            ConnectResult.Connected(new ScriptedConnection(script.Remote, new Queue<byte[]>(script.Reads)), null, script.Local));
    }

    /// <summary>One scripted connection.</summary>
    /// <param name="Local">The local end point the connect reports, or <see langword="null" /> for none.</param>
    /// <param name="Remote">The connection's remote end point: an IP end point, or a Unix domain socket.</param>
    /// <param name="Reads">The chunks the connection reads, in order.</param>
    internal sealed record Script(IPEndPoint? Local, EndPoint Remote, params byte[][] Reads);

    private sealed class ScriptedConnection(EndPoint remote, Queue<byte[]> reads) : IConnection
    {
        private byte[] remainder = [];

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => remote;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (remainder.Length == 0 && !reads.TryDequeue(out remainder!))
            {
                remainder = [];
                return ValueTask.FromResult(0);
            }

            int count = Math.Min(remainder.Length, buffer.Length);
            remainder.AsSpan(0, count).CopyTo(buffer.Span);
            remainder = remainder[count..];

            return ValueTask.FromResult(count);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
