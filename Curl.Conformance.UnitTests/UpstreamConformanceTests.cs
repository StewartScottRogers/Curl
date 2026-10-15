using System.Globalization;
using System.Text;
using Curl.Cli;
using Curl.Conformance.SshServer;
using Curl.Console;
using Curl.Networking;
using Curl.Protocol.Ssh;
using Curl.Testing;

namespace Curl.Conformance;

/// <summary>
/// Runs every vendored upstream test case through curl in process, one data row per case named
/// by its test number, as the ratchet of ADR-0013 decision 5: a case on
/// <c>PassingUpstreamCases.txt</c> must pass, and every other case is reported
/// <c>Inconclusive</c> with its first difference, its skip reason, or a note that it can be listed.
/// </summary>
[TestClass]
public sealed class UpstreamConformanceTests
{
    public TestContext TestContext { get; set; } = null!;

    // Every case passes in well under a second; the headroom is for a cold, busy CI runner
    // compiling curl's code paths for the first time while other test assemblies run (BL-1056).
    private static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(20);

    // The platform the conformance run stands for, with Perl's $^O for its precheck one-liners.
    private static readonly UpstreamCurlPlatform Platform =
        OperatingSystem.IsWindows() ? UpstreamCurlPlatform.Windows : OperatingSystem.IsMacOS() ? UpstreamCurlPlatform.MacOS : UpstreamCurlPlatform.Unix;
    private const string UpstreamTestFileExtension = ".curltest";

    // The runner fails a slow curl run itself after TimeLimit; this bounds the rest of the case
    // (expansion, screening, verification) so no row can hold up the fast suite, whatever it does.
    // Past it the case is judged failed, so only a listed case fails its row.
    private static readonly TimeSpan CaseHangLimit = TimeSpan.FromSeconds(30);

    private static readonly string UpstreamTestDataFolder = Path.Combine(AppContext.BaseDirectory, "UpstreamTestData");

    // Beside the tests rather than under the system's temporary folder, whose path can hold a
    // space that an unquoted %LOGDIR in a command would split, as upstream's relative log/ never does.
    private static readonly string LogFolder = Path.Combine(AppContext.BaseDirectory, "log");

    // The files cases read through %CERTDIR/certs/, generated once per run from the vendored
    // .prm files as upstream's genserv.pl does at build time (BL-1923); %CERTDIR is their parent.
    private static readonly Lazy<string> CertificateFolder = new(GenerateCertificates);

    private static readonly IReadOnlySet<int> PassingCases =
        UpstreamCaseRatchet.ReadPassingList(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, UpstreamCaseRatchet.PassingListFileName)));

    // Listed cases whose verdict needs the HTTPS server to speak TLS 1.3, as upstream's stunnel
    // does: test4001's ECH offer is rejected only by a TLS 1.3 server. On macOS SslStream cannot
    // serve TLS 1.3, the handshake settles on TLS 1.2 and curl ends with exit 52, so there these
    // cases are run and reported but not held to the list (BL-1949).
    private static readonly IReadOnlySet<int> NeedsTls13ServerCases = new HashSet<int> { 4001 };

    /// <summary>
    /// One row per vendored <c>test*.curltest</c> file, in test-number order, named by
    /// <see cref="UpstreamCaseDisplayName"/> and carrying one test category per server the case
    /// names, so <c>dotnet test --filter "TestCategory=ftp"</c> runs only the FTP cases.
    /// </summary>
    public static IEnumerable<TestDataRow<int>> UpstreamCases =>
        Directory.GetFiles(UpstreamTestDataFolder, $"test*{UpstreamTestFileExtension}")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Select(name => int.Parse(name["test".Length..], NumberStyles.None, CultureInfo.InvariantCulture))
            .Order()
            .Select(UpstreamCaseRow);

    private static TestDataRow<int> UpstreamCaseRow(int testNumber)
    {
        UpstreamTestCase? testCase = UpstreamTestCaseParser.Parse(File.ReadAllBytes(Path.Combine(UpstreamTestDataFolder, $"test{testNumber}{UpstreamTestFileExtension}"))).TestCase;
        return new TestDataRow<int>(testNumber)
        {
            DisplayName = UpstreamCaseDisplayName.For(testNumber, testCase),
            TestCategories = [.. UpstreamCaseDisplayName.Servers(testCase)],
        };
    }

    [TestMethod]
    [TestCategory("Conformance")]
    [DynamicData(nameof(UpstreamCases))]
    public async Task UpstreamCase_RunThroughCurl_HoldsTheRatchet(int testNumber)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        bool isListed = PassingCases.Contains(testNumber) && !(OperatingSystem.IsMacOS() && NeedsTls13ServerCases.Contains(testNumber));
        diagnostics.Arrange("upstream case number", testNumber);
        diagnostics.Arrange("case is on the passing list", isListed);
        byte[] testFile = await File.ReadAllBytesAsync(Path.Combine(UpstreamTestDataFolder, $"test{testNumber}{UpstreamTestFileExtension}"));
        UpstreamCaseOutcome outcome;
        using (diagnostics.Phase("run case through curl"))
        {
            outcome = await RunCaseOnceAsync(testNumber, testFile);
        }

        // A stall of the whole CI runner can hold a listed case past a time limit that it
        // finishes in milliseconds elsewhere - the harness's own (test1484, BL-1859) or the case's,
        // as test303's -m 8 expired before its reply arrived on Linux (BL-2093) - so a listed case
        // that fails gets one more attempt; a case that really broke fails again.
        if (isListed && outcome.Kind == UpstreamCaseOutcomeKind.Failed)
        {
            diagnostics.Act("first attempt failed", outcome.Detail);
            using (diagnostics.Phase("run case through curl again"))
            {
                outcome = await RunCaseOnceAsync(testNumber, testFile);
            }
        }

        UpstreamCaseVerdict verdict = UpstreamCaseRatchet.Judge(testNumber, outcome, isListed);
        diagnostics.Act("verdict kind", verdict.Kind);
        diagnostics.Act("verdict message", verdict.Message);
        diagnostics.Assert("verdict kind is not Fail", true, verdict.Kind != UpstreamCaseVerdictKind.Fail);
        switch (verdict.Kind)
        {
            case UpstreamCaseVerdictKind.Fail:
                Assert.Fail(verdict.Message);
                break;
            case UpstreamCaseVerdictKind.Inconclusive:
                Assert.Inconclusive(verdict.Message);
                break;
        }
    }

    // One case by number, listed or not, for a developer to debug (BL-2155): it fails unless the case
    // passes, with the ratchet's message for it, and runs only when CURL_UPSTREAM_CASE names it.
    [TestMethod]
    [TestCategory("Manual")]
    [RunsOnlyWhenAnUpstreamCaseIsNamed]
    public async Task NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        int testNumber = UpstreamCaseDebugSettings.Read(Environment.GetEnvironmentVariable).CaseNumber!.Value;
        string testFilePath = Path.Combine(UpstreamTestDataFolder, $"test{testNumber}{UpstreamTestFileExtension}");
        diagnostics.Arrange("upstream case number", testNumber);
        diagnostics.Arrange("case is on the passing list", PassingCases.Contains(testNumber));
        Assert.IsTrue(File.Exists(testFilePath), $"test{testNumber} is not a vendored upstream case: {testFilePath} does not exist");
        byte[] testFile = await File.ReadAllBytesAsync(testFilePath);

        UpstreamCaseOutcome outcome = await RunCaseOnceAsync(testNumber, testFile);

        UpstreamCaseVerdict verdict = UpstreamCaseRatchet.Judge(testNumber, outcome, PassingCases.Contains(testNumber));
        diagnostics.Act("verdict kind", verdict.Kind);
        diagnostics.Act("verdict message", verdict.Message);
        diagnostics.Assert("outcome kind is Passed", UpstreamCaseOutcomeKind.Passed, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, verdict.Message);
    }

    // Cases whose <client><setenv> the runner now acts on (BL-1892): 63, 288, 708, 1101, 1249,
    // 1250 and 1265 set curl's proxy variables, 392 and 1136 TZ, 1143 MSYS2_ARG_CONV_EXCL; 428 shows a real
    // Curl difference (--variable %NAME imports the process environment, not the run's).
    [TestMethod]
    [TestCategory("Conformance")]
    [DataRow(63)]
    [DataRow(288)]
    [DataRow(392)]
    [DataRow(708)]
    [DataRow(1101)]
    [DataRow(1136)]
    [DataRow(1143)]
    [DataRow(1249)]
    [DataRow(1250)]
    [DataRow(1265)]
    [DataRow(428)]
    public async Task SetenvCase_RunThroughCurl_IsMeasuredNotSkipped(int testNumber)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("upstream case number", testNumber);
        byte[] testFile = await File.ReadAllBytesAsync(Path.Combine(UpstreamTestDataFolder, $"test{testNumber}{UpstreamTestFileExtension}"));

        UpstreamCaseOutcome outcome = await RunCaseOnceAsync(testNumber, testFile);

        diagnostics.Act("outcome kind", outcome.Kind);
        diagnostics.Act("outcome detail", outcome.Detail);
        diagnostics.Assert("outcome kind is not Skipped", true, outcome.Kind != UpstreamCaseOutcomeKind.Skipped);
        Assert.AreNotEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind, outcome.Detail);
    }

    // Cases whose %PERL precheck or postcheck the runner now runs without Perl (BL-1894): 762,
    // 1026, 1027, 1082, 1291, 1443 and 1683 run a -e one-liner, 1445 test613.pl; 8 shows a real
    // Curl difference (its Cookie header) and 2072's precheck runs and, on Windows, skips with the
    // one-liner's own "Test requires a Unix system", so it is not pinned here.
    [TestMethod]
    [TestCategory("Conformance")]
    [DataRow(8)]
    [DataRow(762)]
    [DataRow(1026)]
    [DataRow(1027)]
    [DataRow(1082)]
    [DataRow(1291)]
    [DataRow(1443)]
    [DataRow(1445)]
    [DataRow(1683)]
    public async Task PerlCheckCase_RunThroughCurl_IsMeasuredNotSkipped(int testNumber)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("upstream case number", testNumber);
        byte[] testFile = await File.ReadAllBytesAsync(Path.Combine(UpstreamTestDataFolder, $"test{testNumber}{UpstreamTestFileExtension}"));

        UpstreamCaseOutcome outcome = await RunCaseOnceAsync(testNumber, testFile);

        diagnostics.Act("outcome kind", outcome.Kind);
        diagnostics.Act("outcome detail", outcome.Detail);
        Assert.AreNotEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind, outcome.Detail);
    }

    // The mail cases that ask for TLS (BL-1914): 987, 988 and 989 reach the smtps, imaps and pop3s
    // stand-ins behind implicit TLS; 980, 981, 982, 984 and 985 ask for STARTTLS with --ssl or
    // --ssl-reqd from a plain server that, as ftpserver.pl, does not offer it (ADR-0459).
    [TestMethod]
    [TestCategory("Conformance")]
    [DataRow(987)]
    [DataRow(988)]
    [DataRow(989)]
    [DataRow(980)]
    [DataRow(981)]
    [DataRow(982)]
    [DataRow(984)]
    [DataRow(985)]
    public async Task MailTlsCase_RunThroughCurl_IsMeasuredNotSkipped(int testNumber)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("upstream case number", testNumber);
        byte[] testFile = await File.ReadAllBytesAsync(Path.Combine(UpstreamTestDataFolder, $"test{testNumber}{UpstreamTestFileExtension}"));

        UpstreamCaseOutcome outcome = await RunCaseOnceAsync(testNumber, testFile);

        diagnostics.Act("outcome kind", outcome.Kind);
        diagnostics.Act("outcome detail", outcome.Detail);
        Assert.AreNotEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind, outcome.Detail);
    }

    // SSH cases that end at authentication, a refused login or the host key check (BL-1916).
    [TestMethod]
    [DataRow(606)]
    [DataRow(607)]
    [DataRow(628)]
    [DataRow(629)]
    [DataRow(630)]
    [DataRow(631)]
    [DataRow(656)]
    public async Task SshAuthenticationCase_RunThroughCurl_IsMeasuredNotSkipped(int testNumber)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("upstream case number", testNumber);
        byte[] testFile = await File.ReadAllBytesAsync(Path.Combine(UpstreamTestDataFolder, $"test{testNumber}{UpstreamTestFileExtension}"));

        UpstreamCaseOutcome outcome = await RunCaseOnceAsync(testNumber, testFile);

        diagnostics.Act("outcome kind", outcome.Kind);
        diagnostics.Act("outcome detail", outcome.Detail);
        Assert.AreNotEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind, outcome.Detail);
    }

    // SCP downloads and uploads the SSH stand-in's scp serves (BL-1917).
    [TestMethod]
    [DataRow(601)]
    [DataRow(603)]
    [DataRow(605)]
    [DataRow(617)]
    [DataRow(619)]
    [DataRow(621)]
    [DataRow(623)]
    [DataRow(641)]
    [DataRow(665)]
    [DataRow(3022)]
    public async Task ScpTransferCase_RunThroughCurl_IsMeasuredNotSkipped(int testNumber)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("upstream case number", testNumber);
        byte[] testFile = await File.ReadAllBytesAsync(Path.Combine(UpstreamTestDataFolder, $"test{testNumber}{UpstreamTestFileExtension}"));

        UpstreamCaseOutcome outcome = await RunCaseOnceAsync(testNumber, testFile);

        diagnostics.Act("outcome kind", outcome.Kind);
        diagnostics.Act("outcome detail", outcome.Detail);
        Assert.AreNotEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind, outcome.Detail);
        Assert.IsFalse(outcome.Detail?.Contains(CaseHangLimitMessage, StringComparison.Ordinal) ?? false, outcome.Detail);
    }

    // SFTP transfers and quote commands the SSH stand-in's sftp-server serves (BL-1918).
    [TestMethod]
    [DataRow(600)]
    [DataRow(602)]
    [DataRow(604)]
    [DataRow(608)]
    [DataRow(609)]
    [DataRow(611)]
    [DataRow(612)]
    [DataRow(614)]
    [DataRow(615)]
    [DataRow(616)]
    [DataRow(618)]
    [DataRow(620)]
    [DataRow(622)]
    [DataRow(624)]
    [DataRow(625)]
    [DataRow(626)]
    [DataRow(627)]
    [DataRow(633)]
    [DataRow(634)]
    [DataRow(635)]
    [DataRow(636)]
    [DataRow(637)]
    [DataRow(638)]
    [DataRow(639)]
    [DataRow(640)]
    [DataRow(642)]
    [DataRow(664)]
    [DataRow(1446)]
    [DataRow(1583)]
    [DataRow(2004)]
    [DataRow(2007)]
    [DataRow(3021)]
    public async Task SftpTransferCase_RunThroughCurl_IsMeasuredNotSkipped(int testNumber)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("upstream case number", testNumber);
        byte[] testFile = await File.ReadAllBytesAsync(Path.Combine(UpstreamTestDataFolder, $"test{testNumber}{UpstreamTestFileExtension}"));

        UpstreamCaseOutcome outcome = await RunCaseOnceAsync(testNumber, testFile);

        diagnostics.Act("outcome kind", outcome.Kind);
        diagnostics.Act("outcome detail", outcome.Detail);
        Assert.AreNotEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind, outcome.Detail);
        Assert.IsFalse(outcome.Detail?.Contains(CaseHangLimitMessage, StringComparison.Ordinal) ?? false, outcome.Detail);
    }

    // Every vendored case naming an SSH variable gets a value for it, so none skips for one (BL-1954).
    [TestMethod]
    public async Task SshVariableCases_RunThroughCurl_NoneSkipsForAnSshVariable()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string[] sshVariables = ["%USER", "%SFTP_PWD", "%SCP_PWD", "%SSHPORT", "%SSHSRVMD5", "%SSHSRVSHA256"];
        List<string> skippedForAVariable = [];
        foreach (string path in Directory.GetFiles(UpstreamTestDataFolder, $"test*{UpstreamTestFileExtension}"))
        {
            byte[] testFile = await File.ReadAllBytesAsync(path);
            string text = Encoding.Latin1.GetString(testFile);
            if (sshVariables.Any(variable => text.Contains(variable, StringComparison.Ordinal)))
            {
                int testNumber = int.Parse(Path.GetFileNameWithoutExtension(path)["test".Length..], CultureInfo.InvariantCulture);
                UpstreamCaseOutcome outcome = await RunCaseOnceAsync(testNumber, testFile);
                if (outcome.Kind == UpstreamCaseOutcomeKind.Skipped && sshVariables.Any(variable => outcome.Detail!.Contains(variable, StringComparison.Ordinal)))
                {
                    skippedForAVariable.Add($"test{testNumber}: {outcome.Detail}");
                }
            }
        }

        diagnostics.Act("cases skipped for an SSH variable", string.Join("; ", skippedForAVariable));
        Assert.IsEmpty(skippedForAVariable, string.Join("; ", skippedForAVariable));
    }

    // The stand-in for upstream's test sshd, on %SSHPORT, for the platform's curl build:
    // the RSA keys for curl's WinCNG build on Windows, the Ed25519 ones elsewhere (BL-1954, BL-2217).
    private static readonly UpstreamSshServer SshServer = UpstreamSshServerFactory.Create(OperatingSystem.IsWindows());

    private static async Task<UpstreamCaseOutcome> RunCaseOnceAsync(int testNumber, byte[] testFile)
    {
        DirectoryInfo logDirectory = Directory.CreateDirectory(Path.Combine(LogFolder, $"test{testNumber}-{Guid.NewGuid():N}"));
        try
        {
            UpstreamCaseRunner runner = new(RunCurlAsync, PlatformFor(testFile), TimeProvider.System, TimeLimit, SshServer);
            return await Task.Run(() => runner.RunAsync(testNumber, testFile, logDirectory.FullName, certificateDirectory: CertificateFolder.Value, sourceDirectory: UpstreamTestDataFolder)).WaitAsync(CaseHangLimit);
        }
        catch (TimeoutException)
        {
            // A failure of the case, judged like any other: a listed case fails the row, an
            // unlisted one is Inconclusive, so a loaded machine stretching an unlisted case's real
            // retry waits (test3035, BL-1359) cannot fail the fast suite.
            return UpstreamCaseOutcome.Failed(CaseHangLimitMessage);
        }
        finally
        {
            DeleteOrKeepLogDirectory(logDirectory);
        }
    }

    // Upstream's httpsig cases rely on the Debug feature, which lets a test fix the signing time at
    // created=0 (CURL_FORCETIME with CURL_HTTPSIG_CREATED). The harness stands in for it: it reports Debug to a
    // case that also names httpsig, and RunCurlAsync signs those commands with a clock fixed at the epoch (BL-2183).
    // The HSTS cases that fix the clock with CURL_TIME (446, 780-783) rely on it too: the harness reports Debug to a
    // case naming the HSTS feature and setting CURL_TIME, and RunCurlAsync has Curl honour CURL_TIME as the HSTS clock
    // (ADR-0493, BL-2234). Every other case sees no Debug, so the other Debug cases stay skipped.
    private static UpstreamCurlPlatform PlatformFor(byte[] testFile)
    {
        string text = Encoding.Latin1.GetString(testFile);
        bool standsInForDebug = text.Contains("\nhttpsig\n", StringComparison.Ordinal)
            || (text.Contains("\nHSTS\n", StringComparison.Ordinal) && text.Contains("\nCURL_TIME=", StringComparison.Ordinal));
        return standsInForDebug ? Platform.WithFeatures(["Debug"]) : Platform;
    }

    private static readonly string CaseHangLimitMessage =
        $"the case did not finish within {CaseHangLimit.TotalSeconds.ToString(CultureInfo.InvariantCulture)} seconds";

    // GF-0002 (BL-1998): a tool outside the test projects, such as the gap office's, reaches Curl only
    // through the public InProcessCurl, over the in-memory connector or the production TcpConnector with
    // only the dial replaced, so the connection-reuse cases must pass both ways too.
    [TestMethod]
    [DataRow(48, false)]
    [DataRow(48, true)]
    [DataRow(338, false)]
    [DataRow(338, true)]
    [DataRow(435, false)]
    [DataRow(435, true)]
    [DataRow(471, false)]
    [DataRow(471, true)]
    [DataRow(1074, false)]
    [DataRow(1074, true)]
    [DataRow(1134, false)]
    [DataRow(1134, true)]
    [DataRow(1418, false)]
    [DataRow(1418, true)]
    [DataRow(1419, false)]
    [DataRow(1419, true)]
    [DataRow(1421, false)]
    [DataRow(1421, true)]
    [DataRow(1479, false)]
    [DataRow(1479, true)]
    public async Task InProcessCurl_ConnectionReuseCase_Passes(int testNumber, bool dialsThroughTheTcpConnector)
    {
        byte[] testFile = await File.ReadAllBytesAsync(Path.Combine(UpstreamTestDataFolder, $"test{testNumber}{UpstreamTestFileExtension}"));
        DirectoryInfo logDirectory = Directory.CreateDirectory(Path.Combine(LogFolder, $"test{testNumber}-{Guid.NewGuid():N}"));
        try
        {
            UpstreamCaseRunner runner = new(
                invocation => dialsThroughTheTcpConnector
                    ? InProcessCurl.RunAsync(invocation.Arguments, invocation.StandardOutput, invocation.StandardError, invocation.StandardInput, new InMemoryServerTcpDialer(invocation.Connector), new LoopbackOnlyDnsResolver(), invocation.DatagramConnector)
                    : InProcessCurl.RunAsync(invocation.Arguments, invocation.StandardOutput, invocation.StandardError, invocation.StandardInput, invocation.Connector, invocation.DatagramConnector),
                Platform,
                TimeProvider.System,
                TimeLimit,
                SshServer);
            UpstreamCaseOutcome outcome = await runner.RunAsync(testNumber, testFile, logDirectory.FullName, certificateDirectory: CertificateFolder.Value);

            Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        }
        finally
        {
            DeleteOrKeepLogDirectory(logDirectory);
        }
    }

    // Curl as the command composes it, every TCP dial reaching the case's in-memory servers through
    // InMemoryServerTcpDialer, so TcpConnector's proxy tunnel and PROXY-line code stays in the path (ADR-0460).
    // The default config file is looked for only where the case's <setenv> points CURL_HOME, XDG_CONFIG_HOME or HOME (BL-1977).
    internal static Task<int> RunCurlAsync(UpstreamCurlInvocation invocation) =>
        CurlComposition.CreateRunner(
            invocation.StandardOutput,
            invocation.StandardError,
            invocation.StandardInput,
            new InMemoryServerTcpDialer(invocation.Connector),
            new LoopbackOnlyDnsResolver(),
            invocation.DatagramConnector,
            writesProgressMeter: true,
            writeOutFileOpener: new DiskWriteOutFileOpener(writesLineFeedAsCrLf: OperatingSystem.IsWindows()),
            usesHandBuiltNtlm: true,
            readEnvironmentVariable: name => invocation.EnvironmentVariables.GetValueOrDefault(name),
            ftpListener: invocation.ConnectionListener,
            defaultConfigFileSearch: new DefaultConfigFileSearch(name => invocation.EnvironmentVariables.GetValueOrDefault(name), OperatingSystem.IsWindows(), executableDirectory: null, accountHomeDirectory: null),
            signingClock: invocation.Arguments.Any(argument => argument.StartsWith("--httpsig", StringComparison.Ordinal)) ? new UnixEpochClock() : null,
            honoursHstsTimeVariable: true).RunAsync(invocation.Arguments);

    // The signing time of upstream's debug build under CURL_FORCETIME and CURL_HTTPSIG_CREATED=0.
    private sealed class UnixEpochClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }

    private static string GenerateCertificates() => UpstreamCertificateFolder.Prepare(
        Path.Combine(UpstreamTestDataFolder, "certs"), Path.Combine(AppContext.BaseDirectory, "UpstreamCertificates"), TimeProvider.System);

    // A run that timed out may still hold a file open; the temporary folder is left to the system then.
    // With CURL_UPSTREAM_KEEP_LOGS=1 the folder is kept and its path printed to the test's output (BL-2155).
    private static void DeleteOrKeepLogDirectory(DirectoryInfo logDirectory)
    {
        if (UpstreamCaseDebugSettings.Read(Environment.GetEnvironmentVariable).KeepsLogs)
        {
            System.Console.WriteLine(UpstreamCaseDebugSettings.KeptLogFolderLine(logDirectory.FullName));
            return;
        }

        try
        {
            logDirectory.Delete(recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
