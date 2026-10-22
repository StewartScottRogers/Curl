using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>Pins that <see cref="SmallSendsConnector"/> wraps each TCP connection in a <see cref="SmallSendsConnection"/> and passes failures and QUIC through (BL-2832).</summary>
[TestClass]
public sealed class SmallSendsConnectorTests
{
    private static readonly ConnectTarget Target = new("127.0.0.1", 8990, false);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ConnectAsync_InnerConnects_WritesReachItInPieces()
    {
        SmallSendsConnectionTests.RecordingConnection server = new();
        SmallSendsConnector connector = new(new FixedConnector(ConnectResult.Connected(server)), firstWriteLimit: 4, everyWriteLimit: null);

        ConnectResult result = await connector.ConnectAsync(Target, TestContext.CancellationToken);
        await result.Connection!.WriteAsync(Encoding.Latin1.GetBytes("GET / HTTP/1.1"), TestContext.CancellationToken);

        Assert.IsInstanceOfType<SmallSendsConnection>(result.Connection);
        CollectionAssert.AreEqual(new[] { "GET ", "/ HT", "TP/1", ".1" }, server.Writes);
    }

    [TestMethod]
    public async Task ConnectAsync_InnerRefuses_ReturnsItsResult()
    {
        ConnectResult refused = ConnectResult.Refused("refused");
        SmallSendsConnector connector = new(new FixedConnector(refused), firstWriteLimit: null, everyWriteLimit: 1);

        ConnectResult result = await connector.ConnectAsync(Target, TestContext.CancellationToken);

        Assert.AreSame(refused, result);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_IsTheInnerConnectors()
    {
        SmallSendsConnector connector = new(new FixedConnector(ConnectResult.Refused("refused")), firstWriteLimit: 1, everyWriteLimit: 1);

        MultiplexedConnectResult result = await connector.ConnectMultiplexedAsync(Target, TestContext.CancellationToken);

        Assert.AreEqual(FixedConnector.MultiplexedFailure, result.ErrorMessage);
    }

    private sealed class FixedConnector(ConnectResult result) : IConnector
    {
        public const string MultiplexedFailure = "the fixed connector's QUIC";

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) => ValueTask.FromResult(result);

        public ValueTask<MultiplexedConnectResult> ConnectMultiplexedAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(MultiplexedConnectResult.Failed(CurlExitCode.CouldntConnect, MultiplexedFailure));
    }
}
