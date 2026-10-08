using System.Net;
using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the two ways a <see cref="ListenResult" /> may be built, and the invariant that a
/// pending connection is present exactly when the exit code is <see cref="CurlExitCode.Ok" />.
/// </summary>
[TestClass]
public sealed class ListenResultTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Listening_WithNullPendingConnection_ThrowsArgumentNullException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        IPendingConnection? pendingConnection = null;
        diagnostics.Arrange("pending connection", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => ListenResult.Listening(pendingConnection!));

        diagnostics.Act("exception parameter", exception.ParamName);
        diagnostics.Assert("parameter name", "pendingConnection", exception.ParamName);
        Assert.AreEqual("pendingConnection", exception.ParamName);
    }

    [TestMethod]
    public void Listening_WithPendingConnection_ExposesItWithOkAndNoMessage()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var pendingConnection = new UnusedPendingConnection();
        diagnostics.Arrange("pending connection", nameof(UnusedPendingConnection));

        var result = ListenResult.Listening(pendingConnection);

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("error message", result.ErrorMessage ?? "null");
        diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(pendingConnection, result.PendingConnection);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public void Failed_WithOk_ThrowsArgumentOutOfRangeException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exit code", CurlExitCode.Ok);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ListenResult.Failed(CurlExitCode.Ok, "unused"));

        diagnostics.Act("exception parameter", exception.ParamName);
        diagnostics.Assert("parameter name", "exitCode", exception.ParamName);
        Assert.AreEqual("exitCode", exception.ParamName);
    }

    [TestMethod]
    public void Failed_WithCouldntConnect_ExposesCodeAndMessageWithNoPendingConnection()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exit code", CurlExitCode.CouldntConnect);

        var result = ListenResult.Failed(CurlExitCode.CouldntConnect, "bind failed");

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Assert("error message", "bind failed", result.ErrorMessage);
        Assert.IsNull(result.PendingConnection);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("bind failed", result.ErrorMessage);
    }

    private sealed class UnusedPendingConnection : IPendingConnection
    {
        public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 0);

        public ValueTask<ConnectResult> AcceptAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
