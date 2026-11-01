using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp.Fakes;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Pins that the data connection is closed before the end-of-transfer reply is read, as
/// curl 8.22.0's <c>ftp_done</c> closes it (BL-3161): a server such as test.rebex.net waits
/// for the client to close the data connection, and answers <c>426</c> after its own timeout
/// when the client reads the reply first. The control connection here plays that server: it
/// answers <c>226</c> when the data connection has been closed by the time the reply is
/// read, and <c>426</c> when it is still open. The fakes answer at once and the time provider
/// never moves, so nothing waits on a timeout.
/// </summary>
/// <remarks>
/// FTPS needs no test of its own: a TLS data connection is the connection the TLS provider
/// hands back, held in the same field and closed on the same path as a plain one.
/// </remarks>
[TestClass]
public sealed class FtpProtocolHandlerGracefulDataCloseTests
{
    private const string LoggedIn = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private const string Passive = "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n";

    private const string Opening = "150 Opening BINARY mode data connection\r\n";

    private const string Bye = "221 Bye\r\n";

    [TestMethod]
    public async Task ExecuteAsync_DownloadFromAServerThatWaitsForTheDataClose_ClosesTheDataFirstAndSucceeds()
    {
        var data = new ScriptedConnection(Encoding.Latin1.GetBytes("hello"));
        var control = new DataCloseWaitingControl(data, LoggedIn + Passive + "213 5\r\n" + Opening);

        (TransferResult result, string output) = await RunAsync("ftp://127.0.0.1:18321/file.txt", control, data);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual("hello", output);
        Assert.IsTrue(control.AnsweredComplete);
    }

    [TestMethod]
    public async Task ExecuteAsync_ListingFromAServerThatWaitsForTheDataClose_ClosesTheDataFirstAndSucceeds()
    {
        const string listing = "-rw-r--r-- 1 user group 5 Jan 01 00:00 file.txt\r\n";
        var data = new ScriptedConnection(Encoding.Latin1.GetBytes(listing));
        var control = new DataCloseWaitingControl(data, LoggedIn + "250 OK\r\n" + Passive + Opening);

        (TransferResult result, string output) = await RunAsync("ftp://127.0.0.1:18321/dir/", control, data);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual(FtpAsciiOutput.Of(listing), output);
        Assert.IsTrue(control.AnsweredComplete);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadToAServerThatWaitsForTheDataClose_ClosesTheDataFirstAndSucceeds()
    {
        var data = new ScriptedConnection();
        var control = new DataCloseWaitingControl(data, LoggedIn + Passive + Opening);

        (TransferResult result, _) = await RunAsync(
            "ftp://127.0.0.1:18321/up.txt",
            control,
            data,
            new MemoryStream(Encoding.Latin1.GetBytes("upload")));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual("upload", Encoding.Latin1.GetString(data.Sent));
        Assert.IsTrue(control.AnsweredComplete);
    }

    [TestMethod]
    public async Task DataCloseWaitingControl_WhenTheDataConnectionIsStillOpen_Answers426()
    {
        var data = new ScriptedConnection();
        var control = new DataCloseWaitingControl(data, "220 Ready\r\n");
        byte[] buffer = new byte[256];

        int greeting = await control.ReadAsync(buffer, CancellationToken.None);
        int end = await control.ReadAsync(buffer, CancellationToken.None);

        Assert.AreEqual("220 Ready\r\n".Length, greeting);
        StringAssert.StartsWith(Encoding.Latin1.GetString(buffer, 0, end), "426 ");
        Assert.IsFalse(control.AnsweredComplete);
    }

    private static async Task<(TransferResult Result, string Output)> RunAsync(
        string url,
        DataCloseWaitingControl control,
        ScriptedConnection data,
        Stream? upload = null)
    {
        var connector = new QueuedConnector(ConnectResult.Connected(control), ConnectResult.Connected(data));
        var output = new MemoryStream();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(url),
            Output = output,
            Upload = upload,
            TimeProvider = new SteppingTimeProvider(),
        };

        TransferResult result = await new FtpProtocolHandler(connector).ExecuteAsync(context);
        return (result, Encoding.Latin1.GetString(output.ToArray()));
    }

    /// <summary>
    /// A control connection that reads <c>repliesBeforeEnd</c>, then the end-of-transfer reply:
    /// <c>226</c> when <c>data</c> has been closed, otherwise <c>426</c>, as a server that waits
    /// for the client's close answers once its wait runs out; then <c>221</c>.
    /// </summary>
    private sealed class DataCloseWaitingControl(ScriptedConnection data, string repliesBeforeEnd) : IConnection
    {
        private readonly ScriptedConnection before = ScriptedConnection.FromReplies(repliesBeforeEnd);

        private readonly ScriptedConnection after = ScriptedConnection.FromReplies(Bye);

        private bool endAnswered;

        public bool AnsweredComplete { get; private set; }

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int read = await before.ReadAsync(buffer, cancellationToken);
            if (read > 0)
            {
                return read;
            }

            if (endAnswered)
            {
                return await after.ReadAsync(buffer, cancellationToken);
            }

            endAnswered = true;
            AnsweredComplete = data.IsDisposed;
            byte[] reply = Encoding.Latin1.GetBytes(AnsweredComplete
                ? "226 Transfer complete\r\n"
                : "426 Data connection closed, unable to close data connection gracefully\r\n");
            reply.CopyTo(buffer);
            return reply.Length;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
            before.WriteAsync(buffer, cancellationToken);

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
