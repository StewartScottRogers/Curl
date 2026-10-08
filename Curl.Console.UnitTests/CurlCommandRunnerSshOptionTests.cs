using System.Net;
using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins what an <c>scp</c> or <c>sftp</c> transfer's context carries (BL-576): the SSH options
/// verbatim in <see cref="ITransferContext.Ssh" />, <c>-u</c>, <c>-Q</c>, <c>-T</c>, <c>-r</c> and
/// <c>-C</c> in their own members, and the known-hosts file resolved as curl 8.21.0's tool does,
/// measured on 2026-09-29 with <c>HOME</c>, <c>USERPROFILE</c>, <c>CURL_HOME</c>, <c>APPDATA</c> and
/// <c>XDG_CONFIG_HOME</c> each pointed at its own directory (BL-576 Notes).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerSshOptionTests
{
    private const string Md5 = "0123456789abcdef0123456789abcdef";

    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem fileSystem = new() { ReadContent = [1, 2, 3] };
    private readonly InMemoryDataFileReader dataFiles = new();
    private readonly Dictionary<string, string> environment = [];
    private readonly RecordingProtocolHandler sftp = RecordingProtocolHandler.WritingPath("sftp");
    private readonly RecordingProtocolHandler scp = RecordingProtocolHandler.WritingPath("scp");
    private readonly RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.UTF8.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_SshOptions_ReachTheContextUnchanged()
    {
        int exitCode = await RunAsync(
            ["-k", "--key", "id key", "--pubkey", "id.pub", "--pass", "phrase", "--hostpubmd5", Md5, "--hostpubsha256", "c2hh", "--compressed-ssh", "sftp://h/f"]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        SshOptions expectedSsh = new()
        {
            PrivateKeyPath = "id key",
            PublicKeyPath = "id.pub",
            PrivateKeyPassphrase = "phrase",
            KnownHostsPath = null,
            HostPublicKeyMd5 = Md5,
            HostPublicKeySha256 = "c2hh",
            Compression = true,
        };
        Diagnostics.Assert("ssh options", expectedSsh, sftp.Contexts.Single().Ssh);
        Assert.AreEqual(expectedSsh, sftp.Contexts.Single().Ssh);
    }

    [TestMethod]
    public async Task RunAsync_NoSshOptions_CarriesCurlsNotGivenValues()
    {
        await RunAsync(["-k", "scp://h/f"]);

        Diagnostics.Assert("ssh options", new SshOptions(), scp.Contexts.Single().Ssh);
        Assert.AreEqual(new SshOptions(), scp.Contexts.Single().Ssh);
    }

    [TestMethod]
    public async Task RunAsync_UserQuoteUploadRangeAndResume_ReachTheirOwnMembers()
    {
        int exitCode = await RunAsync(
            ["-k", "-u", "tester:secret", "-Q", "rm /old", "-T", "up.txt", "-C", "2", "sftp://h/dir/up.txt"]);
        ITransferContext context = sftp.Contexts.Single();

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("credentials", ("tester", "secret"), (context.Credentials!.UserName, context.Credentials.Password));
        Assert.AreEqual(("tester", "secret"), (context.Credentials!.UserName, context.Credentials.Password));
        Diagnostics.Assert("quote commands", "rm /old", string.Join(", ", context.QuoteCommands));
        CollectionAssert.AreEqual(new[] { "rm /old" }, context.QuoteCommands.ToArray());
        Diagnostics.Assert("upload present", true, context.Upload is not null);
        Assert.IsNotNull(context.Upload);
        Diagnostics.Assert("resume from", 2L, context.ResumeFrom);
        Assert.AreEqual(2L, context.ResumeFrom);
    }

    [TestMethod]
    public async Task RunAsync_Range_ReachesTheContext()
    {
        await RunAsync(["-k", "-r", "0-4", "sftp://h/f"]);

        Diagnostics.Assert("range text", "0-4", sftp.Contexts.Single().RangeText);
        Assert.AreEqual("0-4", sftp.Contexts.Single().RangeText);
        Diagnostics.Assert("range present", true, sftp.Contexts.Single().Range is not null);
        Assert.IsNotNull(sftp.Contexts.Single().Range);
    }

    [TestMethod]
    public async Task RunAsync_OtherScheme_CarriesNoSshOptions()
    {
        await RunAsync(["--key", "k", "http://h/"]);

        Diagnostics.Assert("ssh options present", false, http.Contexts.Single().Ssh is not null);
        Assert.IsNull(http.Contexts.Single().Ssh);
    }

    [TestMethod]
    public async Task RunAsync_InsecureWithKnownHosts_ChecksNoKnownHostsFile()
    {
        // curl's tool sets CURLOPT_SSH_KNOWNHOSTS only without -k (ADR-0122: -k is a null KnownHostsPath).
        await RunAsync(["-k", "--knownhosts", Path.GetTempPath(), "sftp://h/f"]);

        Diagnostics.Assert("known hosts path", "(null)", WithoutTempPath(sftp.Contexts.Single().Ssh!.KnownHostsPath));
        Assert.IsNull(sftp.Contexts.Single().Ssh!.KnownHostsPath);
    }

    [TestMethod]
    public async Task RunAsync_KnownHostsOption_IsTheFileChecked()
    {
        string knownHosts = Path.GetTempPath();

        await RunAsync(["--knownhosts", knownHosts, "sftp://h/f"]);

        Diagnostics.Assert("known hosts path", WithoutTempPath(knownHosts), WithoutTempPath(sftp.Contexts.Single().Ssh!.KnownHostsPath));
        Assert.AreEqual(knownHosts, sftp.Contexts.Single().Ssh!.KnownHostsPath);
        Diagnostics.Assert("data files read", string.Empty, string.Join(", ", dataFiles.PathsRead));
        Assert.IsEmpty(dataFiles.PathsRead);
    }

    [TestMethod]
    [DataRow(false, "HOME", "home", "home/.ssh/known_hosts")]
    [DataRow(false, "CURL_HOME", "curlhome", "curlhome/.ssh/known_hosts")]
    [DataRow(true, "USERPROFILE", "profile", @"profile\.ssh/known_hosts")]
    [DataRow(true, "APPDATA", "appdata", @"appdata\.ssh/known_hosts")]
    public async Task RunAsync_KnownHostsInAHomeDirectory_IsTheFileChecked(bool runsOnWindows, string variable, string directory, string path)
    {
        environment[variable] = directory;
        dataFiles.Files[path] = [];

        int exitCode = await RunAsync(["-sS", "sftp://h/f"], runsOnWindows);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("known hosts path", path, sftp.Contexts.Single().Ssh!.KnownHostsPath);
        Assert.AreEqual(path, sftp.Contexts.Single().Ssh!.KnownHostsPath);
        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_KnownHostsInTheAccountHomeDirectoryOffWindows_IsTheFileChecked()
    {
        dataFiles.Files["account/.ssh/known_hosts"] = [];

        await RunAsync(["sftp://h/f"], accountHomeDirectory: "account");

        Diagnostics.Assert("known hosts path", "account/.ssh/known_hosts", sftp.Contexts.Single().Ssh!.KnownHostsPath);
        Assert.AreEqual("account/.ssh/known_hosts", sftp.Contexts.Single().Ssh!.KnownHostsPath);
    }

    [TestMethod]
    public async Task RunAsync_NoKnownHostsFile_PrintsCurlsLinesExits2AndEndsTheRun()
    {
        environment["HOME"] = "home";

        int exitCode = await RunAsync(["http://h/a", "sftp://h/f", "http://h/b"]);

        Diagnostics.Assert("exit code", 2, exitCode);
        Assert.AreEqual(2, exitCode);
        Diagnostics.Assert("sftp transfers", 0, sftp.Contexts.Count);
        Assert.IsEmpty(sftp.Contexts);
        Diagnostics.Assert("http transfers", 1, http.Contexts.Count);
        Assert.HasCount(1, http.Contexts, "curl transfers nothing after the failure");
        Diagnostics.Diff("stderr", "curl: Could not find a known_hosts file\ncurl: (2) Failed initialization\n", Lf(StandardErrorText));
        Assert.AreEqual("curl: Could not find a known_hosts file" + NewLine + "curl: (2) Failed initialization" + NewLine, StandardErrorText);
        Diagnostics.Assert("data files read", "home/.ssh/known_hosts", string.Join(", ", dataFiles.PathsRead));
        CollectionAssert.AreEqual(new[] { "home/.ssh/known_hosts" }, dataFiles.PathsRead);
    }

    [TestMethod]
    public async Task RunAsync_NoKnownHostsFileSilent_PrintsNothingAndExits2()
    {
        int exitCode = await RunAsync(["-s", "scp://h/f"]);

        Diagnostics.Assert("exit code", 2, exitCode);
        Assert.AreEqual(2, exitCode);
        Diagnostics.Assert("scp transfers", 0, scp.Contexts.Count);
        Assert.IsEmpty(scp.Contexts);
        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    [DataRow("--hostpubmd5", Md5)]
    [DataRow("--hostpubsha256", "c2hh")]
    public async Task RunAsync_NoKnownHostsFileButAFingerprint_WarnsAndTransfers(string option, string value)
    {
        int exitCode = await RunAsync([option, value, "sftp://h/f"]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("known hosts path", "(null)", sftp.Contexts.Single().Ssh!.KnownHostsPath ?? "(null)");
        Assert.IsNull(sftp.Contexts.Single().Ssh!.KnownHostsPath);
        Diagnostics.Diff("stderr", "Warning: Could not find a known_hosts file\n", Lf(StandardErrorText));
        Assert.AreEqual("Warning: Could not find a known_hosts file" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_NoKnownHostsFileButAFingerprintSilent_TransfersWithoutTheWarning()
    {
        int exitCode = await RunAsync(["-sS", "--hostpubmd5", Md5, "sftp://h/f"]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_LibcurlWithAKnownHostsFile_WritesItAfterTheSshLines()
    {
        environment["HOME"] = "home";
        dataFiles.Files["home/.ssh/known_hosts"] = [];

        await RunAsync(["-s", "--libcurl", "-", "--compressed-ssh", "sftp://h/f"]);

        Diagnostics.Assert(
            "stdout contains the compression and known hosts lines",
            true,
            StandardOutputText.Contains("  curl_easy_setopt(curl, CURLOPT_SSH_COMPRESSION, 1L);\n  curl_easy_setopt(curl, CURLOPT_SSH_KNOWNHOSTS, \"home/.ssh/known_hosts\");\n", StringComparison.Ordinal));
        Assert.Contains(
            "  curl_easy_setopt(curl, CURLOPT_SSH_COMPRESSION, 1L);\n  curl_easy_setopt(curl, CURLOPT_SSH_KNOWNHOSTS, \"home/.ssh/known_hosts\");\n",
            Encoding.UTF8.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_LibcurlWithoutAKnownHostsFile_StopsTheSourceWhereCurlFails()
    {
        int exitCode = await RunAsync(["-s", "--libcurl", "-", "sftp://h/f", "http://h/b"]);

        Diagnostics.Assert("exit code", 2, exitCode);
        Assert.AreEqual(2, exitCode);
        Diagnostics.Assert(
            "stdout ends where curl fails",
            true,
            StandardOutputText.EndsWith("  curl_easy_setopt(curl, CURLOPT_USERAGENT, \"curl/8.21.0\");\n  curl_easy_cleanup(curl);\n  curl = NULL;\n\n  return (int)result;\n}\n/**** End of sample code ****/\n", StringComparison.Ordinal));
        Assert.EndsWith(
            "  curl_easy_setopt(curl, CURLOPT_USERAGENT, \"curl/8.21.0\");\n  curl_easy_cleanup(curl);\n  curl = NULL;\n\n  return (int)result;\n}\n/**** End of sample code ****/\n",
            Encoding.UTF8.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_LibcurlUnderInsecure_WritesNoKnownHostsLine()
    {
        environment["HOME"] = "home";
        dataFiles.Files["home/.ssh/known_hosts"] = [];

        await RunAsync(["-s", "-k", "--libcurl", "-", "sftp://h/f"]);

        string source = Encoding.UTF8.GetString(standardOutput.ToArray());
        Diagnostics.Assert("source mentions KNOWNHOSTS", false, source.Contains("KNOWNHOSTS", StringComparison.Ordinal));
        Assert.DoesNotContain("KNOWNHOSTS", source);
        Diagnostics.Assert("source performs the transfer", true, source.Contains("  result = curl_easy_perform(curl);\n", StringComparison.Ordinal));
        Assert.Contains("  result = curl_easy_perform(curl);\n", source);
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    // The temporary directory differs by OS, so it is printed as <temp>/.
    private static string WithoutTempPath(string? text) =>
        text is null ? "(null)" : text.Replace(Path.GetTempPath(), "<temp>/", StringComparison.Ordinal);

    private async Task<int> RunAsync(IReadOnlyList<string> arguments, bool runsOnWindows = false, string? accountHomeDirectory = null)
    {
        Diagnostics.Arrange("arguments", WithoutTempPath(string.Join(' ', arguments)));
        Diagnostics.Arrange("runs on Windows", runsOnWindows);
        Diagnostics.Arrange("environment", string.Join(", ", environment.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value)));
        Diagnostics.Arrange("readable data files", string.Join(", ", dataFiles.Files.Keys.Order(StringComparer.Ordinal)));
        Diagnostics.Arrange("account home directory", accountHomeDirectory ?? "(none)");
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher([sftp, scp, http])),
                    fileSystem,
                    fileSystem,
                    standardOutput,
                    standardError,
                    new MemoryStream(),
                    runsOnWindows,
                    outputPaths: fileSystem,
                    configFileReader: dataFiles,
                    readEnvironmentVariable: name => environment.GetValueOrDefault(name),
                    accountHomeDirectory: accountHomeDirectory)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stdout", Lf(StandardOutputText));
        Diagnostics.Act("stderr", Lf(StandardErrorText));
        Diagnostics.Act("data files read", string.Join(", ", dataFiles.PathsRead));
        Diagnostics.Act("sftp transfers", sftp.Contexts.Count);
        Diagnostics.Act("scp transfers", scp.Contexts.Count);
        Diagnostics.Act("http transfers", http.Contexts.Count);
        return exitCode;
    }
}
