using System.Text;
using Curl.Extension.Protocol.AzureStorage.Http;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.AzureStorage.Client;

/// <summary>A fake <see cref="IConnector"/> that answers each connection with the next recorded response and keeps what each was sent.</summary>
internal sealed class QueuedAzureStorageConnector(params string[] responses) : IConnector
{
    private readonly Queue<string> pending = new(responses);

    /// <summary>The response that makes the connection be refused instead.</summary>
    public const string Refuse = "refuse";

    public List<RecordedConnection> Connections { get; } = [];

    public List<ConnectTarget> Targets { get; } = [];

    /// <summary>Gives a response with <paramref name="body"/> and its <c>Content-Length</c>.</summary>
    public static string Reply(string body, string headers = "", string status = "200 OK") =>
        $"HTTP/1.1 {status}\r\n{headers}Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n\r\n{body}";

    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Targets.Add(target);
        string response = pending.Dequeue();
        if (response == Refuse)
        {
            return ValueTask.FromResult(ConnectResult.Refused("Failed to connect to account.blob.core.windows.net port 443"));
        }

        RecordedConnection connection = new(response);
        Connections.Add(connection);
        return ValueTask.FromResult(ConnectResult.Connected(connection));
    }
}
