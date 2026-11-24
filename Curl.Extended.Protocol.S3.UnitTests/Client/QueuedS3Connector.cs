using System.Text;
using Curl.Extended.Protocol.S3.Http;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.S3.Client;

/// <summary>A fake <see cref="IConnector"/> that answers each connection with the next recorded response and keeps what each was sent.</summary>
internal sealed class QueuedS3Connector(params string[] responses) : IConnector
{
    private readonly Queue<string> pending = new(responses);

    /// <summary>The response that makes the connection be refused instead.</summary>
    public const string Refuse = "refuse";

    /// <summary>Prefix of a response whose connection throws a reset <see cref="IOException"/> once the recorded bytes are read.</summary>
    public const string ResetAfter = "reset-after:";

    public List<RecordedConnection> Connections { get; } = [];

    public List<ConnectTarget> Targets { get; } = [];

    /// <summary>Gives a <c>200 OK</c> response with <paramref name="body"/> and its <c>Content-Length</c>.</summary>
    public static string Ok(string body, string headers = "") =>
        $"HTTP/1.1 200 OK\r\n{headers}Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n\r\n{body}";

    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Targets.Add(target);
        string response = pending.Dequeue();
        if (response == Refuse)
        {
            return ValueTask.FromResult(ConnectResult.Refused("Failed to connect to minio port 9000"));
        }

        bool reset = response.StartsWith(ResetAfter, StringComparison.Ordinal);
        RecordedConnection connection = new(reset ? response[ResetAfter.Length..] : response, resetAtEnd: reset);
        Connections.Add(connection);
        return ValueTask.FromResult(ConnectResult.Connected(connection));
    }
}
