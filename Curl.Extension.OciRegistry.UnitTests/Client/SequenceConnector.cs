using Curl.Extension.OciRegistry.Http;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OciRegistry.Client;

/// <summary>A fake <see cref="IConnector"/> that hands out one <see cref="RecordedConnection"/> per connect, in order, and records each target.</summary>
internal sealed class SequenceConnector(params RecordedConnection[] connections) : IConnector
{
    private int next;

    public List<ConnectTarget> Targets { get; } = [];

    public RecordedConnection this[int index] => connections[index];

    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Targets.Add(target);
        return ValueTask.FromResult(ConnectResult.Connected(connections[next++]));
    }

    public static RecordedConnection Respond(string head, string body = "") =>
        new($"HTTP/1.1 {head}\r\nContent-Length: {System.Text.Encoding.UTF8.GetByteCount(body)}\r\n\r\n{body}");
}
