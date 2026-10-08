using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Protocol.Ssh.Sftp;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Runs whole transfers with <c>--compressed-ssh</c> (BL-575) against an
/// <see cref="InMemorySshServer" /> that compresses as OpenSSH does: <c>zlib</c> from its
/// <c>NEWKEYS</c>, <c>zlib@openssh.com</c> from its <c>SSH_MSG_USERAUTH_SUCCESS</c>. A
/// client that started or stopped a direction's stream at any other packet would send or
/// read bytes the other side cannot parse, so each passing transfer pins the start point.
/// </summary>
public sealed partial class SshProtocolHandlerTests
{
    private static readonly SshOptions Compressed = new() { Compression = true };

    public static IEnumerable<object[]> CompressionCases =>
    [
        ["Windows", SshAlgorithmPreferences.WindowsReference, "zlib"],
        ["Windows", SshAlgorithmPreferences.WindowsReference, "zlib@openssh.com"],
        ["OpenSSL", SshAlgorithmPreferences.OpenSslReference, "zlib"],
        ["OpenSSL", SshAlgorithmPreferences.OpenSslReference, "zlib@openssh.com"],
        ["OpenSSL", SshAlgorithmPreferences.OpenSslReference, "none"],
    ];

    [TestMethod]
    [DynamicData(nameof(CompressionCases))]
    public async Task ExecuteAsync_CompressedSftpDownloadOfManyPackets_WritesTheFile(string platform, SshAlgorithmPreferences preferences, string compression)
    {
        InMemorySshServer server = new(User, Password) { Compression = compression };
        byte[] large = LargeFile();
        server.Files["/data/large.bin"] = large;

        Outcome outcome = await RunAsync(server, $"sftp://{Host}/data/large.bin", options: Compressed, preferences: preferences);

        Diagnostics.AssertResult(TransferResult.Success(large.Length), outcome.Result);
        Assert.AreEqual(TransferResult.Success(large.Length), outcome.Result, $"{platform} {compression}");
        CollectionAssert.AreEqual(large, outcome.Output);
        Assert.AreEqual("disconnect 11 Shutdown", server.Events[^1]);
    }

    [TestMethod]
    [DataRow("zlib")]
    [DataRow("zlib@openssh.com")]
    public async Task ExecuteAsync_CompressedSftpUploadOfManyPackets_StoresTheFile(string compression)
    {
        InMemorySshServer server = new(User, Password) { Compression = compression };
        byte[] large = LargeFile();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse($"sftp://{Host}/data/up.bin"),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential(User, Password),
            Upload = new MemoryStream(large),
            Ssh = Compressed,
        };
        ArrangeTransfer(context);

        TransferResult result = await Handler(server).ExecuteAsync(context);
        await server.WhenSessionsEndAsync();

        ActTransfer(result, context, server);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(large, server.Files["/data/up.bin"]);
    }

    [TestMethod]
    public async Task ExecuteAsync_CompressedScpDownloadWithDelayedZlib_WritesTheFile()
    {
        InMemorySshServer server = new(User, Password) { Compression = "zlib@openssh.com" };
        server.Files["/data/hello.txt"] = Hello;

        Outcome outcome = await RunAsync(server, $"scp://{Host}/data/hello.txt", options: Compressed);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, outcome.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, outcome.Result.ExitCode);
        CollectionAssert.AreEqual(Hello, outcome.Output);
    }

    // Compressible, but not trivially: a repeating text with a counter in it.
    private static byte[] LargeFile() =>
        [.. Enumerable.Range(0, 7000).SelectMany(index => System.Text.Encoding.ASCII.GetBytes($"line {index % 97:D3}\n"))];
}
