using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ssh.Fakes;

namespace Curl.Original.Protocol.Ssh;

/// <summary>
/// Pins CGR-030 (BL-3559): <c>-I</c> on an <c>sftp://</c> or <c>scp://</c> file reads and
/// writes nothing and exits 0, as curl 8.22.0 does. Over SFTP curl sends <c>STAT</c> for
/// the file time, opens the file, sends <c>STAT</c> for its size and closes it with no
/// <c>READ</c>; that is also why a directory named without a trailing slash, whose first
/// read OpenSSH's sftp-server fails, exits 0 under <c>-I</c>.
/// </summary>
[TestClass]
public sealed class Cgr030Tests
{
    private const string User = "tester";

    private const string Password = "secret";

    private static readonly byte[] Hello = "hello world"u8.ToArray();

    [TestMethod]
    [DataRow(null, DisplayName = "whole file")]
    [DataRow("0-3", DisplayName = "-r 0-3")]
    public async Task ExecuteAsync_SftpNoBodyOnAFile_StatsOpensStatsAndClosesWithNoRead(string? range)
    {
        InMemorySshServer server = new(User, Password);
        server.Files["/data/hello.txt"] = Hello;

        (TransferResult result, byte[] output) = await RunAsync(server, "sftp://files.example/data/hello.txt", range);

        Assert.AreEqual(TransferResult.Success(0), result with { Report = null });
        Assert.IsEmpty(output);
        CollectionAssert.AreEqual(
            new[] { "sftp 16 .", "sftp 17 /data/hello.txt", "sftp 3 /data/hello.txt", "sftp 17 /data/hello.txt", "sftp 4 /data/hello.txt" },
            server.Events.Where(line => line.StartsWith("sftp ", StringComparison.Ordinal)).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_SftpNoBodyOnAPathWithoutATrailingSlash_SendsNoReadTheServerCouldFail()
    {
        InMemorySshServer server = new(User, Password);
        server.Files["/data/sub"] = [];

        (TransferResult result, byte[] output) = await RunAsync(server, "sftp://files.example/data/sub", null);

        Assert.AreEqual(TransferResult.Success(0), result with { Report = null });
        Assert.IsEmpty(output);
        Assert.IsFalse(server.Events.Any(line => line.StartsWith("sftp 5 ", StringComparison.Ordinal)), "no READ was sent");
    }

    [TestMethod]
    public async Task ExecuteAsync_ScpNoBodyOnAFile_WritesNothingAndClosesTheChannel()
    {
        InMemorySshServer server = new(User, Password);
        server.Files["/data/hello.txt"] = Hello;

        (TransferResult result, byte[] output) = await RunAsync(server, "scp://files.example/data/hello.txt", null);

        Assert.AreEqual(TransferResult.Success(0), result with { Report = null });
        Assert.IsEmpty(output);
        CollectionAssert.Contains(server.Events.ToList(), "channel close");
    }

    private static async Task<(TransferResult Result, byte[] Output)> RunAsync(InMemorySshServer server, string url, string? range)
    {
        SshProtocolHandler handler = new(
            server,
            new InMemoryKeyFileSystem(new Dictionary<string, string>()),
            SshAlgorithmPreferences.OpenSslReference,
            Encoding.UTF8,
            new SystemSshRandomSource(),
            new SystemSshEphemeralKeySource(),
            Environment.GetEnvironmentVariable,
            new UnreachableSshAgent());
        MemoryStream output = new();
        TransferResult result = await handler.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse(url),
            Output = output,
            Credentials = new NetworkCredential(User, Password),
            NoBody = true,
            RangeText = range,
        });
        await server.WhenSessionsEndAsync();
        return (result, output.ToArray());
    }
}
