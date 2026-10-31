using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Scp;
using Curl.Ssh;
using Curl.Ssh.KeyExchange;
using Curl.Ssh.Negotiation;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Pins CGR-134 (BL-3663): an <c>scp://</c> path holding <c>%00</c> exits 3, <c>URL using
/// bad/illegal format or missing URL</c>, as curl 8.22.0 does, and no channel is opened, so
/// the NUL never reaches the remote <c>exec</c> command. curl's <c>Curl_getworkingpath</c>
/// rejects the zero byte in <c>SSH_SCP_TRANS_INIT</c>, after login and before
/// <c>libssh2_scp_recv2</c> or <c>libssh2_scp_send64</c> opens the channel; a closed port
/// still answers exit 7, so curl connects before it looks at the path.
/// </summary>
[TestClass]
public sealed class Cgr134Tests
{
    private const string User = "tester";

    private const string Password = "secret";

    [TestMethod]
    [DataRow("/a%00b", DisplayName = "as reported")]
    [DataRow("/~/a%00b", DisplayName = "under the home directory")]
    public void Resolve_PathHoldsZeroByte_ThrowsUrlMalformat(string urlPath)
    {
        SshTransferException failure = Assert.ThrowsExactly<SshTransferException>(() => ScpRemotePath.Resolve(urlPath));

        Assert.AreEqual(CurlExitCode.UrlMalformat, failure.ExitCode);
        Assert.AreEqual("URL using bad/illegal format or missing URL", failure.Message);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "download")]
    [DataRow(true, DisplayName = "upload")]
    public async Task ExecuteAsync_ScpPathHoldsZeroByte_IsExit3WithNoChannelOpened(bool upload)
    {
        InMemorySshServer server = new(User, Password);
        server.Files["/data/a"] = "hello"u8.ToArray();

        TransferResult result = await RunAsync(server, "scp://files.example/data/a%00b", upload);

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual("URL using bad/illegal format or missing URL", result.ErrorMessage);
        Assert.IsFalse(server.Events.Any(line => line.StartsWith("channel open", StringComparison.Ordinal)), "no channel was opened");
    }

    private static async Task<TransferResult> RunAsync(InMemorySshServer server, string url, bool upload)
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
        TransferResult result = await handler.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential(User, Password),
            Upload = upload ? new MemoryStream("up"u8.ToArray()) : null,
        });
        await server.WhenSessionsEndAsync();
        return result;
    }
}
