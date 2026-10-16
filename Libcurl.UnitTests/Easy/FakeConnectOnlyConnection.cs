using Curl.Api.Codes;
using Curl.Api.EngineContracts.ConnectOnly;

namespace Libcurl.UnitTests.Easy;

/// <summary>
/// An <see cref="IConnectOnlyConnection"/> whose receive writes <see cref="Incoming"/> into the buffer and answers
/// <see cref="ReceiveResult"/> with its length, and whose send records the bytes and token and answers
/// <see cref="SendResult"/>. Both answer <see cref="CurlCode.UnsupportedProtocol"/> by default, as an engine with
/// no connect-only connection open does.
/// </summary>
internal sealed class FakeConnectOnlyConnection : IConnectOnlyConnection
{
    public CurlConnectOnlyResult ReceiveResult { get; init; } = new(CurlCode.UnsupportedProtocol, 0);

    public byte[] Incoming { get; init; } = [];

    public CurlConnectOnlyResult SendResult { get; init; } = new(CurlCode.UnsupportedProtocol, 0);

    public List<byte[]> Sent { get; } = [];

    public CancellationToken LastToken { get; private set; }

    public ValueTask<CurlConnectOnlyResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        LastToken = cancellationToken;
        Incoming.CopyTo(buffer);
        return ValueTask.FromResult(ReceiveResult);
    }

    public ValueTask<CurlConnectOnlyResult> SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        LastToken = cancellationToken;
        Sent.Add(buffer.ToArray());
        return ValueTask.FromResult(SendResult);
    }
}
