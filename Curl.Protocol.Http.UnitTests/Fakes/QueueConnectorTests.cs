using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// Pins <see cref="QueueConnector" />, the fake connector for transfers that connect more than once.
/// </summary>
[TestClass]
public sealed class QueueConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_SeveralConnects_ReturnsResultsInOrderAndRecordsTargets()
    {
        ScriptedConnection first = new([], 1);
        ScriptedConnection second = new([], 1);
        QueueConnector connector = QueueConnector.For(first, second);
        ConnectTarget a = new("a.example", 80, false);
        ConnectTarget b = new("b.example", 443, true);

        ConnectResult one = await connector.ConnectAsync(a, CancellationToken.None);
        ConnectResult two = await connector.ConnectAsync(b, CancellationToken.None);

        Assert.AreSame(first, one.Connection);
        Assert.AreSame(second, two.Connection);
        CollectionAssert.AreEqual(new[] { a, b }, connector.Targets);
    }

    [TestMethod]
    public async Task ConnectAsync_FailureScripted_ReturnsIt()
    {
        QueueConnector connector = new(ConnectResult.Failed(CurlExitCode.CouldntConnect, "refused"));

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("a.example", 80, false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
    }

    [TestMethod]
    public async Task ConnectAsync_NothingLeftScripted_Throws()
    {
        QueueConnector connector = new();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await connector.ConnectAsync(new ConnectTarget("a.example", 80, false), CancellationToken.None));
    }

    [TestMethod]
    public async Task ConnectAsync_TokenCancelled_ThrowsOperationCanceled()
    {
        QueueConnector connector = QueueConnector.For(new ScriptedConnection([], 1));
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await connector.ConnectAsync(new ConnectTarget("a.example", 80, false), cancelled.Token));
    }
}
