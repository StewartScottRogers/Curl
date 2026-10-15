using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Http2Server;

/// <summary>
/// A backend that records each request's bytes and answers every connection with the same reply,
/// or refuses every connection when the reply is <see langword="null"/>.
/// </summary>
internal sealed class RecordingBackendConnector(string? reply) : IConnector
{
    private readonly List<MemoryStream> requests = [];

    internal IReadOnlyList<string> Requests => [.. requests.Select(r => Encoding.Latin1.GetString(r.ToArray()))];

    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        if (reply is null)
        {
            return ValueTask.FromResult(ConnectResult.Failed(CurlExitCode.CouldntConnect, "refused"));
        }

        var request = new MemoryStream();
        requests.Add(request);
        return ValueTask.FromResult(ConnectResult.Connected(new Connection(request, Encoding.Latin1.GetBytes(reply))));
    }

    private sealed class Connection(MemoryStream request, byte[] reply) : IConnection
    {
        private int replied;

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int taken = Math.Min(Math.Min(buffer.Length, reply.Length - replied), 7);
            reply.AsMemory(replied, taken).CopyTo(buffer);
            replied += taken;
            return ValueTask.FromResult(taken);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            request.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
