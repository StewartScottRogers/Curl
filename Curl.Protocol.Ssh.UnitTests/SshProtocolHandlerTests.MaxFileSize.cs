using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Pins <c>--max-filesize</c> over <c>sftp://</c> end to end (BL-1327): a download cut at
/// the limit fails with exit 63, reports curl's <c>failf</c> text as a <c>-v</c> line and
/// tears the session down as any other failed copy does; an upload ignores the limit.
/// </summary>
public sealed partial class SshProtocolHandlerTests
{
    private static readonly byte[] HelloLine = "hello\n"u8.ToArray();

    [TestMethod]
    public async Task ExecuteAsync_SftpDownloadOverTheMaxFileSize_IsExit63WithTheInfoLineAndClosesTheHandle()
    {
        InMemorySshServer server = Server();
        server.Files["/data/hello.txt"] = HelloLine;
        MemoryStream output = new();
        TranscriptTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse($"sftp://{Host}/data/hello.txt"),
            Output = output,
            Credentials = new NetworkCredential(User, Password),
            Events = events,
            MaxFileSize = 3,
        };
        ArrangeTransfer(context);

        TransferResult result = await Handler(server).ExecuteAsync(context);
        await server.WhenSessionsEndAsync();

        ActTransfer(result, context, server);

        Diagnostics.Assert("exit code", CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual("Exceeded the maximum allowed file size (3) with 3 bytes", result.ErrorMessage);
        CollectionAssert.AreEqual("hel"u8.ToArray(), output.ToArray());
        CollectionAssert.Contains(events.Transcript, "* Exceeded the maximum allowed file size (3) with 3 bytes");
        string[] serverEvents = [.. server.Events];
        int subsystem = Array.IndexOf(serverEvents, "subsystem sftp");
        string[] expected = ["sftp 16 .", "sftp 3 /data/hello.txt", "sftp 17 /data/hello.txt", "sftp 5 /data/hello.txt", "sftp 4 /data/hello.txt", .. ChannelClosedThenDisconnected];
        Assert.AreEqual(string.Join(" | ", expected), string.Join(" | ", serverEvents[(subsystem + 1)..]));
    }

    [TestMethod]
    [DataRow(false, "-rw-r--r--", DisplayName = "long names")]
    [DataRow(true, "hello.txt\n", DisplayName = "-l")]
    public async Task ExecuteAsync_SftpListingOverTheMaxFileSize_IsExit63WithTheInfoLineAndClosesTheHandle(bool listOnly, string expected)
    {
        InMemorySshServer server = Server();
        server.Files["/data/hello.txt"] = HelloLine;
        server.Files["/data/world.txt"] = HelloLine;
        MemoryStream output = new();
        TranscriptTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse($"sftp://{Host}/data/"),
            Output = output,
            Credentials = new NetworkCredential(User, Password),
            Events = events,
            ListOnly = listOnly,
            MaxFileSize = 10,
        };
        ArrangeTransfer(context);

        TransferResult result = await Handler(server).ExecuteAsync(context);
        await server.WhenSessionsEndAsync();

        ActTransfer(result, context, server);

        Diagnostics.Assert("exit code", CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual("Exceeded the maximum allowed file size (10) with 10 bytes", result.ErrorMessage);
        Assert.AreEqual(expected, System.Text.Encoding.UTF8.GetString(output.ToArray()));
        CollectionAssert.Contains(events.Transcript, "* Exceeded the maximum allowed file size (10) with 10 bytes");
        string[] serverEvents = [.. server.Events];
        int subsystem = Array.IndexOf(serverEvents, "subsystem sftp");
        string[] expectedEvents = ["sftp 16 .", "sftp 11 /data/", "sftp 12 /data/", "sftp 4 /data/", .. ChannelClosedThenDisconnected];
        Assert.AreEqual(string.Join(" | ", expectedEvents), string.Join(" | ", serverEvents[(subsystem + 1)..]));
    }

    [TestMethod]
    public async Task ExecuteAsync_SftpUploadWithAMaxFileSize_IgnoresIt()
    {
        InMemorySshServer server = Server();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse($"sftp://{Host}/data/up.txt"),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential(User, Password),
            Upload = new MemoryStream(HelloLine),
            MaxFileSize = 1,
        };
        ArrangeTransfer(context);

        TransferResult result = await Handler(server).ExecuteAsync(context);
        await server.WhenSessionsEndAsync();

        ActTransfer(result, context, server);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(HelloLine, server.Files["/data/up.txt"]);
    }
}
