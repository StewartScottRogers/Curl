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
using Curl.Conformance.UnitProgram;
using Curl.Console;
using Curl.Engine;
using Curl.Networking;
using Curl.Original.Protocol.Ssh;
using Curl.Original.Protocol.Ws;
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
public sealed partial class UpstreamConformanceTests
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

    internal static readonly string UpstreamTestDataFolder = Path.Combine(AppContext.BaseDirectory, "CurlTestCases", "Original");

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

    // Listed cases whose -w output holds non-ASCII command-line text, which upstream's runner hands
    // curl as UTF-8 bytes. On Windows Curl writes -w in the ANSI code page, as the ANSI-argv curl 8.22.0
    // does (ADR-0830, BL-3841), so there these cases are run and reported but not held to the list;
    // Linux and macOS judge them.
    private static readonly IReadOnlySet<int> NeedsUtf8ArgumentsCases = new HashSet<int> { 2046, 2047 };

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
        byte[] testFile = await File.ReadAllBytesAsync(Path.Combine(UpstreamTestDataFolder, $"test{testNumber}{UpstreamTestFileExtension}"));
        bool isListed = PassingCases.Contains(testNumber) && !(OperatingSystem.IsMacOS() && NeedsTls13ServerCases.Contains(testNumber)) && !(OperatingSystem.IsWindows() && NeedsUtf8ArgumentsCases.Contains(testNumber)) && IsHeldOnThisPlatform(testFile);
        diagnostics.Arrange("upstream case number", testNumber);
        diagnostics.Arrange("case is on the passing list", isListed);
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
                Assert.Inconclusive(WithPlatformHoldReason(verdict.Message, testFile));
                break;
        }
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
            EngineFeatureMask featureMask = FeatureMaskFor(testFile);
            UpstreamCaseRunner runner = new(invocation => RunCurlAsync(invocation, featureMask), PlatformFor(testFile), TimeProvider.System, TimeLimit, SshServer, Http2Server, Http3Server, libTests: LibTests, unitPrograms: UnitPrograms.Ported, perlCommandScripts: UpstreamPerlCommandScripts.All);
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


    private static readonly string CaseHangLimitMessage =
        $"the case did not finish within {CaseHangLimit.TotalSeconds.ToString(CultureInfo.InvariantCulture)} seconds";

    // Curl as the command composes it, every TCP dial reaching the case's in-memory servers through
    // InMemoryServerTcpDialer, so TcpConnector's proxy tunnel and PROXY-line code stays in the path (ADR-0460).
    // The default config file is looked for only where the case's <setenv> points CURL_HOME, XDG_CONFIG_HOME or HOME (BL-1977).
    // It parses as a QUIC build, though no platform reports http/3 (ADR-0540), so the http/3 cases, to which the harness reports it, run over the stand-in (ADR-0720, BL-2838).
    // Under the case's feature mask, curl answers as a build without the features it hides (BL-2856).
    internal static Task<int> RunCurlAsync(UpstreamCurlInvocation invocation) => RunCurlAsync(invocation, EngineFeatureMask.None);

    internal static Task<int> RunCurlAsync(UpstreamCurlInvocation invocation, EngineFeatureMask featureMask) =>
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
            signingClock: invocation.Arguments.Any(argument => argument.StartsWith("--httpsig", StringComparison.Ordinal) || argument == "--aws-sigv4") ? SigningClockFor(invocation.EnvironmentVariables) : null,
            honoursHstsTimeVariable: true,
            honoursHstsHttpVariable: true,
            honoursAltSvcHttpVariable: true,
            honoursDnsServerVariable: true,
            parsesAsQuicBuild: true,
            parsesAsEchBuild: true,
            extendedAttributeWriter: ExtendedAttributeWriterFor(invocation),
            honoursDebugWriteOutVariables: true,
            webSocketRandomSource: WebSocketRandomSourceFor(invocation.EnvironmentVariables),
            fileSystem: FileSystemFor(invocation.EnvironmentVariables),
            securityContexts: SecurityContextsFor(invocation.EnvironmentVariables),
            featureMask: featureMask,
            standardOutputIsTerminal: StandardOutputIsTerminalFor(invocation.EnvironmentVariables),
            digestRandomBytes: DigestRandomBytesFor(invocation.EnvironmentVariables)).RunAsync(ArgumentsFor(invocation));

    // A ported libtest program's engine, in place of curl (BL-2558, ADR-0658): the managed Libcurl engine, every TCP
    // dial reaching the case's in-memory servers through InMemoryServerTcpDialer, names resolved to loopback but
    // test1592's host, whose lookup never answers (BL-2839, ADR-0722), the
    // datagram protocols on the case's datagram servers, FTP's active-mode data connections on the FTP stand-in's
    // listener (BL-2597) and the proxy environment the case's <setenv>, as RunCurlAsync composes curl. It signs AWS SigV4
    // requests and HTTP signatures at the epoch, as upstream's debug build does under the CURL_FORCETIME its test runner sets (BL-2675),
    // and answers NTLM with the hand-built context, as RunCurlAsync's usesHandBuiltNtlm does (BL-2635).
    // Under a case's CURL_ENTROPY its WebSocket key and masks follow the debug build's forced sequence (BL-2688), and its
    // MIME boundary is the one that sequence gives curl_mime_init (test1972, BL-2695).
    // Its handles read the case's <stdin> where libcurl reads the process's, as a curl_formadd file named "-" does (BL-2625),
    // and write their CURLOPT_VERBOSE lines to the case's stderr where libcurl writes the process's (BL-2726).
    internal static ICurlEngine CreateEngine(UpstreamCurlInvocation invocation) =>
        new ManagedCurlEngine(
            TimeProvider.System,
            createDispatcher: null,
            CurlEasyErrorText.Of,
            name => invocation.EnvironmentVariables.GetValueOrDefault(name),
            new EngineDialing(new InMemoryServerTcpDialer(invocation.Connector), new StalledLookupDnsResolver(new LoopbackOnlyDnsResolver(), TimeProvider.System), invocation.DatagramConnector, invocation.ConnectionListener, UsesHandBuiltNtlm: true),
            signingClock: SigningClockFor(invocation.EnvironmentVariables),
            webSocketRandomSource: WebSocketRandomSourceFor(invocation.EnvironmentVariables),
            standardInput: invocation.StandardInput,
            stopsFirstFtpTransferBeforePwd: invocation.EnvironmentVariables.ContainsKey("CURL_FTP_PWD_STOP"),
            createFormBoundary: invocation.EnvironmentVariables.GetValueOrDefault("CURL_ENTROPY") is { } entropy ? () => ForcedEntropyFormBoundary.Of(entropy) : null,
            standardError: invocation.StandardError);

    // The debug build's CURL_ENTROPY sequence (BL-2688), else its zero masks under CURL_WS_FORCE_ZERO_MASK (BL-2698),
    // else null, for the engine's system random source.
    private static IWebSocketRandomSource? WebSocketRandomSourceFor(IReadOnlyDictionary<string, string> environment) =>
        environment.GetValueOrDefault("CURL_ENTROPY") is { } entropy ? new ForcedEntropyWebSocketRandomSource(entropy)
        : environment.ContainsKey("CURL_WS_FORCE_ZERO_MASK") ? new ZeroMaskWebSocketRandomSource()
        : null;

    // The Digest and DIGEST-MD5 client nonce's bytes under the debug build's CURL_ENTROPY sequence (ADR-0751, BL-2834),
    // else null, for the system's random numbers.
    internal static Func<int, byte[]>? DigestRandomBytesFor(IReadOnlyDictionary<string, string> environment)
    {
        if (environment.GetValueOrDefault("CURL_ENTROPY") is not { } entropy)
        {
            return null;
        }

        ForcedEntropyWebSocketRandomSource source = new(entropy);
        return count =>
        {
            byte[] bytes = new byte[count];
            source.Fill(bytes);
            return bytes;
        };
    }

    // The ported libtest programs the ratchet runs a <tool> case's program from, over CreateEngine, with lib670's
    // pause taken off the wall clock (ADR-1169).
    private static readonly UpstreamLibTests LibTests = new(
        new LibTestPrograms(LibTestPrograms.Ported.ProgramNames.Select(name => name == Lib670WithItsPauseSkipped.Name ? new Lib670WithItsPauseSkipped() : LibTestPrograms.Ported.Find(name)!)),
        CreateEngine);

    // lib670 (cases 670 to 673), each run over its own PauseSkippingClock: the pause still holds until the program
    // unpauses, but its PAUSE_TIME seconds pass at once, so a loaded runner cannot stretch them to the hang limit (BL-4378).
    private sealed class Lib670WithItsPauseSkipped : ILibTest
    {
        internal const string Name = "lib670";

        public string ProgramName => Name;

        public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
            new Lib670(new PauseSkippingClock()).RunAsync(context, cancellationToken);
    }

    // The system clock, but every reading after the first, the moment lib670's pause began, is Lib670.PauseSeconds later.
    private sealed class PauseSkippingClock : TimeProvider
    {
        private int readings;

        public override long GetTimestamp() =>
            TimeProvider.System.GetTimestamp() + (Interlocked.Increment(ref readings) == 1 ? 0 : Lib670.PauseSeconds * TimestampFrequency);
    }

    // The signing time of upstream's debug build under CURL_FORCETIME: CURL_HTTPSIG_CREATED seconds after the epoch
    // when the case sets it (test5004), else the epoch itself.
    private static FixedClock SigningClockFor(IReadOnlyDictionary<string, string> environment) =>
        environment.GetValueOrDefault("CURL_HTTPSIG_CREATED") is { } created && long.TryParse(created, NumberStyles.None, CultureInfo.InvariantCulture, out long seconds)
            ? new FixedClock(DateTimeOffset.FromUnixTimeSeconds(seconds))
            : new FixedClock(DateTimeOffset.UnixEpoch);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
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
