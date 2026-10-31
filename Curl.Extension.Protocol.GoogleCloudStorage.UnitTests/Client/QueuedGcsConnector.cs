using System.Text;
using Curl.Extension.Protocol.GoogleCloudStorage.Http;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.GoogleCloudStorage.Client;

/// <summary>A fake <see cref="IConnector"/> that answers each connection with the next recorded response and keeps what each was sent.</summary>
internal sealed class QueuedGcsConnector(params string[] responses) : IConnector
{
    private readonly Queue<string> pending = new(responses);

    /// <summary>The response that makes the connection be refused instead.</summary>
    public const string Refuse = "refuse";

    public List<RecordedConnection> Connections { get; } = [];

    public List<ConnectTarget> Targets { get; } = [];

    /// <summary>Gives a <c>200 OK</c> response with <paramref name="body"/> and its <c>Content-Length</c>.</summary>
    public static string Ok(string body) =>
        $"HTTP/1.1 200 OK\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\n\r\n{body}";

    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Targets.Add(target);
        string response = pending.Dequeue();
        if (response == Refuse)
        {
            return ValueTask.FromResult(ConnectResult.Refused("Failed to connect to emulator port 4443"));
        }

        RecordedConnection connection = new(response);
        Connections.Add(connection);
        return ValueTask.FromResult(ConnectResult.Connected(connection));
    }
}
