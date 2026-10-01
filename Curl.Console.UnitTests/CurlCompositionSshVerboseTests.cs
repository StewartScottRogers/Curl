using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;

namespace Curl.Console;

/// <summary>
/// Pins <c>-v</c> and <c>--trace-ascii</c> for <c>sftp://</c> and <c>scp://</c> end to end
/// through the production composition against the <see cref="InMemorySshServer" />, with the
/// fixed test keys of <c>Curl.Protocol.Ssh.UnitTests</c>. The lines are those curl 8.21.0
/// wrote against OpenSSH 10.2 on 2026-09-29 (BL-578, ADR-0262), normalised as the other
/// <c>-v</c> tests are: the in-memory connector writes no <c>Trying</c> or
/// <c>Established connection</c> line, new lines are <c>\n</c>, and the key files, the
/// known-hosts file and the host key are this test's. The cryptography backend is the
/// platform build's: <c>WinCNG</c> on Windows, <c>OpenSSL</c> elsewhere.
/// </summary>
[TestClass]
public sealed class CurlCompositionSshVerboseTests
{
    private const string User = "tester";

    private const string Password = "secret";

    private const string Url = "sftp://127.0.0.1:2222/data/hello.txt";

    private static readonly string Backend = OperatingSystem.IsWindows() ? "WinCNG" : "OpenSSL";

    private static readonly string Start = $"* SSH: libssh2 cryptography backend: {Backend}\n* SSH: user 'tester'\n";

    // WinCNG's libssh2 cannot say why the public key was not derived; OpenSSL's says the
    // missing private key file did not open (BL-990).
    private static readonly string MissingKeyDenied = OperatingSystem.IsWindows()
        ? "Reason unknown (-1)"
        : "Unable to extract public key from private key file: Unable to open private key file";

    [TestMethod]
    public async Task CreateRunner_SftpDownloadWithAPassword_WritesCurlsVerboseLines()
    {
        using TemporaryFiles files = new();
        string key = files.PathOf("id_missing");

        (int exitCode, string standardOutput, string standardError) = await RunAsync(
            Server(), ["-v", "-sS", "-k", "--key", key, "-u", $"{User}:{Password}", Url]);

        Assert.AreEqual(
            Start
            + "* SSH: no knownhosts file configured\n"
            + "* SSH: host offers authentication via: publickey,password\n"
            + $"* SSH: trying private key file '{key}'\n"
            + $"* SSH: publickey authentication denied: {MissingKeyDenied}\n"
            + "* SSH: initialized password authentication\n"
            + "* SSH: authentication complete\n"
            + "{ [11 bytes data]\n"
            + "* Connection #0 to host 127.0.0.1:2222 left intact\n",
            standardError);
        Assert.AreEqual("hello world", standardOutput);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public async Task CreateRunner_SftpDownloadWithAPublicKey_WritesCurlsVerboseLines()
    {
        using TemporaryFiles files = new();
        (string key, string publicKey) = files.UserKeys();

        (int exitCode, _, string standardError) = await RunAsync(
            Server(authorizesTheTestKey: true), ["-v", "-sS", "-k", "--key", key, "--pubkey", publicKey, "-u", $"{User}:", Url]);

        Assert.AreEqual(
            Start
            + "* SSH: no knownhosts file configured\n"
            + "* SSH: host offers authentication via: publickey,password\n"
            + $"* SSH: trying public key file '{publicKey}'\n"
            + $"* SSH: trying private key file '{key}'\n"
            + "* SSH: authenticated via publickey\n"
            + "* SSH: authentication complete\n"
            + "{ [11 bytes data]\n"
            + "* Connection #0 to host 127.0.0.1:2222 left intact\n",
            standardError);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public async Task CreateRunner_ScpDownload_WritesCurlsVerboseLinesWithTheConnectionEstablished()
    {
        using TemporaryFiles files = new();
        (string key, string publicKey) = files.UserKeys();

        (int exitCode, string standardOutput, string standardError) = await RunAsync(
            Server(authorizesTheTestKey: true), ["-v", "-sS", "-k", "--key", key, "--pubkey", publicKey, "-u", $"{User}:", "scp://127.0.0.1:2222/data/hello.txt"]);

        Assert.AreEqual(
            Start
            + "* SSH: no knownhosts file configured\n"
            + "* SSH: host offers authentication via: publickey,password\n"
            + $"* SSH: trying public key file '{publicKey}'\n"
            + $"* SSH: trying private key file '{key}'\n"
            + "* SSH: authenticated via publickey\n"
            + "* SSH: authentication complete\n"
            + "* SSH: connection established\n"
            + "{ [11 bytes data]\n"
            + "* Connection #0 to host 127.0.0.1:2222 left intact\n",
            standardError);
        Assert.AreEqual("hello world", standardOutput);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public async Task CreateRunner_HostKeyMismatch_WritesTheCheckAndClosesTheConnectionAsMeasured()
    {
        using TemporaryFiles files = new();
        string otherKey = TestUserKeys.RsaPublicKeyFile.Split(' ')[1];
        string knownHosts = files.Write("known_hosts", $"[127.0.0.1]:2222 ssh-rsa {otherKey}\n");

        (int exitCode, string standardOutput, string standardError) = await RunAsync(
            Server(), ["-v", "-sS", "--knownhosts", knownHosts, "-u", $"{User}:{Password}", Url]);

        Assert.AreEqual(
            Start
            + $"* SSH: found host '127.0.0.1' in '{knownHosts}'\n"
            + "* SSH: set 'rsa-sha2-256,rsa-sha2-512,ssh-rsa' as hostkey type\n"
            + $"* SSH: host check 1, key: {otherKey}\n"
            + "* SSH: knownhost check failed\n"
            + "* closing connection #0\n"
            + "curl: (60) SSL peer certificate or SSH remote key was not OK\n"
            + "More details here: https://curl.se/docs/sslcerts.html\n\n"
            + "curl failed to verify the legitimacy of the server and therefore could not\n"
            + "establish a secure connection to it. To learn more about this situation and\n"
            + "how to fix it, please visit the webpage mentioned above.\n",
            standardError);
        Assert.AreEqual(string.Empty, standardOutput);
        Assert.AreEqual(60, exitCode);
    }

    [TestMethod]
    public async Task CreateRunner_SftpTraceAscii_WritesTheLinesAndTheReceivedDataAsMeasured()
    {
        using TemporaryFiles files = new();
        (string key, string publicKey) = files.UserKeys();

        (int exitCode, string standardOutput, string standardError) = await RunAsync(
            Server(authorizesTheTestKey: true), ["--trace-ascii", "-", "-sS", "-k", "--key", key, "--pubkey", publicKey, "-u", $"{User}:", Url]);

        Assert.AreEqual(
            Start
            + "* SSH: no knownhosts file configured\n"
            + "* SSH: host offers authentication via: publickey,password\n"
            + $"* SSH: trying public key file '{publicKey}'\n"
            + $"* SSH: trying private key file '{key}'\n"
            + "* SSH: authenticated via publickey\n"
            + "* SSH: authentication complete\n"
            + "<= Recv data, 11 bytes (0xb)\n"
            + "0000: hello world\n"
            + "hello world"
            + "* Connection #0 to host 127.0.0.1:2222 left intact\n",
            standardOutput);
        Assert.AreEqual(string.Empty, standardError);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    [DataRow("sftp", "", DisplayName = "sftp")]
    [DataRow("scp", "* SSH: connection established\n", DisplayName = "scp")]
    public async Task CreateRunner_UploadWithAPublicKey_WritesTheSentDataAndTheUploadSentOffAsMeasured(string scheme, string established)
    {
        using TemporaryFiles files = new();
        (string key, string publicKey) = files.UserKeys();
        string source = files.Write("up.txt", "hello upload\n");

        (int exitCode, string standardOutput, string standardError) = await RunAsync(
            Server(authorizesTheTestKey: true), ["-v", "-sS", "-k", "--key", key, "--pubkey", publicKey, "-u", $"{User}:", "-T", source, $"{scheme}://127.0.0.1:2222/data/up.txt"]);

        Assert.AreEqual(
            Start
            + "* SSH: no knownhosts file configured\n"
            + "* SSH: host offers authentication via: publickey,password\n"
            + $"* SSH: trying public key file '{publicKey}'\n"
            + $"* SSH: trying private key file '{key}'\n"
            + "* SSH: authenticated via publickey\n"
            + "* SSH: authentication complete\n"
            + established
            + "} [13 bytes data]\n"
            + "* upload completely sent off: 13 bytes\n"
            + "* Connection #0 to host 127.0.0.1:2222 left intact\n",
            standardError);
        Assert.AreEqual(string.Empty, standardOutput);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    [DataRow("sftp", "", DisplayName = "sftp")]
    [DataRow("scp", "* SSH: connection established\n", DisplayName = "scp")]
    public async Task CreateRunner_UploadTraceAscii_WritesTheSentDataAsMeasured(string scheme, string established)
    {
        using TemporaryFiles files = new();
        (string key, string publicKey) = files.UserKeys();
        string source = files.Write("up.txt", "hello upload\n");

        (int exitCode, string standardOutput, string standardError) = await RunAsync(
            Server(authorizesTheTestKey: true), ["--trace-ascii", "-", "-sS", "-k", "--key", key, "--pubkey", publicKey, "-u", $"{User}:", "-T", source, $"{scheme}://127.0.0.1:2222/data/up.txt"]);

        Assert.AreEqual(
            Start
            + "* SSH: no knownhosts file configured\n"
            + "* SSH: host offers authentication via: publickey,password\n"
            + $"* SSH: trying public key file '{publicKey}'\n"
            + $"* SSH: trying private key file '{key}'\n"
            + "* SSH: authenticated via publickey\n"
            + "* SSH: authentication complete\n"
            + established
            + "=> Send data, 13 bytes (0xd)\n"
            + "0000: hello upload.\n"
            + "* upload completely sent off: 13 bytes\n"
            + "* Connection #0 to host 127.0.0.1:2222 left intact\n",
            standardOutput);
        Assert.AreEqual(string.Empty, standardError);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public async Task CreateRunner_ScpDownloadWithKeyboardInteractive_WritesTheAgentThenTheMethodAsMeasured()
    {
        using TemporaryFiles files = new();
        string key = files.PathOf("id_missing");
        InMemorySshServer server = new(User, Password) { OffersKeyboardInteractive = true };
        server.Files["/data/hello.txt"] = "hello world"u8.ToArray();

        (int exitCode, string standardOutput, string standardError) = await RunWithoutAgentAsync(
            server, ["-v", "-sS", "-k", "--key", key, "-u", $"{User}:{Password}", "scp://127.0.0.1:2222/data/hello.txt"]);

        Assert.AreEqual(
            Start
            + "* SSH: no knownhosts file configured\n"
            + "* SSH: host offers authentication via: publickey,keyboard-interactive\n"
            + $"* SSH: trying private key file '{key}'\n"
            + $"* SSH: publickey authentication denied: {MissingKeyDenied}\n"
            + "* SSH: trying publickey authentication via agent\n"
            + "* SSH: failure connecting to agent\n"
            + "* SSH: initialized keyboard interactive authentication\n"
            + "* SSH: authentication complete\n"
            + "* SSH: connection established\n"
            + "{ [11 bytes data]\n"
            + "* Connection #0 to host 127.0.0.1:2222 left intact\n",
            standardError);
        Assert.AreEqual("hello world", standardOutput);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public async Task CreateRunner_ScpFileShort_WritesTheFailureAndClosesTheConnectionAsMeasured()
    {
        using TemporaryFiles files = new();
        string key = files.PathOf("id_missing");

        (int exitCode, string standardOutput, string standardError) = await RunAsync(
            ShortFileServer(), ["-v", "-sS", "-k", "--key", key, "-u", $"{User}:{Password}", "scp://127.0.0.1:2222/data/x"]);

        StringAssert.EndsWith(
            standardError,
            "* SSH: authentication complete\n"
            + "* SSH: connection established\n"
            + "{ [5 bytes data]\n"
            + "* end of response with 5 bytes missing\n"
            + "* closing connection #0\n"
            + "curl: (18) end of response with 5 bytes missing\n");
        Assert.AreEqual("01234", standardOutput);
        Assert.AreEqual(18, exitCode);
    }

    [TestMethod]
    public async Task CreateRunner_ScpFileShortTraceAscii_WritesTheChannelsEndAsEmptyDataAsMeasured()
    {
        using TemporaryFiles files = new();
        string key = files.PathOf("id_missing");

        (int exitCode, string standardOutput, string standardError) = await RunAsync(
            ShortFileServer(), ["--trace-ascii", "-", "-sS", "-k", "--key", key, "-u", $"{User}:{Password}", "scp://127.0.0.1:2222/data/x"]);

        StringAssert.EndsWith(
            standardOutput,
            "* SSH: connection established\n"
            + "<= Recv data, 5 bytes (0x5)\n"
            + "0000: 01234\n"
            + "01234"
            + "<= Recv data, 0 bytes (0x0)\n"
            + "* end of response with 5 bytes missing\n"
            + "* closing connection #0\n");
        Assert.AreEqual("curl: (18) end of response with 5 bytes missing\n", standardError);
        Assert.AreEqual(18, exitCode);
    }

    // Measured against a scripted scp announcing 10 bytes and sending 5.
    private static InMemorySshServer ShortFileServer()
    {
        InMemorySshServer server = new(User, Password) { ScpFileShortBy = 5 };
        server.Files["/data/x"] = "01234"u8.ToArray();
        return server;
    }

    private static InMemorySshServer Server(bool authorizesTheTestKey = false)
    {
        byte[]? publicKey = authorizesTheTestKey ? Convert.FromBase64String(TestUserKeys.RsaPublicKeyFile.Split(' ')[1]) : null;
        InMemorySshServer server = new(User, Password) { AuthorizedPublicKey = publicKey };
        server.Files["/data/hello.txt"] = "hello world"u8.ToArray();
        return server;
    }

    // SSH_AUTH_SOCK names no agent for the run, so the result does not
    // depend on whether the machine runs one; no other test here reaches the agent step.
    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunWithoutAgentAsync(InMemorySshServer server, string[] arguments)
    {
        string? agentSocket = Environment.GetEnvironmentVariable("SSH_AUTH_SOCK");
        Environment.SetEnvironmentVariable("SSH_AUTH_SOCK", Path.Combine(Path.GetTempPath(), $"curl-bl988-no-agent-{Guid.NewGuid():N}"));
        try
        {
            return await RunAsync(server, arguments);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SSH_AUTH_SOCK", agentSocket);
        }
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunAsync(InMemorySshServer server, string[] arguments)
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        using MemoryStream standardInput = new();

        int exitCode = await CurlComposition
            .CreateRunner(standardOutput, standardError, standardInput, server, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"))
            .RunAsync(arguments);
        await server.WhenSessionsEndAsync();

        return (exitCode, Normalised(standardOutput), Normalised(standardError));
    }

    private static string Normalised(MemoryStream stream) =>
        Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal);

    // A directory of its own for one test's key and known-hosts files, deleted afterwards.
    private sealed class TemporaryFiles : IDisposable
    {
        private readonly string directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"curl-bl578-{Guid.NewGuid():N}")).FullName;

        public string PathOf(string name) => Path.Combine(directory, name);

        public string Write(string name, string text)
        {
            string path = PathOf(name);
            File.WriteAllText(path, text);
            return path;
        }

        public (string Key, string PublicKey) UserKeys() =>
            (Write("id_test", TestUserKeys.RsaPkcs1), Write("id_test.pub", TestUserKeys.RsaPublicKeyFile));

        public void Dispose() => Directory.Delete(directory, recursive: true);
    }
}
