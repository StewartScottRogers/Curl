using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Pins <c>--max-filesize</c> over <c>scp://</c> end to end (BL-1328): a download cut at
/// the limit fails with exit 63, reports curl's <c>failf</c> text as a <c>-v</c> line and
/// closes the channel as any other SCP download does; an upload ignores the limit.
/// </summary>
public sealed partial class SshProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_ScpDownloadOverTheMaxFileSize_IsExit63WithTheInfoLineAndClosesTheChannel()
    {
        InMemorySshServer server = Server();
        server.Files["/data/hello.txt"] = HelloLine;
        MemoryStream output = new();
        TranscriptTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse($"scp://{Host}/data/hello.txt"),
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
        int exec = Array.IndexOf(serverEvents, "exec scp -pf '/data/hello.txt'");
        Assert.AreEqual(string.Join(" | ", ChannelClosedThenDisconnected), string.Join(" | ", serverEvents[(exec + 1)..]));
    }

    [TestMethod]
    public async Task ExecuteAsync_ScpUploadWithAMaxFileSize_IgnoresIt()
    {
        InMemorySshServer server = Server();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse($"scp://{Host}/data/up.txt"),
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
