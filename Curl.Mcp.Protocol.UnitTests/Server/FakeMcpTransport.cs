using System.Text;
using System.Threading.Channels;
using Curl.Mcp.Protocol.JsonRpc;
using Curl.Mcp.Protocol.Transport;

namespace Curl.Mcp.Protocol.Server;

/// <summary>An in-memory transport: tests send lines in, end it, and read the responses the server wrote.</summary>
internal sealed class FakeMcpTransport : IMcpTransport
{
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(10);

    private readonly Channel<JsonRpcIncoming> _incoming = Channel.CreateUnbounded<JsonRpcIncoming>();

    private readonly Channel<string> _written = Channel.CreateUnbounded<string>();

    /// <summary>Gets or sets an exception the next read throws instead of returning a message.</summary>
    public Exception? ReadFailure { get; set; }

    /// <summary>Gets or sets an exception every write throws instead of recording the response.</summary>
    public Exception? WriteFailure { get; set; }

    public void Send(JsonRpcIncoming message) => _incoming.Writer.TryWrite(message);

    public void Send(string line) => _incoming.Writer.TryWrite(new JsonRpcMessageParser().Parse(Encoding.UTF8.GetBytes(line)));

    public void End() => _incoming.Writer.TryComplete();

    public async Task<string> NextResponseAsync() => await _written.Reader.ReadAsync().AsTask().WaitAsync(ResponseTimeout);

    public List<string> RemainingResponses()
    {
        List<string> responses = [];
        while (_written.Reader.TryRead(out string? response))
        {
            responses.Add(response);
        }

        return responses;
    }

    public async ValueTask<JsonRpcIncoming?> ReadMessageAsync(CancellationToken cancellationToken)
    {
        if (ReadFailure is not null)
        {
            throw ReadFailure;
        }

        return await _incoming.Reader.WaitToReadAsync(cancellationToken) && _incoming.Reader.TryRead(out JsonRpcIncoming? message) ? message : null;
    }

    public ValueTask WriteMessageAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken)
    {
        if (WriteFailure is not null)
        {
            throw WriteFailure;
        }

        _written.Writer.TryWrite(Encoding.UTF8.GetString(message.Span));
        return ValueTask.CompletedTask;
    }
}
