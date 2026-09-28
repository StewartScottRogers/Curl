using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the two ways a <see cref="ListenResult" /> may be built, and the invariant that a
/// pending connection is present exactly when the exit code is <see cref="CurlExitCode.Ok" />.
/// </summary>
[TestClass]
public sealed class ListenResultTests
{
    [TestMethod]
    public void Listening_WithNullPendingConnection_ThrowsArgumentNullException()
    {
        IPendingConnection? pendingConnection = null;

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => ListenResult.Listening(pendingConnection!));

        Assert.AreEqual("pendingConnection", exception.ParamName);
    }

    [TestMethod]
    public void Listening_WithPendingConnection_ExposesItWithOkAndNoMessage()
    {
        var pendingConnection = new UnusedPendingConnection();

        var result = ListenResult.Listening(pendingConnection);

        Assert.AreSame(pendingConnection, result.PendingConnection);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public void Failed_WithOk_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ListenResult.Failed(CurlExitCode.Ok, "unused"));

        Assert.AreEqual("exitCode", exception.ParamName);
    }

    [TestMethod]
    public void Failed_WithCouldntConnect_ExposesCodeAndMessageWithNoPendingConnection()
    {
        var result = ListenResult.Failed(CurlExitCode.CouldntConnect, "bind failed");

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
