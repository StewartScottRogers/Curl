using System.Net;
using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the default member of <see cref="ITlsProvider" />, which lets a provider that reports
/// nothing leave out the overload that takes the transfer's events (BL-1058).
/// </summary>
[TestClass]
public sealed class ITlsProviderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithEventsNotOverridden_RunsTheThreeArgumentHandshake()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var provider = new HostRecordingTlsProvider();
        var plaintext = new UnusedConnection();
        diagnostics.Arrange("target host", "mail.example");

        ConnectResult secured = await ((ITlsProvider)provider)
            .AuthenticateAsClientAsync(plaintext, "mail.example", new StubTransferEvents(), CancellationToken.None);

        diagnostics.Act("provider target host", provider.TargetHost);
        diagnostics.Act("connection is the plaintext one", ReferenceEquals(plaintext, secured.Connection));
        diagnostics.Assert("connection", plaintext, secured.Connection);
        diagnostics.Diff("target host", "mail.example", provider.TargetHost ?? string.Empty);
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
