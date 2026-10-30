using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.ObliviousDoh.Http;

/// <summary>A connector that opens a new <see cref="ExchangeConnection" /> per connect, or fails when told to.</summary>
internal sealed class FakeConnector(Func<byte[], byte[]> respond) : IConnector
{
    public bool Fails { get; init; }

    public bool ThrowOnWrite { get; init; }

    public List<ConnectTarget> Targets { get; } = [];

    public List<ExchangeConnection> Connections { get; } = [];

    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Targets.Add(target);
        if (Fails)
        {
            return ValueTask.FromResult(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect"));
        }

        var connection = new ExchangeConnection(respond) { ThrowOnWrite = ThrowOnWrite };
        Connections.Add(connection);
        return ValueTask.FromResult(ConnectResult.Connected(connection));
    }

    internal static byte[] Ok(byte[] body) => [.. Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\n\r\n"), .. body];

    internal static byte[] Status(int code) => Encoding.ASCII.GetBytes($"HTTP/1.1 {code} Nope\r\nContent-Length: 0\r\n\r\n");

    // The body of a request: everything after the empty line.
    internal static byte[] BodyOf(byte[] request) => request[(request.AsSpan().IndexOf("\r\n\r\n"u8) + 4)..];
}
