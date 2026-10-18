using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Cli;
using Curl.Conformance.Http2Server;
using Curl.Conformance.Http3Server;
using Curl.Conformance.LibTest;
using Curl.Conformance.SshServer;
using Curl.Console;
using Curl.Engine;
using Curl.Networking;
using Curl.Protocol.Ssh;
using Curl.Testing;
using Curl.Tls;
using Libcurl.Misc.ErrorText;

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

    // The stand-in for upstream's http/2 server, nghttpx serving h2c in front of sws, on %HTTP2PORT (BL-2230), and HTTP/2 over TLS on %HTTP2TLSPORT (BL-2245).
    private static readonly UpstreamHttp2Server Http2Server = new(UpstreamHttp2ServerFactory.Create, UpstreamHttp2ServerFactory.CreateTls);

    // The stand-in for upstream's http/3 server, nghttpx-h3 in front of sws, on %HTTP3PORT (BL-2261). The generated
    // test-localhost.pem holds a P-256 key, so the TLS settings sign with ECDSA; the server's own transport parameters
    // replace the ones given here.
    private static readonly UpstreamHttp3Server Http3Server = new((backend, certificate) => UpstreamHttp3ServerFactory.Create(
        backend,
        new Http3ServerTlsSettings
        {
            CertificateChain = [certificate.RawData],
            SigningKey = new EcdsaTlsSigningKey(certificate.GetECDsaPrivateKey()!),
            QuicTransportParameters = Http3ServerConnection.DefaultTransportParameters.Encode(),
        }));

    private static async Task<UpstreamCaseOutcome> RunCaseOnceAsync(int testNumber, byte[] testFile)
    {
        DirectoryInfo logDirectory = Directory.CreateDirectory(Path.Combine(LogFolder, $"test{testNumber}-{Guid.NewGuid():N}"));
        try
        {
            UpstreamCaseRunner runner = new(RunCurlAsync, PlatformFor(testFile), TimeProvider.System, TimeLimit, SshServer, Http2Server, Http3Server, libTests: LibTests);
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
    // (ADR-0493, BL-2234), and CURL_HSTS_HTTP, when set, as learning HSTS over plain http (ADR-0494, BL-2250).
    // The Alt-Svc cases that set CURL_ALTSVC_HTTP (358, 359) rely on it to learn and look up Alt-Svc over plain http:
    // the harness reports Debug to a case naming the alt-svc feature and setting CURL_ALTSVC_HTTP, and RunCurlAsync has
    // Curl honour it (ADR-0497, BL-2263). The cases against nghttpx's TLS front on %HTTP2TLSPORT (2400, 2401, 2406) name
    // Debug only because upstream runs its http/2 cases on debug builds; they use nothing debug-only, so the harness
    // reports Debug to a case whose command names %HTTP2TLSPORT. Every other case sees no Debug, so the other Debug cases stay skipped.
    // A case naming the dns server (2102 to 2104) relies on override-dns, the debug build's CURL_DNS_SERVER: the harness
    // reports it to that case alone, and RunCurlAsync has Curl ask the dnsd stand-in on %DNSPORT through the datagram connector (ADR-0492, BL-2232).
    // The AWS SigV4 libtest cases (1933 to 1938) rely on Debug for CURL_FORCETIME, which signs at the epoch: the harness
    // reports Debug to a case naming the aws feature, and CreateEngine gives the libtest engine a signing clock fixed at
    // the epoch (ADR-0671, BL-2675); the curl tool's aws cases (439, 472, 1976, 3222, 5026, 5027) get Debug too, and
    // RunCurlAsync signs an --aws-sigv4 command at the epoch. The WebSocket libtest cases (2301 to 2304) rely on Debug for
    // CURL_ENTROPY, which pins Sec-WebSocket-Key and the frame masks: the harness reports Debug to a case naming the ws
    // feature and setting CURL_ENTROPY, and CreateEngine gives the libtest engine the forced sequence (ADR-0678, BL-2688).
    private static UpstreamCurlPlatform PlatformFor(byte[] testFile)
    {
        string text = Encoding.Latin1.GetString(testFile);
        bool standsInForDebug = text.Contains("\nhttpsig\n", StringComparison.Ordinal)
            || text.Contains("\naws\n", StringComparison.Ordinal)
            || (text.Contains("\nws\n", StringComparison.Ordinal) && text.Contains("\nCURL_ENTROPY=", StringComparison.Ordinal))
            || (text.Contains("\nHSTS\n", StringComparison.Ordinal) && text.Contains("\nCURL_TIME=", StringComparison.Ordinal))
            || (text.Contains("\nalt-svc\n", StringComparison.Ordinal) && text.Contains("\nCURL_ALTSVC_HTTP=", StringComparison.Ordinal))
            || text.Contains(":%HTTP2TLSPORT/", StringComparison.Ordinal);
        UpstreamCurlPlatform platform = standsInForDebug ? Platform.WithFeatures(["Debug"]) : Platform;
        return text.Contains("\n<server>\n", StringComparison.Ordinal) && text.Contains("\ndns\n", StringComparison.Ordinal)
            ? platform.WithFeatures(["override-dns"])
            : platform;
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
                SshServer,
                Http2Server);
            UpstreamCaseOutcome outcome = await runner.RunAsync(testNumber, testFile, logDirectory.FullName, certificateDirectory: CertificateFolder.Value);

            // test471 needs http/2, which the Schannel build Curl matches on Windows lacks (ADR-0488, BL-2230).
            if (outcome.RunsOnlyOnAnotherPlatform)
            {
                Assert.Inconclusive(outcome.Detail);
            }

            Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        }
        finally
        {
            DeleteOrKeepLogDirectory(logDirectory);
        }
    }

    // A sws case whose <tool> is a test-only program doing an easy GET of its URL to stdout through the ratchet's
    // CreateEngine, so the managed engine's own transports reach the sws emulation (BL-2558, ADR-0658). Its data is
    // nocheck: how a lib case's <reply><data> is judged against the program's stdout is BL-2560's.
    private const string EasyGetLibCase =
        "<testcase>\n<reply>\n<data crlf=\"headers\" nocheck=\"yes\">\nHTTP/1.1 200 OK\nContent-Length: 6\nConnection: close\n\n-foo-\n</data>\n</reply>\n"
        + "<client>\n<server>\nhttp\n</server>\n<tool>\nlib%TESTNUMBER\n</tool>\n<name>\neasy GET through the engine\n</name>\n"
        + "<command>\nhttp://%HOSTIP:%HTTPPORT/%TESTNUMBER\n</command>\n</client>\n"
        + "<verify>\n<protocol crlf=\"yes\">\nGET /%TESTNUMBER HTTP/1.1\nHost: %HOSTIP:%HTTPPORT\nUser-Agent: lib%TESTNUMBER\nAccept: */*\n\n</protocol>\n"
        + "<stdout>\n-foo-\n</stdout>\n<errorcode>\n0\n</errorcode>\n</verify>\n</testcase>\n";

    [TestMethod]
    public async Task CreateEngine_EasyGetLibTestOnAnSwsCase_MatchesTheCaseProtocolAndStdout()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        EasyGetLibTest program = new("lib9558");
        UpstreamCaseRunner runner = new(
            _ => throw new AssertFailedException("curl must not run for a lib case"),
            Platform,
            TimeProvider.System,
            TimeLimit,
            libTests: new UpstreamLibTests(new LibTestPrograms([program]), CreateEngine));
        DirectoryInfo logDirectory = Directory.CreateDirectory(Path.Combine(LogFolder, $"test9558-{Guid.NewGuid():N}"));
        UpstreamCaseOutcome outcome;
        try
        {
            outcome = await runner.RunAsync(9558, Encoding.Latin1.GetBytes(EasyGetLibCase), logDirectory.FullName);
        }
        finally
        {
            DeleteOrKeepLogDirectory(logDirectory);
        }

        diagnostics.Act("outcome detail", outcome.Detail);
        diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Passed, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        Assert.IsInstanceOfType<ManagedCurlEngine>(program.Engine);
    }

    // Upstream's lib500 at its simplest: one easy GET of the URL, the body written to stdout, the perform's code returned.
    // It sets CURLOPT_USERAGENT to its own name, since the engine still sends the tool's curl/8.22.0 without one (BL-2559).
    private sealed class EasyGetLibTest(string programName) : ILibTest
    {
        public ICurlEngine? Engine { get; private set; }

        public string ProgramName => programName;

        public async Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
        {
            Engine = context.Engine;
            await using ICurlEngine engine = context.Engine!;
            await using IEasyTransfer easy = engine.CreateEasy();
            MemoryStream body = new();
            easy.Options = new EasyOptions
            {
                Network = new EasyNetworkOptions { Url = context.Url },
                Http = new EasyHttpOptions { UserAgent = programName },
                Callbacks = new EasyCallbackOptions { Write = data => WriteTo(body, data) },
            };
            CurlCode code = await easy.PerformAsync(cancellationToken);
            await context.StandardOutput.WriteAsync(body.ToArray(), cancellationToken);
            return LibTestResult.FromCurlCode(code);
        }

        private static long WriteTo(MemoryStream body, ReadOnlySpan<byte> data)
        {
            body.Write(data);
            return data.Length;
        }
    }

    // Curl as the command composes it, every TCP dial reaching the case's in-memory servers through
    // InMemoryServerTcpDialer, so TcpConnector's proxy tunnel and PROXY-line code stays in the path (ADR-0460).
    // The default config file is looked for only where the case's <setenv> points CURL_HOME, XDG_CONFIG_HOME or HOME (BL-1977).
    // It parses as a QUIC build, though no platform reports http/3, so upstream's http/3 cases skip as for the reference curl (ADR-0540, BL-2370).
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
            signingClock: invocation.Arguments.Any(argument => argument.StartsWith("--httpsig", StringComparison.Ordinal) || argument == "--aws-sigv4") ? new UnixEpochClock() : null,
            honoursHstsTimeVariable: true,
            honoursHstsHttpVariable: true,
            honoursAltSvcHttpVariable: true,
            honoursDnsServerVariable: true,
            parsesAsQuicBuild: true).RunAsync(invocation.Arguments);

    // A ported libtest program's engine, in place of curl (BL-2558, ADR-0658): the managed Libcurl engine, every TCP
    // dial reaching the case's in-memory servers through InMemoryServerTcpDialer, names resolved to loopback, the
    // datagram protocols on the case's datagram servers, FTP's active-mode data connections on the FTP stand-in's
    // listener (BL-2597) and the proxy environment the case's <setenv>, as RunCurlAsync composes curl. It signs AWS SigV4
    // requests and HTTP signatures at the epoch, as upstream's debug build does under the CURL_FORCETIME its test runner sets (BL-2675),
    // and answers NTLM with the hand-built context, as RunCurlAsync's usesHandBuiltNtlm does (BL-2635).
    // Under a case's CURL_ENTROPY its WebSocket key and masks follow the debug build's forced sequence (BL-2688).
    // Its handles read the case's <stdin> where libcurl reads the process's, as a curl_formadd file named "-" does (BL-2625).
    internal static ICurlEngine CreateEngine(UpstreamCurlInvocation invocation) =>
        new ManagedCurlEngine(
            TimeProvider.System,
            createDispatcher: null,
            CurlEasyErrorText.Of,
            name => invocation.EnvironmentVariables.GetValueOrDefault(name),
            new EngineDialing(new InMemoryServerTcpDialer(invocation.Connector), new LoopbackOnlyDnsResolver(), invocation.DatagramConnector, invocation.ConnectionListener, UsesHandBuiltNtlm: true),
            signingClock: new UnixEpochClock(),
            webSocketRandomSource: invocation.EnvironmentVariables.GetValueOrDefault("CURL_ENTROPY") is { } entropy ? new ForcedEntropyWebSocketRandomSource(entropy) : null,
            standardInput: invocation.StandardInput);

    // The ported libtest programs the ratchet runs a <tool> case's program from, over CreateEngine.
    private static readonly UpstreamLibTests LibTests = new(LibTestPrograms.Ported, CreateEngine);

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
