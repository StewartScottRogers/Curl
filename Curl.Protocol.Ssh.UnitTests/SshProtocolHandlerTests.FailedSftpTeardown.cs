using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Pins the teardown after a failed <c>REALPATH</c>, <c>OPEN</c>, <c>OPENDIR</c>, upload
/// <c>OPEN</c> or <c>-Q</c> command: the channel's <c>EOF</c> and <c>CLOSE</c> before
/// <c>DISCONNECT</c>, as OpenSSH's <c>sshd -ddd</c> log showed curl 8.21.0 sending them,
/// measured 2026-09-30 (BL-973); and after a <c>%00</c> in the path, refused after
/// <c>REALPATH</c> with exit 3 (BL-974, ADR-0275).
/// </summary>
public sealed partial class SshProtocolHandlerTests
{
    private static readonly string[] ChannelClosedThenDisconnected = ["channel eof", "channel close", "disconnect 11 Shutdown"];

    [TestMethod]
    public async Task ExecuteAsync_SftpDownloadOpenFails_ClosesTheChannelBeforeDisconnecting()
    {
        InMemorySshServer server = Server();

        TransferResult result = await RunFailingAsync(server, "/data/missing.txt");

        AssertFailedTeardown(
            server,
            result,
            CurlExitCode.RemoteFileNotFound,
            "Could not open remote file for reading: No such file or directory",
            "sftp 16 .",
            "sftp 3 /data/missing.txt");
    }

    [TestMethod]
    public async Task ExecuteAsync_SftpListingOpenDirectoryFails_ClosesTheChannelBeforeDisconnecting()
    {
        InMemorySshServer server = Server();

        TransferResult result = await RunFailingAsync(server, "/nothere/");

        AssertFailedTeardown(
            server,
            result,
            CurlExitCode.RemoteFileNotFound,
            "Could not open directory for reading: No such file or directory",
            "sftp 16 .",
            "sftp 11 /nothere/");
    }

    [TestMethod]
    public async Task ExecuteAsync_SftpUploadOpenFails_ClosesTheChannelBeforeDisconnecting()
    {
        InMemorySshServer server = Server();
        server.RefusedPaths.Add("/data/up.txt");

        TransferResult result = await RunFailingAsync(server, "/data/up.txt", upload: true);

        AssertFailedTeardown(
            server,
            result,
            CurlExitCode.RemoteFileNotFound,
            "Upload failed: No such file or directory (2/-31)",
            "sftp 16 .",
            "sftp 3 /data/up.txt");
    }

    [TestMethod]
    [DataRow("/data/hello.txt", false, DisplayName = "download")]
    [DataRow("/data/", false, DisplayName = "listing")]
    [DataRow("/data/up.txt", true, DisplayName = "upload")]
    public async Task ExecuteAsync_SftpRealPathFails_ClosesTheChannelBeforeDisconnecting(string path, bool upload)
    {
        InMemorySshServer server = Server();
        server.Files["/data/hello.txt"] = Hello;
        server.RefusedPaths.Add(".");

        TransferResult result = await RunFailingAsync(server, path, upload);

        AssertFailedTeardown(server, result, CurlExitCode.RemoteFileNotFound, "Remote file not found", "sftp 16 .");
    }

    [TestMethod]
    [DataRow("/data/a%00.txt", false, DisplayName = "download, as measured")]
    [DataRow("/data%00/", false, DisplayName = "listing")]
    [DataRow("/data/a%00.txt", true, DisplayName = "upload")]
    public async Task ExecuteAsync_SftpPathHoldsZeroByte_IsExit3AfterRealPathAndClosesTheChannel(string path, bool upload)
    {
        InMemorySshServer server = Server();

        TransferResult result = await RunFailingAsync(server, path, upload);

        AssertFailedTeardown(server, result, CurlExitCode.UrlMalformat, "URL using bad/illegal format or missing URL", "sftp 16 .");
    }

    [TestMethod]
    [DataRow("/data/hello.txt", false, DisplayName = "download")]
    [DataRow("/data/", false, DisplayName = "listing")]
    [DataRow("/data/up.txt", true, DisplayName = "upload")]
    public async Task ExecuteAsync_SftpQuoteCommandFails_ClosesTheChannelBeforeDisconnecting(string path, bool upload)
    {
        InMemorySshServer server = Server();
        server.Files["/data/hello.txt"] = Hello;
        server.RefusedPaths.Add("/missing");

        TransferResult result = await RunFailingAsync(server, path, upload, "rm /missing");

        AssertFailedTeardown(
            server,
            result,
            CurlExitCode.QuoteError,
            "rm \"/missing\" failed: No such file or directory",
            "sftp 16 .",
            "sftp 13 /missing");
    }

    private static async Task<TransferResult> RunFailingAsync(InMemorySshServer server, string path, bool upload = false, string? quote = null)
    {
        TransferContext context = new()
        {
            Url = CurlUrl.Parse($"sftp://{Host}{path}"),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential(User, Password),
            Upload = upload ? new MemoryStream(Hello) : null,
            QuoteCommands = quote is null ? [] : [quote],
        };

        TransferResult result = await Handler(server).ExecuteAsync(context);
        await server.WhenSessionsEndAsync();
        return result;
    }

    // Everything after the subsystem starts: the SFTP requests, then the channel's close
    // and DISCONNECT.
    private static void AssertFailedTeardown(InMemorySshServer server, TransferResult result, CurlExitCode exitCode, string message, params string[] requests)
    {
        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        string[] events = [.. server.Events];
        int subsystem = Array.IndexOf(events, "subsystem sftp");
        Assert.AreEqual(string.Join(" | ", [.. requests, .. ChannelClosedThenDisconnected]), string.Join(" | ", events[(subsystem + 1)..]));
    }
}
