using System.Net;
using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="StreamConnection" /> over a <see cref="MemoryStream" />, so every
/// member is covered without a socket.
/// </summary>
[TestClass]
public sealed class StreamConnectionTests
{
    [TestMethod]
    public void Constructor_WithNullStream_ThrowsArgumentNullException()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new StreamConnection(null!, null));

        Assert.AreEqual("stream", exception.ParamName);
    }

    [TestMethod]
    public async Task Properties_ReportPlaintextAndTheGivenEndPoints()
    {
        var endPoint = new IPEndPoint(IPAddress.Loopback, 80);
        var localEndPoint = new IPEndPoint(IPAddress.Loopback, 50000);
        await using var connection = new StreamConnection(new MemoryStream(), endPoint, localEndPoint);

        Assert.IsFalse(connection.IsSecure);
        Assert.AreSame(endPoint, connection.RemoteEndPoint);
        Assert.AreSame(localEndPoint, connection.LocalEndPoint);
    }

    [TestMethod]
    public async Task LocalEndPoint_WhenNotGiven_IsNull()
    {
        await using var connection = new StreamConnection(new MemoryStream(), null);

        Assert.IsNull(connection.LocalEndPoint);
    }

    [TestMethod]
    public async Task ReadAsync_ReturnsTheStreamsBytesThenZero()
    {
        await using var connection = new StreamConnection(new MemoryStream([1, 2, 3]), null);
        var buffer = new byte[8];

        var read = await connection.ReadAsync(buffer, CancellationToken.None);
        var afterEnd = await connection.ReadAsync(buffer, CancellationToken.None);

        Assert.AreEqual(3, read);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, buffer[..3]);
        Assert.AreEqual(0, afterEnd);
    }

    [TestMethod]
    public async Task WriteAsyncAndFlushAsync_PutTheBytesOnTheStream()
    {
        var stream = new MemoryStream();
        var connection = new StreamConnection(stream, null);

        await connection.WriteAsync(new byte[] { 4, 5 }, CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 4, 5 }, stream.ToArray());
        await connection.DisposeAsync();
    }

    [TestMethod]
    public async Task DisposeAsync_DisposesTheStream()
    {
        var stream = new MemoryStream();
        var connection = new StreamConnection(stream, null);

        await connection.DisposeAsync();

        Assert.IsFalse(stream.CanRead);
    }

    // BL-1450: Windows answers a receive issued a few milliseconds after a peer's RST with
    // WSAECONNABORTED; curl's recv sees WSAECONNRESET, so the read reports the reset.
    [TestMethod]
    public async Task ReadAsync_WhenTheReadIsAbortedAndAbortsAreReportedAsResets_FailsWithConnectionReset()
    {
        var aborted = new IOException("Unable to read data.", new SocketException((int)SocketError.ConnectionAborted));
        await using var connection = new StreamConnection(new ReadFailingStream(aborted), null) { ReportsAbortedReadAsReset = true };

        var failure = await Assert.ThrowsExactlyAsync<IOException>(() => connection.ReadAsync(new byte[8], CancellationToken.None).AsTask());

        Assert.AreEqual(SocketError.ConnectionReset, ((SocketException)failure.InnerException!).SocketErrorCode);
        Assert.AreEqual("Unable to read data.", failure.Message);
    }

    [TestMethod]
    public async Task ReadAsync_WhenTheReadIsAbortedAndAbortsAreNotReportedAsResets_FailsWithTheAbort()
    {
        var aborted = new IOException("Unable to read data.", new SocketException((int)SocketError.ConnectionAborted));
        await using var connection = new StreamConnection(new ReadFailingStream(aborted), null) { ReportsAbortedReadAsReset = false };

        var failure = await Assert.ThrowsExactlyAsync<IOException>(() => connection.ReadAsync(new byte[8], CancellationToken.None).AsTask());

        Assert.AreSame(aborted, failure);
    }

    [TestMethod]
    public async Task ReadAsync_WhenTheFirstReceiveIsReset_FailsWithConnectionReset()
    {
        var reset = new IOException("Unable to read data.", new SocketException((int)SocketError.ConnectionReset));
        await using var connection = new StreamConnection(new ReadFailingStream(reset), null) { ReportsAbortedReadAsReset = true };

        var failure = await Assert.ThrowsExactlyAsync<IOException>(() => connection.ReadAsync(new byte[8], CancellationToken.None).AsTask());

        Assert.AreSame(reset, failure);
        Assert.AreEqual(SocketError.ConnectionReset, ((SocketException)failure.InnerException!).SocketErrorCode);
    }

    [TestMethod]
    public async Task ReadAsync_WhenTheFailureIsNoSocketError_FailsWithItUnchanged()
    {
        var other = new IOException("Disk gone.");
        await using var connection = new StreamConnection(new ReadFailingStream(other), null) { ReportsAbortedReadAsReset = true };

        var failure = await Assert.ThrowsExactlyAsync<IOException>(() => connection.ReadAsync(new byte[8], CancellationToken.None).AsTask());

        Assert.AreSame(other, failure);
    }

    /// <summary>A stream whose every read fails with the given exception.</summary>
    private sealed class ReadFailingStream(IOException failure) : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(failure);
    }
}
