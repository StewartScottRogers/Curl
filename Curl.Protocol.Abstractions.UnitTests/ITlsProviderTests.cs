using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the default member of <see cref="ITlsProvider" />, which lets a provider that reports
/// nothing leave out the overload that takes the transfer's events (BL-1058).
/// </summary>
[TestClass]
public sealed class ITlsProviderTests
{
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithEventsNotOverridden_RunsTheThreeArgumentHandshake()
    {
        var provider = new HostRecordingTlsProvider();
        var plaintext = new UnusedConnection();

        ConnectResult secured = await ((ITlsProvider)provider)
            .AuthenticateAsClientAsync(plaintext, "mail.example", new StubTransferEvents(), CancellationToken.None);

        Assert.AreSame(plaintext, secured.Connection);
        Assert.AreEqual("mail.example", provider.TargetHost);
    }

    private sealed class HostRecordingTlsProvider : ITlsProvider
    {
        public string? TargetHost { get; private set; }

        public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken)
        {
            TargetHost = targetHost;
            return ValueTask.FromResult(ConnectResult.Connected(plaintext));
        }
    }

    private sealed class UnusedConnection : IConnection
    {
        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask FlushAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
