using System.Net;
using System.Net.Sockets;

using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="StreamConnection" /> over a <see cref="MemoryStream" />, so every
/// member is covered without a socket.
/// </summary>
[TestClass]
public sealed class StreamConnectionTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Constructor_WithNullStream_ThrowsArgumentNullException()
    {
        Diagnostics.Arrange("stream", null);

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new StreamConnection(null!, null));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("parameter name", "stream", exception.ParamName);

        Assert.AreEqual("stream", exception.ParamName);
    }

    [TestMethod]
    public async Task Properties_ReportPlaintextAndTheGivenEndPoints()
    {
        var endPoint = new IPEndPoint(IPAddress.Loopback, 80);
        var localEndPoint = new IPEndPoint(IPAddress.Loopback, 50000);
        await using var connection = new StreamConnection(new MemoryStream(), endPoint, localEndPoint);

        Diagnostics.Arrange("remote end point", endPoint);
        Diagnostics.Arrange("local end point", localEndPoint);
        Diagnostics.Act("is secure", connection.IsSecure);
        Diagnostics.Act("remote, local", $"{connection.RemoteEndPoint}, {connection.LocalEndPoint}");
        Diagnostics.Assert("is secure", false, connection.IsSecure);
        Diagnostics.Assert("remote end point", endPoint, connection.RemoteEndPoint);
        Diagnostics.Assert("local end point", localEndPoint, connection.LocalEndPoint);

        Assert.IsFalse(connection.IsSecure);
        Assert.AreSame(endPoint, connection.RemoteEndPoint);
        Assert.AreSame(localEndPoint, connection.LocalEndPoint);
    }

    [TestMethod]
    public async Task LocalEndPoint_WhenNotGiven_IsNull()
    {
        Diagnostics.Arrange("local end point", null);

        await using var connection = new StreamConnection(new MemoryStream(), null);

        Diagnostics.Act("local end point", connection.LocalEndPoint);
        Diagnostics.Assert("local end point", null, connection.LocalEndPoint);

        Assert.IsNull(connection.LocalEndPoint);
    }

    [TestMethod]
    public async Task HasPeerClosed_OverAStreamWithNoSocket_IsFalse()
    {
        Diagnostics.Arrange("stream", "memory stream, no socket to ask");

        await using var connection = new StreamConnection(new MemoryStream(), null);

        Diagnostics.Act("has peer closed", connection.HasPeerClosed);
        Diagnostics.Assert("has peer closed", false, connection.HasPeerClosed);

        Assert.IsFalse(connection.HasPeerClosed);
    }

    [TestMethod]
    public async Task ReadAsync_ReturnsTheStreamsBytesThenZero()
    {
        await using var connection = new StreamConnection(new MemoryStream([1, 2, 3]), null);
        var buffer = new byte[8];

        Diagnostics.Arrange("stream bytes", "01 02 03");
        Diagnostics.Arrange("buffer length", buffer.Length);

        var read = await connection.ReadAsync(buffer, CancellationToken.None);
        var afterEnd = await connection.ReadAsync(buffer, CancellationToken.None);

        Diagnostics.Act("first read count", read);
        Diagnostics.Act("second read count", afterEnd);
        Diagnostics.Bytes("buffer[..3]", buffer.AsSpan(0, 3));
        Diagnostics.Assert("first read count", 3, read);
        Diagnostics.Assert("second read count", 0, afterEnd);

        Assert.AreEqual(3, read);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, buffer[..3]);
        Assert.AreEqual(0, afterEnd);
    }

    [TestMethod]
    public async Task WriteAsyncAndFlushAsync_PutTheBytesOnTheStream()
    {
        var stream = new MemoryStream();
        var connection = new StreamConnection(stream, null);

        Diagnostics.Arrange("written bytes", "04 05");

        await connection.WriteAsync(new byte[] { 4, 5 }, CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);

        Diagnostics.Act("stream length", stream.Length);
        Diagnostics.Bytes("stream", stream.ToArray());
        Diagnostics.Assert("stream length", 2L, stream.Length);

        CollectionAssert.AreEqual(new byte[] { 4, 5 }, stream.ToArray());
        await connection.DisposeAsync();
    }

    [TestMethod]
    public async Task DisposeAsync_DisposesTheStream()
    {
        var stream = new MemoryStream();
        var connection = new StreamConnection(stream, null);

        Diagnostics.Arrange("stream can read", stream.CanRead);

        await connection.DisposeAsync();

        Diagnostics.Act("stream can read", stream.CanRead);
        Diagnostics.Assert("stream can read", false, stream.CanRead);

        Assert.IsFalse(stream.CanRead);
    }

    // BL-1450: Windows answers a receive issued a few milliseconds after a peer's RST with
    // WSAECONNABORTED; curl's recv sees WSAECONNRESET, so the read reports the reset.
    [TestMethod]
    public async Task ReadAsync_WhenTheReadIsAbortedAndAbortsAreReportedAsResets_FailsWithConnectionReset()
    {
        var aborted = new IOException("Unable to read data.", new SocketException((int)SocketError.ConnectionAborted));
        await using var connection = new StreamConnection(new ReadFailingStream(aborted), null) { ReportsAbortedReadAsReset = true };

        Diagnostics.Arrange("read failure, reports abort as reset", "ConnectionAborted, true");

        var failure = await Assert.ThrowsExactlyAsync<IOException>(() => connection.ReadAsync(new byte[8], CancellationToken.None).AsTask());

        var socketError = ((SocketException)failure.InnerException!).SocketErrorCode;
        Diagnostics.Act("exception", failure.GetType().Name);
        Diagnostics.Act("socket error", socketError);
        Diagnostics.Assert("socket error", SocketError.ConnectionReset, socketError);
        Diagnostics.Assert("message", "Unable to read data.", failure.Message);

        Assert.AreEqual(SocketError.ConnectionReset, ((SocketException)failure.InnerException!).SocketErrorCode);
        Assert.AreEqual("Unable to read data.", failure.Message);
    }

    [TestMethod]
    public async Task ReadAsync_WhenTheReadIsAbortedAndAbortsAreNotReportedAsResets_FailsWithTheAbort()
    {
        var aborted = new IOException("Unable to read data.", new SocketException((int)SocketError.ConnectionAborted));
        await using var connection = new StreamConnection(new ReadFailingStream(aborted), null) { ReportsAbortedReadAsReset = false };

        Diagnostics.Arrange("read failure, reports abort as reset", "ConnectionAborted, false");

        var failure = await Assert.ThrowsExactlyAsync<IOException>(() => connection.ReadAsync(new byte[8], CancellationToken.None).AsTask());

        Diagnostics.Act("exception", failure.GetType().Name);
        Diagnostics.Assert("failure is the abort", true, ReferenceEquals(aborted, failure));

        Assert.AreSame(aborted, failure);
    }

    [TestMethod]
    public async Task ReadAsync_WhenTheFirstReceiveIsReset_FailsWithConnectionReset()
    {
        var reset = new IOException("Unable to read data.", new SocketException((int)SocketError.ConnectionReset));
        await using var connection = new StreamConnection(new ReadFailingStream(reset), null) { ReportsAbortedReadAsReset = true };

        Diagnostics.Arrange("read failure, reports abort as reset", "ConnectionReset, true");

        var failure = await Assert.ThrowsExactlyAsync<IOException>(() => connection.ReadAsync(new byte[8], CancellationToken.None).AsTask());

        var socketError = ((SocketException)failure.InnerException!).SocketErrorCode;
        Diagnostics.Act("exception", failure.GetType().Name);
        Diagnostics.Act("socket error", socketError);
        Diagnostics.Assert("failure is the reset", true, ReferenceEquals(reset, failure));
        Diagnostics.Assert("socket error", SocketError.ConnectionReset, socketError);

        Assert.AreSame(reset, failure);
        Assert.AreEqual(SocketError.ConnectionReset, ((SocketException)failure.InnerException!).SocketErrorCode);
    }

    [TestMethod]
    public async Task ReadAsync_WhenTheFailureIsNoSocketError_FailsWithItUnchanged()
    {
        var other = new IOException("Disk gone.");
        await using var connection = new StreamConnection(new ReadFailingStream(other), null) { ReportsAbortedReadAsReset = true };

        Diagnostics.Arrange("read failure, reports abort as reset", "IOException without a socket error, true");

        var failure = await Assert.ThrowsExactlyAsync<IOException>(() => connection.ReadAsync(new byte[8], CancellationToken.None).AsTask());

        Diagnostics.Act("exception", failure.GetType().Name);
        Diagnostics.Assert("failure is unchanged", true, ReferenceEquals(other, failure));

        Assert.AreSame(other, failure);
    }

    /// <summary>A stream whose every read fails with the given exception.</summary>
    private sealed class ReadFailingStream(IOException failure) : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(failure);
    }
}
