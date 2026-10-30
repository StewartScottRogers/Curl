using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Pins that the handler writes the known-hosts file back through its <see cref="IFileSystem" />
/// when <see cref="SshOptions.KnownHostDecision" /> answers <c>CURLKHSTAT_FINE_ADD_TO_FILE</c>, and
/// still transfers when the write fails, as libssh2.c's <c>ssh_knownhost</c> does (ADR-0626).
/// </summary>
public sealed partial class SshProtocolHandlerTests
{
    [TestMethod]
    [DataRow(false, DisplayName = "the file is written")]
    [DataRow(true, DisplayName = "the write is refused")]
    public async Task ExecuteAsync_KnownHostDecisionAddsAMissingHost_WritesTheFileAndDownloads(bool refusesWrites)
    {
        InMemorySshServer server = Server();
        server.Files["/f"] = Hello;
        string existing = $"other.example ssh-rsa {Convert.ToBase64String(server.HostKeyBlob)}\n";
        WritableInMemoryFileSystem fileSystem = new(new Dictionary<string, string> { ["known_hosts"] = existing }) { RefusesWrites = refusesWrites };
        SshOptions options = new() { KnownHostsPath = "known_hosts", KnownHostDecision = _ => SshKnownHostVerdict.AcceptAndAddToFile };
        TranscriptTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse($"sftp://{Host}/f"),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential(User, Password),
            Ssh = options,
            Events = events,
        };
        ArrangeTransfer(context);
        SshProtocolHandler handler = new(
            server,
            fileSystem,
            SshAlgorithmPreferences.OpenSslReference,
            Encoding.UTF8,
            new SystemSshRandomSource(),
            new SystemSshEphemeralKeySource(),
            Environment.GetEnvironmentVariable,
            new UnreachableSshAgent());

        TransferResult result = await handler.ExecuteAsync(context);
        await server.WhenSessionsEndAsync();

        Diagnostics.Assert("result", TransferResult.Success(Hello.Length), result);
        Assert.AreEqual(TransferResult.Success(Hello.Length), result with { Report = null });
        string expected = existing.Replace("\n", Environment.NewLine, StringComparison.Ordinal)
            + $"{Host} ssh-rsa {Convert.ToBase64String(server.HostKeyBlob)}{Environment.NewLine}";
        Assert.AreEqual(refusesWrites ? null : expected, fileSystem.Written.TryGetValue("known_hosts", out byte[]? bytes) ? Encoding.UTF8.GetString(bytes) : null);
        Assert.AreEqual(refusesWrites, events.Transcript.Contains("* WARNING: writing known_hosts failed"));
    }
}
