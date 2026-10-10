using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>Pins that <see cref="NoListenPortConnector"/> refuses <c>%NOLISTENPORT</c> and port 1 and passes every other port on.</summary>
[TestClass]
public sealed class NoListenPortConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_NoListenPort_IsRefusedWithExitCode7AndCurlsMessage()
    {
        NoListenPortConnector connector = new(new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System));

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", NoListenPortConnector.NoListenPort, false), CancellationToken.None);

        Assert.IsNull(result.Connection);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsTrue(result.IsConnectionRefused);
        Assert.AreEqual("Failed to connect to 127.0.0.1:47 after 0 ms: Could not connect to server", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_PortOne_IsRefusedWithExitCode7AndCurlsMessage()
    {
        NoListenPortConnector connector = new(new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System));

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", NoListenPortConnector.PortOne, false), CancellationToken.None);

        Assert.IsNull(result.Connection);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsTrue(result.IsConnectionRefused);
        Assert.AreEqual("Failed to connect to 127.0.0.1:1 after 0 ms: Could not connect to server", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_OtherPort_ReachesTheWrappedServer()
    {
        NoListenPortConnector connector = new(new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System));

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 8990, false), CancellationToken.None);

        Assert.IsNotNull(result.Connection);
    }
}

