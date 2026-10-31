using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="IConnector.HttpOverTlsApplicationProtocols" />'s default, which a connector that does not say
/// what it offers through ALPN keeps (ADR-1066).
/// </summary>
[TestClass]
public sealed class IConnectorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void HttpOverTlsApplicationProtocols_NotOverridden_IsHttp11Alone()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("connector", "one that keeps the default");

        var protocols = ((IConnector)new DefaultConnector()).HttpOverTlsApplicationProtocols;

        diagnostics.Act("protocols", string.Join(",", protocols));
        diagnostics.Assert("protocols", "http/1.1", string.Join(",", protocols));
        CollectionAssert.AreEqual(new[] { "http/1.1" }, protocols.ToArray());
    }

    private sealed class DefaultConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
