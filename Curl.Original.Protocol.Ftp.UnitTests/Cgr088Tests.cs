using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp.Fakes;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// CGR-088 (BL-3617): curl 8.22.0's <c>failf</c> writes a failure's message as a <c>-v</c> line
/// before the lines that end the connection, measured against Record-CurlExchange.ps1 -Ftp.
/// </summary>
[TestClass]
public sealed class Cgr088Tests
{
    private const string Url = "ftp://127.0.0.1:41704/nope.txt";

    private const string LoggedIn = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    [TestMethod]
    public async Task ExecuteAsync_PasswordRefused_EchoesAccessDeniedThenClosesTheConnection()
    {
        // curl -sv -u bad:x ftp://127.0.0.1:41703/file.txt, PASS answered 530: "* Access denied: 530", "* closing connection #0".
        var events = new RecordingTransferEvents();

        FtpRun run = await RunAsync("220 Recorder ready\r\n331 Password required\r\n530 Login incorrect.\r\n", events);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Access denied: 530"), run.Result);
        CollectionAssert.AreEqual(new[] { "Access denied: 530", "closing connection #0" }, events.Info.TakeLast(2).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_SizeRefused_EchoesTheFileDoesNotExistBeforeTheDirectoryLine()
    {
        // curl -sSv ftp://127.0.0.1:41704/nope.txt, SIZE answered 550.
        var events = new RecordingTransferEvents();

        FtpRun run = await RunAsync(LoggedIn + "229 Entering Extended Passive Mode (|||40001|)\r\n200 Type set\r\n550 No such file\r\n221 Bye\r\n", events);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RemoteFileNotFound, "The file does not exist"), run.Result);
        CollectionAssert.AreEqual(
            new[] { "The file does not exist", "Remembering we are in directory \"\"", "Connection #0 to host 127.0.0.1:41704 left intact" },
            events.Info.TakeLast(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_QuoteRefused_EchoesQuotCommandFailedThenShutsTheConnectionDown()
    {
        // curl -sSv -Q BOGUS ftp://127.0.0.1:41705/file.txt: "* QUOT command failed with 500", "* shutting down connection #0".
        var events = new RecordingTransferEvents();

        FtpRun run = await RunAsync(LoggedIn + "500 Unknown command\r\n", events, quote: "BOGUS");

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.QuoteError, "QUOT command failed with 500"), run.Result);
        CollectionAssert.AreEqual(new[] { "QUOT command failed with 500", "shutting down connection #0" }, events.Info.TakeLast(2).ToArray());
    }

    private static Task<FtpRun> RunAsync(string replies, RecordingTransferEvents events, string? quote = null) =>
        FtpRun.ExecuteAsync(Url, replies, adjust: context => MutableContext.Build(context, mutable =>
        {
            mutable.Events = events;
            if (quote is not null)
            {
                mutable.QuoteCommands.Add(quote);
            }
        }));
}
