using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;

namespace Curl.Console;

/// <summary>
/// Pins <c>sftp://</c> and <c>scp://</c> end to end through the production composition: the
/// <see cref="Curl.Protocol.Ssh.SshProtocolHandler" /> <see cref="CurlComposition.CreateProtocolHandlers" />
/// registers, connected to the <see cref="InMemorySshServer" /> of <c>Curl.Protocol.Ssh.UnitTests</c>
/// in place of the network. The outcomes are those BL-569 and BL-574 measured from curl 8.21.0: the
/// file's bytes on standard output, nothing on standard error and exit 0, or exit 78 with curl's
/// line for a missing file.
/// </summary>
[TestClass]
public sealed class CurlCompositionSshTests
{
    private const string User = "tester";

    private const string Password = "secret";

    private static readonly byte[] Hello = "hello world"u8.ToArray();

    [TestMethod]
    [DataRow("sftp", "subsystem sftp")]
    [DataRow("scp", "exec scp -pf '/data/hello.txt'")]
    public async Task CreateRunner_SshDownload_WritesTheFileAndExitsZero(string scheme, string channelRequest)
    {
        InMemorySshServer server = new(User, Password);
        server.Files["/data/hello.txt"] = Hello;

        (int exitCode, string standardOutput, string standardError) = await RunAsync(
            server, ["-k", "-u", $"{User}:{Password}", $"{scheme}://127.0.0.1:2222/data/hello.txt"]);

        Assert.AreEqual("hello world", standardOutput);
        Assert.AreEqual(string.Empty, standardError);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(("127.0.0.1", 2222, false), (server.Targets.Single().Host, server.Targets.Single().Port, server.Targets.Single().UseTls));
        await server.WhenSessionsEndAsync();
        CollectionAssert.Contains(server.Events.ToArray(), channelRequest);
        CollectionAssert.Contains(server.Events.ToArray(), "disconnect 11 Shutdown");
    }

    [TestMethod]
    public async Task CreateRunner_SftpWithoutAPort_ConnectsToPort22()
    {
        InMemorySshServer server = new(User, Password);
        server.Files["/f"] = Hello;

        (int exitCode, _, _) = await RunAsync(server, ["-k", "-u", $"{User}:{Password}", "sftp://127.0.0.1/f"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(22, server.Targets.Single().Port);
    }

    [TestMethod]
    [DataRow("sftp", "curl: (78) Could not open remote file for reading: No such file or directory")]
    [DataRow("scp", "curl: (78) Failed to recv file")]
    public async Task CreateRunner_SshMissingFile_PrintsCurlsLineAndExits78(string scheme, string line)
    {
        InMemorySshServer server = new(User, Password);

        (int exitCode, string standardOutput, string standardError) = await RunAsync(
            server, ["-k", "-u", $"{User}:{Password}", $"{scheme}://127.0.0.1:2222/missing.txt"]);

        Assert.AreEqual(string.Empty, standardOutput);
        Assert.AreEqual(line + Environment.NewLine, standardError);
        Assert.AreEqual(78, exitCode);
    }

    [TestMethod]
    public async Task CreateRunner_KnownHostsFileListingTheServer_ChecksTheHostKeyAndDownloads()
    {
        InMemorySshServer server = new(User, Password);
        server.Files["/f"] = Hello;
        string knownHosts = Path.Combine(Path.GetTempPath(), $"curl-bl576-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(knownHosts, server.KnownHostsLine("[127.0.0.1]:2222"));
        try
        {
            (int exitCode, string standardOutput, string standardError) = await RunAsync(
                server, ["--knownhosts", knownHosts, "-u", $"{User}:{Password}", "sftp://127.0.0.1:2222/f"]);

            Assert.AreEqual("hello world", standardOutput);
            Assert.AreEqual(string.Empty, standardError);
            Assert.AreEqual(0, exitCode);
        }
        finally
        {
            File.Delete(knownHosts);
        }
    }

    [TestMethod]
    public async Task CreateRunner_NoKnownHostsFileButTheHostKeyFingerprint_WarnsAndDownloads()
    {
        InMemorySshServer server = new(User, Password);
        server.Files["/f"] = Hello;

        (int exitCode, string standardOutput, string standardError) = await RunAsync(
            server, ["--hostpubsha256", server.HostKeySha256, "-u", $"{User}:{Password}", "scp://127.0.0.1:2222/f"], silent: false);

        Assert.AreEqual("hello world", standardOutput);
        Assert.AreEqual("Warning: Could not find a known_hosts file" + Environment.NewLine, standardError);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public async Task CreateRunner_NoKnownHostsFile_FailsWithExit2BeforeConnecting()
    {
        InMemorySshServer server = new(User, Password);

        (int exitCode, string standardOutput, string standardError) = await RunAsync(server, ["sftp://127.0.0.1:2222/f"]);

        Assert.AreEqual(string.Empty, standardOutput);
        Assert.AreEqual(
            "curl: Could not find a known_hosts file" + Environment.NewLine + "curl: (2) Failed initialization" + Environment.NewLine,
            standardError);
        Assert.AreEqual(2, exitCode);
        Assert.IsEmpty(server.Targets);
    }

    [TestMethod]
    public async Task CreateRunner_Version_ListsScpAndSftp()
    {
        (int exitCode, string standardOutput, _) = await RunAsync(new InMemorySshServer(User, Password), ["-V"]);

        StringAssert.Contains(standardOutput, "Protocols: dict file ftp ftps gopher gophers http https imap imaps ldap ldaps mqtt mqtts pop3 pop3s rtsp scp sftp smtp smtps telnet tftp ws wss");
        Assert.AreEqual(0, exitCode);
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunAsync(
        InMemorySshServer server, string[] arguments, bool silent = true)
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        using MemoryStream standardInput = new();

        int exitCode = await CurlComposition
            .CreateRunner(standardOutput, standardError, standardInput, server, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"))
            .RunAsync(silent ? ["-sS", .. arguments] : arguments);

        return (exitCode, Encoding.Latin1.GetString(standardOutput.ToArray()), Encoding.UTF8.GetString(standardError.ToArray()));
    }
}
