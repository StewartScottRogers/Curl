using System.Net;
using Curl.Extension.ResumableUpload.Http;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>Pins how a broken connection becomes curl's send (55) or receive (56) failure, one the upload may resume after.</summary>
[TestClass]
public sealed class ResumableUploadInterruptibleConnectionTests
{
    [TestMethod]
    public void IsSecureAndRemoteEndPoint_AreTheInnerConnections()
    {
        var connection = new ResumableUploadInterruptibleConnection(new BrokenConnection());

        Assert.IsTrue(connection.IsSecure);
        Assert.AreEqual(BrokenConnection.EndPoint, connection.RemoteEndPoint);
    }

    [TestMethod]
    public async Task ReadAsync_ConnectionBreaks_IsAResumableReceiveFailure()
    {
        var exception = await Assert.ThrowsExactlyAsync<ResumableUploadTransferException>(
            async () => await new ResumableUploadInterruptibleConnection(new BrokenConnection()).ReadAsync(new byte[1], CancellationToken.None));

        Assert.AreEqual(CurlExitCode.RecvError, exception.ExitCode);
        Assert.AreEqual("Recv failure: The connection was reset.", exception.Message);
        Assert.IsTrue(exception.CanResume);
    }

    [TestMethod]
    public async Task FlushAsync_ConnectionBreaks_IsAResumableSendFailure()
    {
        var exception = await Assert.ThrowsExactlyAsync<ResumableUploadTransferException>(
            async () => await new ResumableUploadInterruptibleConnection(new BrokenConnection()).FlushAsync(CancellationToken.None));

        Assert.AreEqual(CurlExitCode.SendError, exception.ExitCode);
        Assert.AreEqual("Send failure: The connection was reset.", exception.Message);
        Assert.IsTrue(exception.CanResume);
    }

    [TestMethod]
    public async Task WriteAsync_ConnectionBreaks_IsAResumableSendFailure()
    {
        var exception = await Assert.ThrowsExactlyAsync<ResumableUploadTransferException>(
            async () => await new ResumableUploadInterruptibleConnection(new RecordedConnection(string.Empty, writesBeforeBreak: 0)).WriteAsync(new byte[1], CancellationToken.None));

        Assert.AreEqual(CurlExitCode.SendError, exception.ExitCode);
    }

    private sealed class BrokenConnection : IConnection
    {
        public static readonly IPEndPoint EndPoint = new(IPAddress.Loopback, 443);

        public bool IsSecure => true;

        public EndPoint? RemoteEndPoint => EndPoint;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => throw new IOException("The connection was reset.");

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => throw new IOException("The connection was reset.");

        public ValueTask FlushAsync(CancellationToken cancellationToken) => throw new IOException("The connection was reset.");

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
