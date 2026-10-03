using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Pins the <c>--trace-config ssh</c> lines BL-1204 could not measure - a login through the
/// agent, a symbolic link in a listing and <c>-I</c> on a directory - in the order curl 8.21.0
/// (libssh2 1.11.1, WinCNG) wrote them against OpenSSH 10.2 on 2026-10-02 (BL-1207 Notes),
/// without the timing-dependent <c>block=1</c> and <c>pollset</c> lines (ADR-0372).
/// </summary>
public sealed partial class SshProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_TracedAgentLoginAfterARefusedIdentity_WritesEachIdentitysAttemptLines()
    {
        TraceSetup keyLogin = KeyLogin();
        InMemorySshServer server = keyLogin.Server;
        InMemorySshAgent agent = new InMemorySshAgent().Add(TestUserKeys.EcdsaP256Sec1, "refused").Add(TestUserKeys.RsaPkcs1, "k1");
        TraceSetup setup = new(server, new NetworkCredential(User, "wrong"), new SshOptions(), [], agent);
        string refused = $"* [SSH] [SSH_AUTH_AGENT_LIST] auth user '{User}' for key 'refused' | ";
        string accepted = $"* [SSH] [SSH_AUTH_AGENT_LIST] auth user '{User}' for key 'k1' | ";

        string lines = await RunTracedAsync("sftp://files.example/f", setup);

        StringAssert.Contains(
            lines,
            "* SSH: trying publickey authentication via agent | * [SSH] [SSH_AUTH_AGENT_INIT] -> [SSH_AUTH_AGENT_LIST] | "
            + "* [SSH] [SSH_AUTH_AGENT_LIST] -> [SSH_AUTH_AGENT] | " + refused + refused + accepted + accepted + accepted
            + $"* SSH: agent authenticated user '{User}' with key 'k1' | * [SSH] [SSH_AUTH_AGENT] -> [SSH_AUTH_DONE] | * SSH: authentication complete");
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedListingWithASymbolicLink_GoesThroughReaddirLinkForIt()
    {
        TraceSetup setup = KeyLogin();
        setup.Server.Files["/d/a"] = Hello;
        setup.Server.Files["/d/l"] = "/d/a"u8.ToArray();
        setup.Server.SymbolicLinks.Add("/d/l");

        string lines = await RunTracedAsync("sftp://files.example/d/", setup);

        string traced = string.Join(" | ", lines.Split(" | ").Where(line => !line.StartsWith("<= ", StringComparison.Ordinal)));
        StringAssert.Contains(
            traced,
            "* [SSH] [SSH_SFTP_READDIR_INIT] -> [SSH_SFTP_READDIR] | "
            + "* [SSH] [SSH_SFTP_READDIR] -> [SSH_SFTP_READDIR_BOTTOM] | * [SSH] [SSH_SFTP_READDIR_BOTTOM] -> [SSH_SFTP_READDIR] | "
            + "* [SSH] [SSH_SFTP_READDIR] -> [SSH_SFTP_READDIR_LINK] | * [SSH] [SSH_SFTP_READDIR_LINK] -> [SSH_SFTP_READDIR_BOTTOM] | "
            + "* [SSH] [SSH_SFTP_READDIR_BOTTOM] -> [SSH_SFTP_READDIR] | * [SSH] [SSH_SFTP_READDIR] -> [SSH_SFTP_READDIR_DONE]");
        CollectionAssert.Contains(setup.Server.Events.ToList(), "sftp 19 /d/l", "the link's target was asked for");
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedNoBodyOnADirectory_StatsItForTheFileTimeAndEndsInReaddirInit()
    {
        TraceSetup setup = KeyLogin();
        setup.Server.Files["/d/a"] = Hello;

        string lines = await RunTracedAsync("sftp://files.example/d/", setup, noBody: true);

        StringAssert.Contains(
            lines,
            "* [SSH] [SSH_SFTP_QUOTE_INIT] -> [SSH_SFTP_GETINFO] | * [SSH] [SSH_SFTP_GETINFO] -> [SSH_SFTP_FILETIME] | "
            + "* [SSH] [SSH_SFTP_FILETIME] -> [SSH_SFTP_TRANS_INIT] | * [SSH] [SSH_SFTP_TRANS_INIT] -> [SSH_SFTP_READDIR_INIT] | "
            + $"* [SSH] [SSH_SFTP_READDIR_INIT] -> [SSH_STOP] | {Rested} | * [SSH] DO phase is complete | {SftpDoneDone} | * Connection #0");
        CollectionAssert.Contains(setup.Server.Events.ToList(), "sftp 17 /d/", "the directory was asked for its time");
        CollectionAssert.DoesNotContain(setup.Server.Events.ToList(), "sftp 11 /d/", "the directory was not opened");
    }
}
