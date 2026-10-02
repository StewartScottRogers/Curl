using System.Net;
using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

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

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_SshOptions_ReachTheContextUnchanged()
    {
        int exitCode = await RunAsync(
            ["-k", "--key", "id key", "--pubkey", "id.pub", "--pass", "phrase", "--hostpubmd5", Md5, "--hostpubsha256", "c2hh", "--compressed-ssh", "sftp://h/f"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            new SshOptions
            {
                PrivateKeyPath = "id key",
                PublicKeyPath = "id.pub",
                PrivateKeyPassphrase = "phrase",
                KnownHostsPath = null,
                HostPublicKeyMd5 = Md5,
                HostPublicKeySha256 = "c2hh",
                Compression = true,
            },
            sftp.Contexts.Single().Ssh);
    }

    [TestMethod]
    public async Task RunAsync_NoSshOptions_CarriesCurlsNotGivenValues()
    {
        await RunAsync(["-k", "scp://h/f"]);

        Assert.AreEqual(new SshOptions(), scp.Contexts.Single().Ssh);
    }

    [TestMethod]
    public async Task RunAsync_UserQuoteUploadRangeAndResume_ReachTheirOwnMembers()
    {
        int exitCode = await RunAsync(
            ["-k", "-u", "tester:secret", "-Q", "rm /old", "-T", "up.txt", "-C", "2", "sftp://h/dir/up.txt"]);
        ITransferContext context = sftp.Contexts.Single();

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(("tester", "secret"), (context.Credentials!.UserName, context.Credentials.Password));
        CollectionAssert.AreEqual(new[] { "rm /old" }, context.QuoteCommands.ToArray());
        Assert.IsNotNull(context.Upload);
        Assert.AreEqual(2L, context.ResumeFrom);
    }

    [TestMethod]
    public async Task RunAsync_Range_ReachesTheContext()
    {
        await RunAsync(["-k", "-r", "0-4", "sftp://h/f"]);

        Assert.AreEqual("0-4", sftp.Contexts.Single().RangeText);
        Assert.IsNotNull(sftp.Contexts.Single().Range);
    }

    [TestMethod]
    public async Task RunAsync_OtherScheme_CarriesNoSshOptions()
    {
        await RunAsync(["--key", "k", "http://h/"]);

        Assert.IsNull(http.Contexts.Single().Ssh);
    }

    [TestMethod]
    public async Task RunAsync_InsecureWithKnownHosts_ChecksNoKnownHostsFile()
    {
        // curl's tool sets CURLOPT_SSH_KNOWNHOSTS only without -k (ADR-0122: -k is a null KnownHostsPath).
        await RunAsync(["-k", "--knownhosts", Path.GetTempPath(), "sftp://h/f"]);

        Assert.IsNull(sftp.Contexts.Single().Ssh!.KnownHostsPath);
    }

    [TestMethod]
    public async Task RunAsync_KnownHostsOption_IsTheFileChecked()
    {
        string knownHosts = Path.GetTempPath();

        await RunAsync(["--knownhosts", knownHosts, "sftp://h/f"]);

        Assert.AreEqual(knownHosts, sftp.Contexts.Single().Ssh!.KnownHostsPath);
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

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(path, sftp.Contexts.Single().Ssh!.KnownHostsPath);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_KnownHostsInTheAccountHomeDirectoryOffWindows_IsTheFileChecked()
    {
        dataFiles.Files["account/.ssh/known_hosts"] = [];

        await RunAsync(["sftp://h/f"], accountHomeDirectory: "account");

        Assert.AreEqual("account/.ssh/known_hosts", sftp.Contexts.Single().Ssh!.KnownHostsPath);
    }

    [TestMethod]
    public async Task RunAsync_NoKnownHostsFile_PrintsCurlsLinesExits2AndEndsTheRun()
    {
        environment["HOME"] = "home";

        int exitCode = await RunAsync(["http://h/a", "sftp://h/f", "http://h/b"]);

        Assert.AreEqual(2, exitCode);
        Assert.IsEmpty(sftp.Contexts);
        Assert.HasCount(1, http.Contexts, "curl transfers nothing after the failure");
        Assert.AreEqual("curl: Could not find a known_hosts file" + NewLine + "curl: (2) Failed initialization" + NewLine, StandardErrorText);
        CollectionAssert.AreEqual(new[] { "home/.ssh/known_hosts" }, dataFiles.PathsRead);
    }

    [TestMethod]
    public async Task RunAsync_NoKnownHostsFileSilent_PrintsNothingAndExits2()
    {
        int exitCode = await RunAsync(["-s", "scp://h/f"]);

        Assert.AreEqual(2, exitCode);
        Assert.IsEmpty(scp.Contexts);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    [DataRow("--hostpubmd5", Md5)]
    [DataRow("--hostpubsha256", "c2hh")]
    public async Task RunAsync_NoKnownHostsFileButAFingerprint_WarnsAndTransfers(string option, string value)
    {
        int exitCode = await RunAsync([option, value, "sftp://h/f"]);

        Assert.AreEqual(0, exitCode);
        Assert.IsNull(sftp.Contexts.Single().Ssh!.KnownHostsPath);
        Assert.AreEqual("Warning: Could not find a known_hosts file" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_NoKnownHostsFileButAFingerprintSilent_TransfersWithoutTheWarning()
    {
        int exitCode = await RunAsync(["-sS", "--hostpubmd5", Md5, "sftp://h/f"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_LibcurlWithAKnownHostsFile_WritesItAfterTheSshLines()
    {
        environment["HOME"] = "home";
        dataFiles.Files["home/.ssh/known_hosts"] = [];

        await RunAsync(["-s", "--libcurl", "-", "--compressed-ssh", "sftp://h/f"]);

        Assert.Contains(
            "  curl_easy_setopt(curl, CURLOPT_SSH_COMPRESSION, 1L);\n  curl_easy_setopt(curl, CURLOPT_SSH_KNOWNHOSTS, \"home/.ssh/known_hosts\");\n",
            Encoding.UTF8.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_LibcurlWithoutAKnownHostsFile_StopsTheSourceWhereCurlFails()
    {
        int exitCode = await RunAsync(["-s", "--libcurl", "-", "sftp://h/f", "http://h/b"]);

        Assert.AreEqual(2, exitCode);
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
        Assert.DoesNotContain("KNOWNHOSTS", source);
        Assert.Contains("  result = curl_easy_perform(curl);\n", source);
    }

    private Task<int> RunAsync(IReadOnlyList<string> arguments, bool runsOnWindows = false, string? accountHomeDirectory = null) =>
        new CurlCommandRunner(
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
