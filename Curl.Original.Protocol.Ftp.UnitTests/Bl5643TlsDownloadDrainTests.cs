using System.Net;
using System.Text;
using Curl.Original.Protocol.Ftp.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Pins BL-5643: a TLS download that has read its announced size does not close the data
/// connection with the server's <c>close_notify</c> still unread, because the reset that follows
/// can fail the server's last write, so the server answers <c>426</c> and the download exits 18
/// (<c>PartialFile</c>). The data connection here reports whether its close_notify was read, as
/// a zero read after the client's shutdown, before it was disposed.
/// </summary>
[TestClass]
public sealed class Bl5643TlsDownloadDrainTests
{
    private const string Replies =
        "220 ready\r\n331 Password required\r\n230 Logged in\r\n200 PBSZ=0\r\n200 Protection level set to P\r\n" +
        "257 \"/\" is current directory\r\n229 Entering Extended Passive Mode (|||64396|)\r\n200 Type set\r\n213 5\r\n" +
        "150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n221 Bye\r\n";

    [TestMethod]
    public async Task ExecuteAsync_TlsDownloadThatReadItsAnnouncedSize_ReadsTheServersCloseNotifyBeforeClosing()
    {
        var data = new CloseNotifyConnection();
        var connector = new QueuedConnector(
            ConnectResult.Connected(ScriptedConnection.FromReplies(Replies)),
            ConnectResult.Connected(new ScriptedConnection()));
        var output = new MemoryStream();
        var context = new TransferContext { Url = CurlUrl.Parse("ftps://127.0.0.1/a.txt"), Output = output };

        TransferResult result = await new FtpProtocolHandler(connector, new QueuedListener(), new QueuedTlsProvider(ConnectResult.Connected(data))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual("hello", Encoding.Latin1.GetString(output.ToArray()));
        Assert.IsTrue(data.CloseNotifyReadBeforeDispose);
    }

    /// <summary>Sends "hello", then its close_notify (a zero read) to whoever reads again.</summary>
    private sealed class CloseNotifyConnection : IConnection
    {
        private bool sentData;

        private bool shutDown;

        public bool CloseNotifyReadBeforeDispose { get; private set; }

        public bool IsSecure => true;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (!sentData)
            {
                sentData = true;
                Encoding.Latin1.GetBytes("hello").CopyTo(buffer.Span);
                return ValueTask.FromResult(5);
            }

            CloseNotifyReadBeforeDispose = shutDown;
            return ValueTask.FromResult(0);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask ShutDownSendingAsync(CancellationToken cancellationToken)
        {
            shutDown = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
