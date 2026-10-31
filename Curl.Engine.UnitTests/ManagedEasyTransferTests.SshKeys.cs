using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Original.Protocol.Ssh.Fakes;

namespace Curl.Engine;

// CURLOPT_SSH_KEYFUNCTION and CURLOPT_SSH_HOSTKEYFUNCTION called during a whole sftp transfer's host-key check (BL-2422, ADR-0623).
public sealed partial class ManagedEasyTransferTests
{
    private const string SshUser = "user";

    private const string SshPassword = "secret";

    [TestMethod]
    [DataRow("127.0.0.1", SshKeyMatch.Ok, DisplayName = "the file holds the key: CURLKHMATCH_OK")]
    [DataRow("127.0.0.2", SshKeyMatch.Missing, DisplayName = "the file lacks the host: CURLKHMATCH_MISSING")]
    public async Task PerformAsync_SshKeyCallbackAccepts_IsToldTheMatchAndTransfers(string knownHost, SshKeyMatch expected)
    {
        InMemorySshServer server = SshServerWithFile();
        string knownHosts = WriteKnownHosts(server.KnownHostsLine(knownHost));
        List<SshKeyMatch> matches = [];
        try
        {
            (CurlCode code, string body) = await PerformSftpAsync(
                server,
                new() { SshKey = (_, found, match) => { matches.Add(match); return found.KeyType == SshKeyType.Rsa ? SshKeyStatus.Fine : SshKeyStatus.Reject; } },
                knownHosts);

            Assert.AreEqual(CurlCode.Ok, code);
            Assert.AreEqual("hello", body);
            CollectionAssert.AreEqual(new[] { expected }, matches);
        }
        finally
        {
            File.Delete(knownHosts);
        }
    }

    [TestMethod]
    [DataRow(SshKeyStatus.Reject)]
    [DataRow(SshKeyStatus.Defer)]
    public async Task PerformAsync_SshKeyCallbackRefusesAMatch_IsPeerFailedVerification(SshKeyStatus status)
    {
        InMemorySshServer server = SshServerWithFile();
        string knownHosts = WriteKnownHosts(server.KnownHostsLine("127.0.0.1"));
        try
        {
            (CurlCode code, string body) = await PerformSftpAsync(server, new() { SshKey = (_, _, _) => status }, knownHosts);

            Assert.AreEqual(CurlCode.PeerFailedVerification, code);
            Assert.AreEqual(string.Empty, body);
        }
        finally
        {
            File.Delete(knownHosts);
        }
    }

    [TestMethod]
    [DataRow(SshKeyMatch.Ok, CurlCode.Ok)]
    [DataRow(SshKeyMatch.Mismatch, CurlCode.PeerFailedVerification)]
    public async Task PerformAsync_SshHostKeyCallback_DecidesWithoutAKnownHostsFile(SshKeyMatch answer, CurlCode expected)
    {
        InMemorySshServer server = SshServerWithFile();
        List<SshKeyType> types = [];

        (CurlCode code, _) = await PerformSftpAsync(server, new() { SshHostKey = (type, _) => { types.Add(type); return answer; } }, knownHostsFile: null);

        Assert.AreEqual(expected, code);
        CollectionAssert.AreEqual(new[] { SshKeyType.Rsa }, types);
    }

    private static InMemorySshServer SshServerWithFile()
    {
        InMemorySshServer server = new(SshUser, SshPassword);
        server.Files["/f"] = "hello"u8.ToArray();
        return server;
    }

    private static string WriteKnownHosts(string line)
    {
        string path = Path.Combine(Path.GetTempPath(), $"curl-known-hosts-{Guid.NewGuid():N}");
        File.WriteAllText(path, line);
        return path;
    }

    private static async Task<(CurlCode Code, string Body)> PerformSftpAsync(InMemorySshServer server, EasyCallbackOptions callbacks, string? knownHostsFile)
    {
        StringBuilder body = new();
        await using ManagedCurlEngine engine = CreateEngine(server);
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new() { Url = "sftp://127.0.0.1:2222/f" },
            Authentication = new() { UserPassword = $"{SshUser}:{SshPassword}" },
            Protocols = new() { Ssh = new() { KnownHostsFile = knownHostsFile } },
            Callbacks = callbacks with { Write = data => { body.Append(Encoding.ASCII.GetString(data)); return data.Length; } },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);
        return (code, body.ToString());
    }
}
