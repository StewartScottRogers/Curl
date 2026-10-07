using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// Pins <see cref="QueueConnector" />, the fake connector for transfers that connect more than once.
/// </summary>
[TestClass]
public sealed class QueueConnectorTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ConnectAsync_SeveralConnects_ReturnsResultsInOrderAndRecordsTargets()
    {
        ScriptedConnection first = new([], 1);
        ScriptedConnection second = new([], 1);
        QueueConnector connector = QueueConnector.For(first, second);
        ConnectTarget a = new("a.example", 80, false);
        ConnectTarget b = new("b.example", 443, true);
        Diagnostics.Arrange("targets", $"{a}; {b}");

        ConnectResult one = await connector.ConnectAsync(a, CancellationToken.None);
        ConnectResult two = await connector.ConnectAsync(b, CancellationToken.None);

        Diagnostics.Act("first connection is the first scripted", ReferenceEquals(first, one.Connection));
        Diagnostics.Act("second connection is the second scripted", ReferenceEquals(second, two.Connection));
        Diagnostics.Assert("recorded targets", 2, connector.Targets.Count);
        Assert.AreSame(first, one.Connection);
        Assert.AreSame(second, two.Connection);
        CollectionAssert.AreEqual(new[] { a, b }, connector.Targets);
    }

    [TestMethod]
    public async Task ConnectAsync_FailureScripted_ReturnsIt()
    {
        QueueConnector connector = new(ConnectResult.Failed(CurlExitCode.CouldntConnect, "refused"));
        Diagnostics.Arrange("scripted result", "CouldntConnect: refused");

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("a.example", 80, false), CancellationToken.None);

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
    }

    [TestMethod]
    public async Task ConnectAsync_NothingLeftScripted_Throws()
    {
        QueueConnector connector = new();
        Diagnostics.Arrange("scripted results", "none");

        InvalidOperationException thrown = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await connector.ConnectAsync(new ConnectTarget("a.example", 80, false), CancellationToken.None));

        Diagnostics.Act("exception", thrown.Message);
        Diagnostics.Assert("exception", nameof(InvalidOperationException), thrown.GetType().Name);
    }

    [TestMethod]
    public async Task ConnectAsync_TokenCancelled_ThrowsOperationCanceled()
    {
        QueueConnector connector = QueueConnector.For(new ScriptedConnection([], 1));
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();
        Diagnostics.Arrange("token", "cancelled before the connect");

        OperationCanceledException thrown = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await connector.ConnectAsync(new ConnectTarget("a.example", 80, false), cancelled.Token));

        Diagnostics.Act("exception", thrown.GetType().Name);
        Diagnostics.Assert("exception", nameof(OperationCanceledException), thrown.GetType().Name);
    }
}
