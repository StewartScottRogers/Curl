using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.ConnectOnly;

namespace Curl.Conformance.LibTest.UnitTests;

/// <summary>
/// An <see cref="IConnectOnlyConnection"/> answering each send and receive from its queue: a send takes
/// <c>(code, count)</c>, a receive <c>(code, text)</c>, the text copied into the buffer when the code is
/// <see cref="CurlCode.Ok"/>. An empty queue answers <see cref="CurlCode.Ok"/>: everything sent, nothing received.
/// </summary>
internal sealed class FakeConnectOnlyConnection : IConnectOnlyConnection
{
    public Queue<(CurlCode Code, int Count)> Sends { get; } = new();

    public Queue<(CurlCode Code, string Text)> Receives { get; } = new();

    public StringBuilder Sent { get; } = new();

    public ValueTask<CurlConnectOnlyResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var (code, text) = Receives.Count > 0 ? Receives.Dequeue() : (CurlCode.Ok, string.Empty);
        int count = code == CurlCode.Ok ? Encoding.ASCII.GetBytes(text, buffer.Span) : 0;
        return ValueTask.FromResult(new CurlConnectOnlyResult(code, count));
    }

    public ValueTask<CurlConnectOnlyResult> SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        var (code, count) = Sends.Count > 0 ? Sends.Dequeue() : (CurlCode.Ok, buffer.Length);
        Sent.Append(Encoding.ASCII.GetString(buffer.Span[..(code == CurlCode.Ok ? count : 0)]));
        return ValueTask.FromResult(new CurlConnectOnlyResult(code, count));
    }
}
