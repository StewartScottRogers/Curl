using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Connection;
using Curl.Protocol.Ssh.Fakes;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// Pins where <see cref="SftpFileDownload" />, <see cref="SftpDirectoryListing" /> and
/// <see cref="SftpFileUpload" /> run the <c>-Q</c> commands, against an in-memory peer, as
/// measured 2026-09-29 with curl 8.21.0 (libssh2 1.11.1) against OpenSSH 10.2's
/// <c>sftp-server</c> (BL-572, ADR-0247): those with no prefix right after
/// <c>REALPATH .</c>, before the <c>STAT</c> of <c>-C -</c> or the open; those with a
/// <c>-</c> after a successful transfer's <c>CLOSE</c> and before the channel's; a failure
/// after the transfer keeps its bytes and takes exit 21.
/// </summary>
[TestClass]
public sealed class SftpTransferQuoteTests
{
    private const UnixFileMode Mode0644 =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

    private static readonly byte[] Hello = "hello"u8.ToArray();

    private static readonly byte[] Realpath = Join([SftpPacketType.RealPath], UInt32(0), Name("."));

    // The server's CLOSE of the channel, which the client answers with its own.
    private static readonly byte[] ServerClose = [SshConnectionMessageNumber.ChannelClose, .. UInt32(0)];

    [TestMethod]
    public async Task DownloadAsync_Quotes_RunBeforeTheOpenAndAfterTheCloseAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Status(1, SftpStatusCode.Ok)
            .Handle(2).Size(5, 3).Data(4, Hello).Status(5, SftpStatusCode.Ok).Status(6, SftpStatusCode.Ok).Ssh(ServerClose);
        MemoryStream header = new();
        MemoryStream output = new();
        ScriptedConnection connection = new(script.Bytes);

        TransferResult result = await new SftpFileDownload(SftpSessionTests.Transport(connection)).DownloadAsync(
            "/~/a.txt", Mode0644, output, NoTransferProgress.Instance, CancellationToken.None, Quotes(header, ["rm /a", "pwd"], ["rm /b", "pwd"]));

        Assert.AreEqual(TransferResult.Success(5), result);
        CollectionAssert.AreEqual(Hello, output.ToArray());
        Assert.AreEqual("257 \"/home/fake/a.txt\" is current directory.\n257 \"(nil)\" is current directory.\n", Encoding.UTF8.GetString(header.ToArray()), "as measured");
        AssertRequests(
            connection,
            Realpath,
            SftpQuoteCommandsTests.PathRequest(SftpPacketType.Remove, 1, "/a"),
            SftpServerScript.OpenRequest("/home/fake/a.txt", 2),
            SftpServerScript.StatRequest("/home/fake/a.txt", 3),
            SftpServerScript.ReadRequest(4, 0, 20),
            SftpServerScript.CloseRequest(5),
            SftpQuoteCommandsTests.PathRequest(SftpPacketType.Remove, 6, "/b"));
        AssertChannelClosedLast(connection);
    }

    [TestMethod]
    public async Task DownloadAsync_CommandAfterTheTransferFails_KeepsTheBytesWithExit21AndClosesTheChannelAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().Opened(5).Data(3, Hello).Status(4, SftpStatusCode.Ok).Status(5, 2).Ssh(ServerClose);
        ScriptedConnection connection = new(script.Bytes);

        TransferResult result = await new SftpFileDownload(SftpSessionTests.Transport(connection)).DownloadAsync(
            "/f", Mode0644, new MemoryStream(), NoTransferProgress.Instance, CancellationToken.None, Quotes(null, [], ["rm /zz"]));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.QuoteError, "rm \"/zz\" failed: No such file or directory", 5), result);
        AssertChannelClosedLast(connection);
    }

    [TestMethod]
    public async Task DownloadAsync_TransferFails_RunsNoCommandAfterItAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().Opened(5).Status(3, 4).Status(4, SftpStatusCode.Ok);
        ScriptedConnection connection = new(script.Bytes);

        TransferResult result = await new SftpFileDownload(SftpSessionTests.Transport(connection)).DownloadAsync(
            "/f", Mode0644, new MemoryStream(), NoTransferProgress.Instance, CancellationToken.None, Quotes(null, [], ["mkdir /nd"]));

        Assert.AreEqual(CurlExitCode.Ssh, result.ExitCode);
        CollectionAssert.AreEqual(SftpServerScript.CloseRequest(4), SftpServerScript.SftpRequests(connection.Written)[^1]);
    }

    [TestMethod]
    public async Task DownloadAsync_CommandBeforeTheTransferFails_ThrowsExit21BeforeTheOpenAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Status(1, 2);
        ScriptedConnection connection = new(script.Bytes);

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(async () =>
            await new SftpFileDownload(SftpSessionTests.Transport(connection)).DownloadAsync(
                "/f", Mode0644, new MemoryStream(), NoTransferProgress.Instance, CancellationToken.None, Quotes(null, ["rm /zz"], [])));

        Assert.AreEqual(CurlExitCode.QuoteError, failure.ExitCode);
        Assert.AreEqual("rm \"/zz\" failed: No such file or directory", failure.Message);
        AssertRequests(connection, Realpath, SftpQuoteCommandsTests.PathRequest(SftpPacketType.Remove, 1, "/zz"));
    }

    [TestMethod]
    public async Task UploadAsync_Quotes_RunBeforeTheStatOfResumeAndAfterTheCloseAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Status(1, SftpStatusCode.Ok).Status(2, 2)
            .Handle(3).Status(4, SftpStatusCode.Ok).Status(5, SftpStatusCode.Ok).Status(6, SftpStatusCode.Ok).Ssh(ServerClose);
        ScriptedConnection connection = new(script.Bytes);
        SftpUploadOptions options = new(0, true, false, false, Mode0644);

        TransferResult result = await new SftpFileUpload(SftpSessionTests.Transport(connection)).UploadAsync(
            "/u.txt", options, new MemoryStream(Hello), NoTransferProgress.Instance, CancellationToken.None, Quotes(null, ["rm /b"], ["mkdir /nd"]));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(5, result.Report!.UploadSize);
        AssertRequests(
            connection,
            Realpath,
            SftpQuoteCommandsTests.PathRequest(SftpPacketType.Remove, 1, "/b"),
            SftpServerScript.StatRequest("/u.txt", 2),
            SftpServerScript.OpenRequest("/u.txt", 0x1A, 3),
            SftpServerScript.WriteRequest(4, 0, Hello),
            SftpServerScript.CloseRequest(5),
            SftpServerScript.MakeDirectoryRequest("/nd", 6));
    }

    [TestMethod]
    public async Task UploadAsync_CommandAfterTheTransferFails_ReportsTheUploadedSizeWithExit21()
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Handle().Status(2, SftpStatusCode.Ok).Status(3, SftpStatusCode.Ok).Status(4, 4);
        ScriptedConnection connection = new(script.Bytes);

        TransferResult result = await new SftpFileUpload(SftpSessionTests.Transport(connection)).UploadAsync(
            "/u.txt", new SftpUploadOptions(0, false, false, false, Mode0644), new MemoryStream(Hello), NoTransferProgress.Instance, CancellationToken.None, Quotes(null, [], ["mkdir /nd"]));

        Assert.AreEqual(CurlExitCode.QuoteError, result.ExitCode);
        Assert.AreEqual("mkdir \"/nd\" failed: Operation failed", result.ErrorMessage);
        Assert.AreEqual(5, result.BytesTransferred);
        Assert.AreEqual(5, result.Report!.UploadSize);
    }

    [TestMethod]
    public async Task ListAsync_Quotes_RunBeforeTheOpendirAndAfterTheCloseAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Status(1, SftpStatusCode.Ok)
            .Handle(2).Status(3, SftpStatusCode.EndOfFile).Status(4, SftpStatusCode.Ok).Status(5, SftpStatusCode.Ok);
        MemoryStream header = new();
        ScriptedConnection connection = new(script.Bytes);

        TransferResult result = await new SftpDirectoryListing(SftpSessionTests.Transport(connection)).ListAsync(
            "/d/", listOnly: false, noBody: false, new MemoryStream(), NoTransferProgress.Instance, CancellationToken.None, Quotes(header, ["rm /b", "pwd"], ["mkdir /nd"]));

        Assert.AreEqual(TransferResult.Success(0), result);
        Assert.AreEqual("257 \"/d/\" is current directory.\n", Encoding.UTF8.GetString(header.ToArray()), "as measured");
        AssertRequests(
            connection,
            Realpath,
            SftpQuoteCommandsTests.PathRequest(SftpPacketType.Remove, 1, "/b"),
            SftpServerScript.OpenDirectoryRequest("/d/", 2),
            SftpServerScript.ReadDirectoryRequest(3),
            SftpServerScript.CloseRequest(4),
            SftpServerScript.MakeDirectoryRequest("/nd", 5));
    }

    [TestMethod]
    public async Task ListAsync_NoBody_RunsBothListsWithNoOpendir()
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Status(1, SftpStatusCode.Ok).Status(2, SftpStatusCode.Ok).Ssh(ServerClose);
        ScriptedConnection connection = new(script.Bytes);

        TransferResult result = await new SftpDirectoryListing(SftpSessionTests.Transport(connection)).ListAsync(
            "/d/", listOnly: false, noBody: true, new MemoryStream(), NoTransferProgress.Instance, CancellationToken.None, Quotes(null, ["rm /a"], ["rm /b"]));

        Assert.AreEqual(TransferResult.Success(0), result);
        AssertRequests(
            connection,
            Realpath,
            SftpQuoteCommandsTests.PathRequest(SftpPacketType.Remove, 1, "/a"),
            SftpQuoteCommandsTests.PathRequest(SftpPacketType.Remove, 2, "/b"));
        AssertChannelClosedLast(connection);
    }

    private static SftpQuoteCommands Quotes(Stream? header, string[] before, string[] after) => new(before, after, header, cLongIs32Bits: true);

    // Every SFTP request after INIT.
    private static void AssertRequests(ScriptedConnection connection, params byte[][] expected)
    {
        List<byte[]> requests = [.. SftpServerScript.SftpRequests(connection.Written).Skip(1)];
        Assert.HasCount(expected.Length, requests);
        for (int index = 0; index < expected.Length; index++)
        {
            CollectionAssert.AreEqual(expected[index], requests[index], $"request {index}");
        }
    }

    // Measured: the channel's EOF and CLOSE follow the last SFTP request.
    private static void AssertChannelClosedLast(ScriptedConnection connection)
    {
        List<byte[]> payloads = SftpServerScript.SshPayloads(connection.Written);
        CollectionAssert.AreEqual(
            new[] { SshConnectionMessageNumber.ChannelEof, SshConnectionMessageNumber.ChannelClose },
            payloads.TakeLast(2).Select(payload => payload[0]).ToArray());
    }
}
