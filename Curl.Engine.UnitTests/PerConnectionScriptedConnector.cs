using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// A connector whose every connection replays its own script: the first connect gets the first script, the
/// next connect the next, each read handing over as much of the script as fits, then the server closing.
/// Unlike <see cref="ScriptedConnector" />, a data connection never reads its control connection's replies.
/// What every connection writes is recorded, in order, so no socket is ever opened.
/// </summary>
/// <param name="scripts">Each connection's server bytes, as Latin-1 text, in connect order.</param>
internal sealed class PerConnectionScriptedConnector(params string[] scripts) : IConnector
{
    private readonly Queue<string> pendingScripts = new(scripts);

    private readonly StringBuilder written = new();

    /// <summary>Gets everything every connection wrote, as Latin-1 text.</summary>
    public string Written => written.ToString();

    /// <summary>Connects to the next script, or to an empty one when none is left.</summary>
    /// <param name="target">Where to connect; ignored.</param>
    /// <param name="cancellationToken">Not observed.</param>
    /// <returns>The connection.</returns>
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        ValueTask.FromResult(ConnectResult.Connected(new ScriptConnection(Encoding.Latin1.GetBytes(pendingScripts.TryDequeue(out string? script) ? script : string.Empty), written)));

    private sealed class ScriptConnection(byte[] script, StringBuilder written) : IConnection
    {
        private int position;

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int count = Math.Min(script.Length - position, buffer.Length);
            script.AsSpan(position, count).CopyTo(buffer.Span);
            position += count;
            return ValueTask.FromResult(count);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            written.Append(Encoding.Latin1.GetString(buffer.Span));
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
