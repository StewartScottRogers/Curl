using System.Globalization;
using System.Text;
using Curl.Testing;

namespace Curl.Conformance;

/// <summary>
/// Pins every reason <see cref="UpstreamCaseScreening"/> gives for skipping a case, and that a
/// case the harness can run gets none.
/// </summary>
[TestClass]
public sealed class UpstreamCaseScreeningTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string RunnableClient = "<client>\n<server>\nhttp\nfile\nnone\n</server>\n<command>\nhttp://h/1\n</command>\n</client>\n";

    private static readonly HashSet<string> Features = ["http", "SSL"];

    private static readonly UpstreamTestFileExpansion CleanExpansion = new(ReadOnlyMemory<byte>.Empty, [], [], null);

    [TestMethod]
    public void FindSkipReason_RunnableCase_ReturnsNull()
    {
        string sections = RunnableClient
            + "<reply>\n<servercmd>\nswsclose\n</servercmd>\n</reply>\n"
            + "<client>\n<features>\nhttp\n!Debug\n</features>\n</client>\n"
            + $"<verify>\n<file name=\"{Rooted("out")}\">\n</file>\n<strip>\n^Date:\n</strip>\n<strippart>\n# comment\ns/a/b/\n</strippart>\n<errorcode>\n 7 \n</errorcode>\n</verify>\n";

        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test sections", Neutral(sections));
        string? reason = Screen(sections);
        diagnostics.Act("skip reason", reason ?? "(none)");
        diagnostics.Assert("skip reason", "(none)", reason ?? "(none)");
        Assert.IsNull(reason, reason);
    }

    [TestMethod]
    public void FindSkipReason_ConditionError_IsTheReason()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamTestFileExpansion expansion = new(ReadOnlyMemory<byte>.Empty, [], [], "stray %endif");
        diagnostics.Arrange("expansion", """new(ReadOnlyMemory<byte>.Empty, [], [], "stray %endif")""");
        string? reason = UpstreamCaseScreening.FindSkipReason(expansion, ParsedTestCase.From(RunnableClient), Features);
        diagnostics.Act("skip reason", reason);
        diagnostics.Assert("skip reason", "stray %endif", reason);
        Assert.AreEqual("stray %endif", reason);
    }

    [TestMethod]
    public void FindSkipReason_UnknownVariables_AreTheReason()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamTestFileExpansion expansion = new(ReadOnlyMemory<byte>.Empty, ["%FTPPORT", "%USER"], [], null);
        diagnostics.Arrange("expansion", """new(ReadOnlyMemory<byte>.Empty, ["%FTPPORT", "%USER"], [], null)""");
        string? reason = UpstreamCaseScreening.FindSkipReason(expansion, ParsedTestCase.From(RunnableClient), Features);
        diagnostics.Act("skip reason", reason);
        diagnostics.Assert("skip reason", "the harness has no value for %FTPPORT, %USER", reason);
        Assert.AreEqual("the harness has no value for %FTPPORT, %USER", reason);
    }

    [TestMethod]
    [DataRow(195)]
    [DataRow(196)]
    [DataRow(1120)]
    public void FindSkipReason_FtpCaseWithFtpPortGivenTheRunnersValue_IsNotSkipped(int testNumber)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] testFile = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "CurlTestCases", "Original", $"test{testNumber}.curltest"));
        Dictionary<string, string> variables = new(StringComparer.Ordinal) { ["HOSTIP"] = "127.0.0.1", ["FTPPORT"] = UpstreamCaseRunner.FtpPort, ["TESTNUMBER"] = testNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        UpstreamTestFileExpansion expansion = UpstreamTestFileExpander.Expand(testFile, variables, Features);

        string? reason = UpstreamCaseScreening.FindSkipReason(expansion, expansion.Parse().TestCase!, Features);

        diagnostics.Act("skip reason", reason);
        Assert.IsNull(reason, reason);
    }

    // runtests.pl at curl-8_22_0 never reads <client><connectport>: the http-proxy sws tunnels to the
    // port the CONNECT names, so test1316's part is accepted and ignored (BL-2239).
    [TestMethod]
    public void FindSkipReason_ClientConnectPort_IsAcceptedAsRuntestsPlIgnoresIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] testFile = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "CurlTestCases", "Original", "test1316.curltest"));
        Dictionary<string, string> variables = new(StringComparer.Ordinal) { ["HOSTIP"] = "127.0.0.1", ["FTPPORT"] = UpstreamCaseRunner.FtpPort, ["PROXYPORT"] = UpstreamCaseRunner.ProxyPort, ["TESTNUMBER"] = "1316" };
        UpstreamTestFileExpansion expansion = UpstreamTestFileExpander.Expand(testFile, variables, Features);
        UpstreamTestCase testCase = expansion.Parse().TestCase!;

        string? reason = UpstreamCaseScreening.FindSkipReason(expansion, testCase, Features);

        diagnostics.Act("skip reason", reason);
        Assert.IsNotNull(testCase.Find("client", "connectport"));
        Assert.IsNull(reason, reason);
    }

    // test1651 nests a <verify><limits> inside <client>, where runtests.pl at curl-8_22_0 never reads
    // it, so the part is accepted and ignored (BL-2913).
    [TestMethod]
    public void FindSkipReason_ClientVerify_IsAcceptedAsRuntestsPlIgnoresIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] testFile = Encoding.ASCII.GetBytes("<testcase>\n<client>\n<server>\nnone\n</server>\n<command>\nfile:///dir/x\n</command>\n<verify>\n<limits>\nAllocations: 14000\n</limits>\n</verify>\n</client>\n</testcase>\n");
        UpstreamTestFileExpansion expansion = UpstreamTestFileExpander.Expand(testFile, new Dictionary<string, string>(StringComparer.Ordinal), Features);
        UpstreamTestCase testCase = expansion.Parse().TestCase!;

        string? reason = UpstreamCaseScreening.FindSkipReason(expansion, testCase, Features);

        diagnostics.Act("skip reason", reason);
        Assert.IsNotNull(testCase.Find("client", "verify"));
        Assert.IsNull(reason, reason);
    }

    // The FTP active-mode and upload cases BL-1907 opened: none skips for %FTPPORT, the FTP stand-in
    // or <verify><upload>; the runner measures them (most pass, 149, 216, 1211 and 1217 differ).
    [TestMethod]
    [DataRow(101)]
    [DataRow(103)]
    [DataRow(107)]
    [DataRow(108)]
    [DataRow(109)]
    [DataRow(112)]
    [DataRow(116)]
    [DataRow(128)]
    [DataRow(144)]
    [DataRow(145)]
    [DataRow(149)]
    [DataRow(216)]
    [DataRow(235)]
    [DataRow(236)]
    [DataRow(248)]
    [DataRow(251)]
    [DataRow(348)]
    [DataRow(362)]
    [DataRow(475)]
    [DataRow(476)]
    [DataRow(1038)]
    [DataRow(1039)]
    [DataRow(1055)]
    [DataRow(1211)]
    [DataRow(1217)]
    [DataRow(1414)]
    public void FindSkipReason_FtpActiveModeOrUploadCase_IsNotSkippedForTheFtpStandIn(int testNumber)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] testFile = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "CurlTestCases", "Original", $"test{testNumber}.curltest"));
        Dictionary<string, string> variables = new(StringComparer.Ordinal) { ["HOSTIP"] = "127.0.0.1", ["FTPPORT"] = UpstreamCaseRunner.FtpPort, ["TESTNUMBER"] = testNumber.ToString(CultureInfo.InvariantCulture) };
        UpstreamTestFileExpansion expansion = UpstreamTestFileExpander.Expand(testFile, variables, Features);

        string reason = UpstreamCaseScreening.FindSkipReason(expansion, expansion.Parse().TestCase!, Features) ?? string.Empty;

        diagnostics.Act("skip reason", reason);
        Assert.DoesNotContain("%FTPPORT", reason);
        Assert.DoesNotContain("FTP stand-in", reason);
        Assert.DoesNotContain("<upload>", reason);
    }

    // The FTP cases whose <servercmd> BL-1908 carries out (COUNT, NODATACONN and its 425, 421 and
    // 150 variants, RETRWEIRDO, RETRNOSIZE, RETRSIZE, PASVBADIP, SLOWDOWN, REPLY) run, with
    // %FTPTIME2 given the runner's value; DELAY and SLOWDOWNDATA, which time the answers, run too (BL-1958).
    [TestMethod]
    [DataRow(147, null)]
    [DataRow(280, null)]
    [DataRow(1206, null)]
    [DataRow(1207, null)]
    [DataRow(1208, null)]
    [DataRow(1209, null)]
    [DataRow(1211, null)]
    [DataRow(126, null)]
    [DataRow(137, null)]
    [DataRow(270, null)]
    [DataRow(416, null)]
    [DataRow(250, null)]
    [DataRow(190, null)]
    [DataRow(1086, null)]
    public void FindSkipReason_FtpServerCommandCase_RunsUnlessItsAnswersAreTimed(int testNumber, string? expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] testFile = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "CurlTestCases", "Original", $"test{testNumber}.curltest"));
        Dictionary<string, string> variables = new(StringComparer.Ordinal) { ["HOSTIP"] = "127.0.0.1", ["FTPPORT"] = UpstreamCaseRunner.FtpPort, ["FTPTIME2"] = "8", ["TESTNUMBER"] = testNumber.ToString(CultureInfo.InvariantCulture) };
        UpstreamTestFileExpansion expansion = UpstreamTestFileExpander.Expand(testFile, variables, Features);

        string? reason = UpstreamCaseScreening.FindSkipReason(expansion, expansion.Parse().TestCase!, Features);

        diagnostics.Act("skip reason", reason);
        Assert.AreEqual(expected, reason);
    }

    // The 8 cases naming %FTPSPORT at curl-8_21_0 run against the FTP stand-in behind implicit TLS
    // (BL-1913); 404 still skips, for another reason, never for the port. 407's <client><stdout>,
    // which runtests.pl never reads, no longer skips it (BL-2221).
    [TestMethod]
    [DataRow(400, null)]
    [DataRow(401, null)]
    [DataRow(403, null)]
    [DataRow(404, null)]
    [DataRow(406, null)]
    [DataRow(407, null)]
    [DataRow(408, null)]
    [DataRow(1112, null)]
    public void FindSkipReason_FtpsCase_RunsWithAValueForTheFtpsPort(int testNumber, string? expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] testFile = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "CurlTestCases", "Original", $"test{testNumber}.curltest"));
        Dictionary<string, string> variables = new(StringComparer.Ordinal)
        {
            ["HOSTIP"] = "127.0.0.1",
            ["CLIENTIP"] = "127.0.0.1",
            ["FTPSPORT"] = UpstreamCaseRunner.FtpsPort,
            ["LOGDIR"] = "/log",
            ["TESTNUMBER"] = testNumber.ToString(CultureInfo.InvariantCulture),
        };
        UpstreamTestFileExpansion expansion = UpstreamTestFileExpander.Expand(testFile, variables, Features);

        string? reason = UpstreamCaseScreening.FindSkipReason(expansion, expansion.Parse().TestCase!, Features);

        diagnostics.Act("skip reason", reason);
        Assert.AreEqual(expected, reason);
    }

    [TestMethod]
    [DataRow("REPLY DELAY 200 ok\nCOUNT DELAY 1\ndelay: 5\n", null)]
    [DataRow("SLOWDOWN\n", null)]
    [DataRow("DELAY CWD 60\n", null)]
    [DataRow("SLOWDOWNDATA\n", null)]
    public void FindSkipReason_FtpCaseServerCommand_IsReadAsFtpserverReadsIt(string serverCommands, string? expected)
    {
        string? reason = Screen($"<reply>\n<servercmd>\n{serverCommands}</servercmd>\n</reply>\n<client>\n<server>\nftp\n</server>\n<command>\nftp://127.0.0.1:8993/1\n</command>\n</client>\n");

        Assert.AreEqual(expected, reason);
    }

    [TestMethod]
    public void FindSkipReason_SmtpCaseWithSmtpPortGivenTheRunnersValue_IsNotSkipped()
    {
        byte[] testFile = System.Text.Encoding.Latin1.GetBytes("<testcase>\n<client>\n<server>\nsmtp\n</server>\n<command>\nsmtp://%HOSTIP:%SMTPPORT/1 --mail-rcpt a@b -T -\n</command>\n</client>\n<verify>\n<upload>\nx\n</upload>\n</verify>\n</testcase>\n");
        Dictionary<string, string> variables = new(StringComparer.Ordinal) { ["HOSTIP"] = "127.0.0.1", ["SMTPPORT"] = UpstreamCaseRunner.SmtpPort };
        UpstreamTestFileExpansion expansion = UpstreamTestFileExpander.Expand(testFile, variables, Features);

        string? reason = UpstreamCaseScreening.FindSkipReason(expansion, expansion.Parse().TestCase!, Features);

        Assert.IsNull(reason, reason);
    }

    [TestMethod]
    public void FindSkipReason_Pop3CaseWithPop3PortGivenTheRunnersValue_IsNotSkipped()
    {
        byte[] testFile = System.Text.Encoding.Latin1.GetBytes("<testcase>\n<client>\n<server>\npop3\n</server>\n<command>\npop3://%HOSTIP:%POP3PORT/1 -u user:secret\n</command>\n</client>\n</testcase>\n");
        Dictionary<string, string> variables = new(StringComparer.Ordinal) { ["HOSTIP"] = "127.0.0.1", ["POP3PORT"] = UpstreamCaseRunner.Pop3Port };
        UpstreamTestFileExpansion expansion = UpstreamTestFileExpander.Expand(testFile, variables, Features);

        string? reason = UpstreamCaseScreening.FindSkipReason(expansion, expansion.Parse().TestCase!, Features);

        Assert.IsNull(reason, reason);
    }

    [TestMethod]
    public void FindSkipReason_ImapCaseWithImapPortGivenTheRunnersValue_IsNotSkipped()
    {
        byte[] testFile = System.Text.Encoding.Latin1.GetBytes("<testcase>\n<client>\n<server>\nimap\n</server>\n<command>\nimap://%HOSTIP:%IMAPPORT/1 -T -\n</command>\n</client>\n<verify>\n<upload>\nx\n</upload>\n</verify>\n</testcase>\n");
        Dictionary<string, string> variables = new(StringComparer.Ordinal) { ["HOSTIP"] = "127.0.0.1", ["IMAPPORT"] = UpstreamCaseRunner.ImapPort };
        UpstreamTestFileExpansion expansion = UpstreamTestFileExpander.Expand(testFile, variables, Features);

        string? reason = UpstreamCaseScreening.FindSkipReason(expansion, expansion.Parse().TestCase!, Features);

        Assert.IsNull(reason, reason);
    }

    [TestMethod]
    public void FindSkipReason_UploadVerifiedOffSmtpAndImap_IsTheReason()
    {
        string testFile = RunnableClient + "<verify>\n<upload>\nx\n</upload>\n</verify>\n";

        string? reason = UpstreamCaseScreening.FindSkipReason(new(ReadOnlyMemory<byte>.Empty, [], [], null), ParsedTestCase.From(testFile), Features);

        Assert.AreEqual("the harness records <verify><upload> only for the smtp, imap, smtps, imaps, tftp, ftp, ftps, scp and sftp servers", reason);
    }

    [TestMethod]
    [DataRow(80)]
    [DataRow(83)]
    [DataRow(95)]
    [DataRow(150)]
    [DataRow(184)]
    [DataRow(194)]
    [DataRow(275)]
    [DataRow(744)]
    [DataRow(1078)]
    [DataRow(1184)]
    [DataRow(1288)]
    [DataRow(1297)]
    [DataRow(1428)]
    [DataRow(1904)]
    [DataRow(2050)]
    [DataRow(2107)]
    [DataRow(3028)]
    public void FindSkipReason_ProxyCaseGivenTheRunnersProxyPort_IsNotSkippedForProxyPort(int testNumber)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] testFile = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "CurlTestCases", "Original", $"test{testNumber}.curltest"));
        Dictionary<string, string> variables = new(StringComparer.Ordinal) { ["HOSTIP"] = "127.0.0.1", ["HTTPPORT"] = UpstreamCaseRunner.HttpPort, ["PROXYPORT"] = UpstreamCaseRunner.ProxyPort, ["TESTNUMBER"] = testNumber.ToString(CultureInfo.InvariantCulture) };
        UpstreamTestFileExpansion expansion = UpstreamTestFileExpander.Expand(testFile, variables, Features);

        string? reason = UpstreamCaseScreening.FindSkipReason(expansion, expansion.Parse().TestCase!, Features);

        diagnostics.Act("skip reason", reason ?? "(none)");
        Assert.DoesNotContain("%PROXYPORT", reason ?? string.Empty);
    }

    [TestMethod]
    [DataRow(987)]
    [DataRow(988)]
    [DataRow(989)]
    public void FindSkipReason_ImplicitTlsMailCaseGivenTheRunnersTlsPorts_IsNotSkipped(int testNumber)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] testFile = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "CurlTestCases", "Original", $"test{testNumber}.curltest"));
        Dictionary<string, string> variables = new(StringComparer.Ordinal)
        {
            ["HOSTIP"] = "127.0.0.1",
            ["SMTPSPORT"] = UpstreamCaseRunner.SmtpsPort,
            ["IMAPSPORT"] = UpstreamCaseRunner.ImapsPort,
            ["POP3SPORT"] = UpstreamCaseRunner.Pop3sPort,
            ["TESTNUMBER"] = testNumber.ToString(CultureInfo.InvariantCulture),
        };
        UpstreamTestFileExpansion expansion = UpstreamTestFileExpander.Expand(testFile, variables, Features);

        string? reason = UpstreamCaseScreening.FindSkipReason(expansion, expansion.Parse().TestCase!, Features);

        diagnostics.Act("skip reason", reason ?? "(none)");
        Assert.IsNull(reason, reason);
    }

    [TestMethod]
    public void FindSkipReason_NoListenPortGivenTheRunnersValue_IsNotTheReason()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] test19 = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "CurlTestCases", "Original", "test19.curltest"));
        Dictionary<string, string> variables = new(StringComparer.Ordinal) { ["HOSTIP"] = "127.0.0.1", ["NOLISTENPORT"] = UpstreamCaseRunner.NoListenPort };
        UpstreamTestFileExpansion expansion = UpstreamTestFileExpander.Expand(test19, variables, Features);

        string? reason = UpstreamCaseScreening.FindSkipReason(expansion, expansion.Parse().TestCase!, Features);

        diagnostics.Act("skip reason", reason);
        Assert.IsNull(reason, reason);
    }

    [TestMethod]
    public void FindSkipReason_CertdirGivenAValue_IsNotTheReasonAndNeitherIsTheHttpsServer()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] file = "<testcase>\n<client>\n<server>\nhttps test-localhost.pem\n</server>\n<command>\n--cacert %CERTDIR/certs/test-ca.crt https://localhost/\n</command>\n</client>\n</testcase>\n"u8.ToArray();
        Dictionary<string, string> variables = new(StringComparer.Ordinal) { ["CERTDIR"] = "/curl/tests" };
        diagnostics.Arrange("variables", "CERTDIR=/curl/tests");
        UpstreamTestFileExpansion expansion = UpstreamTestFileExpander.Expand(file, variables, Features);

        string? reason = UpstreamCaseScreening.FindSkipReason(expansion, expansion.Parse().TestCase!, Features);
        diagnostics.Act("skip reason", reason);

        diagnostics.Assert("skip reason does not name %CERTDIR", false, reason?.Contains("%CERTDIR", StringComparison.Ordinal) ?? false);
        Assert.IsNull(reason, "the https server is emulated (BL-1912)");
    }

    [TestMethod]
    public void FindSkipReason_UnsupportedInstructions_AreTheReason()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamTestFileExpansion expansion = new(ReadOnlyMemory<byte>.Empty, [], ["%include"], null);
        diagnostics.Arrange("expansion", """new(ReadOnlyMemory<byte>.Empty, [], ["%include"], null)""");
        string? reason = UpstreamCaseScreening.FindSkipReason(expansion, ParsedTestCase.From(RunnableClient), Features);
        diagnostics.Act("skip reason", reason);
        diagnostics.Assert("skip reason", "the harness does not carry out %include", reason);
        Assert.AreEqual("the harness does not carry out %include", reason);
    }

    [TestMethod]
    [DataRow("lib1", null)]
    [DataRow("lib2", "libtest program not ported")]
    public void FindSkipReason_LibTool_RunsOnlyAPortedProgram(string tool, string? expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string sections = RunnableClient + $"<client>\n<tool>\n{tool}\n</tool>\n</client>\n";
        diagnostics.Arrange("test sections", sections);

        string? reason = UpstreamCaseScreening.FindSkipReason(CleanExpansion, ParsedTestCase.From(sections), Features, isPortedLibTest: program => program == "lib1");
        diagnostics.Act("skip reason", reason);

        diagnostics.Assert("skip reason", expected, reason);
        Assert.AreEqual(expected, reason);
    }

    [TestMethod]
    public void FindSkipReason_PortedLibToolWithNoCommand_IsNullSinceTheProgramRunsWithNoArguments()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const string sections = "<client>\n<tool>\nlib1\n</tool>\n<name>\nno command\n</name>\n</client>\n";
        diagnostics.Arrange("test sections", sections);

        string? reason = UpstreamCaseScreening.FindSkipReason(CleanExpansion, ParsedTestCase.From(sections), Features, isPortedLibTest: program => program == "lib1");
        diagnostics.Act("skip reason", reason);

        diagnostics.Assert("skip reason", "(none)", reason ?? "(none)");
        Assert.IsNull(reason);
    }

    [TestMethod]
    [DataRow("<client>\n<tool>\nlib%TESTNUMBER\n</tool>\n</client>\n", null)]
    [DataRow("<client>\n<tool>\n lib500 \n</tool>\n</client>\n", "lib500")]
    [DataRow("<client>\n<tool>\ntool1394\n</tool>\n</client>\n", null)]
    [DataRow("<client>\n<name>\nno tool\n</name>\n</client>\n", null)]
    public void LibTestProgramName_IsTheLibProgramTheToolLineNames(string sections, string? expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test sections", sections);

        string? program = UpstreamCaseScreening.LibTestProgramName(ParsedTestCase.From(sections));
        diagnostics.Act("program", program);

        diagnostics.Assert("program", expected, program);
        Assert.AreEqual(expected, program);
    }

    [TestMethod]
    [DataRow("<client>\n<features>\nunittest\n</features>\n</client>\n", "unit1302")]
    [DataRow("<client>\n<features>\nunittest\n</features>\n<tool>\ntool1302\n</tool>\n</client>\n", "tool1302")]
    [DataRow("<client>\n<features>\nunittest\n</features>\n<tool>\n unit1305 \n</tool>\n</client>\n", "unit1305")]
    [DataRow("<client>\n<features>\nunittest\n</features>\n<tool>\nlib1302\n</tool>\n</client>\n", null)]
    [DataRow("<client>\n<features>\nunittest\n</features>\n<tool>\nunit\n</tool>\n</client>\n", null)]
    [DataRow("<client>\n<features>\nunittest\n</features>\n<tool>\nunitx2\n</tool>\n</client>\n", null)]
    [DataRow("<client>\n<tool>\ntool1302\n</tool>\n</client>\n", null)]
    public void UnitProgramName_IsTheToolOrUnitAndTheTestNumberOfAUnittestCase(string sections, string? expected)
    {
        string? program = UpstreamCaseScreening.UnitProgramName(ParsedTestCase.From(sections), 1302);

        Assert.AreEqual(expected, program);
    }

    [TestMethod]
    public void UnitProgramName_NullCase_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamCaseScreening.UnitProgramName(null!, 1302));
    }

    [TestMethod]
    public void CaseFeatures_NullFeatures_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamCaseScreening.CaseFeatures(ParsedTestCase.From(UnitCaseClient), 1302, null!, null));
    }

    [TestMethod]
    public void CaseFeatures_PortedUnitProgram_AddsUnittestDebugAndTrackMemory()
    {
        IReadOnlySet<string> features = UpstreamCaseScreening.CaseFeatures(ParsedTestCase.From(UnitCaseClient), 1302, Features, program => program == "unit1302");

        Assert.IsTrue(features.IsSupersetOf(["unittest", "Debug", "TrackMemory"]));
        Assert.IsTrue(features.IsSupersetOf(Features));
        Assert.IsFalse(features.Contains("shuffle-dns"));
    }

    [TestMethod]
    public void CaseFeatures_PortedUnit1608_AlsoAddsShuffleDns()
    {
        IReadOnlySet<string> features = UpstreamCaseScreening.CaseFeatures(ParsedTestCase.From(UnitCaseClient), 1608, Features, program => program == "unit1608");

        Assert.IsTrue(features.IsSupersetOf(["unittest", "shuffle-dns"]));
    }

    [TestMethod]
    public void CaseFeatures_UnitProgramNotPorted_IsThePlatformsFeatures()
    {
        Assert.AreSame(Features, UpstreamCaseScreening.CaseFeatures(ParsedTestCase.From(UnitCaseClient), 1302, Features, null));
    }

    private const string UnitCaseClient = "<client>\n<features>\nunittest\nDebug\nTrackMemory\n</features>\n<name>\nbase64\n</name>\n</client>\n";

    private static readonly UpstreamTestFileExpansion EmptyExpansion = new(ReadOnlyMemory<byte>.Empty, [], [], null);

    [TestMethod]
    [DataRow(UnitCaseClient, "Curl has no port of unit1302")]
    [DataRow("<client>\n<features>\nunittest\n</features>\n<tool>\ntool1302\n</tool>\n</client>\n", "Curl has no port of tool1302")]
    public void FindSkipReason_UnittestCaseWithNoPort_NamesTheProgram(string sections, string expected)
    {
        string? reason = UpstreamCaseScreening.FindSkipReason(EmptyExpansion, ParsedTestCase.From(sections), Features, isPortedUnitProgram: program => program == "unit9999", testNumber: 1302);

        Assert.AreEqual(expected, reason);
    }

    [TestMethod]
    public void FindSkipReason_UnittestCaseWithNoTable_NamesTheProgram()
    {
        string? reason = UpstreamCaseScreening.FindSkipReason(EmptyExpansion, ParsedTestCase.From(UnitCaseClient), Features, testNumber: 1302);

        Assert.AreEqual(UpstreamCaseScreening.UnitProgramNotPorted("unit1302"), reason);
    }

    [TestMethod]
    [DataRow(UnitCaseClient)]
    [DataRow("<client>\n<features>\nunittest\n</features>\n<tool>\nunit1302\n</tool>\n<command>\narg\n</command>\n</client>\n")]
    public void FindSkipReason_UnittestCaseWithAPort_IsNotSkipped(string sections)
    {
        string? reason = UpstreamCaseScreening.FindSkipReason(EmptyExpansion, ParsedTestCase.From(sections), Features, isPortedUnitProgram: program => program == "unit1302", testNumber: 1302);

        Assert.IsNull(reason);
    }

    [TestMethod]
    [DataRow("<client>\n<features>\nunittest\n</features>\n<killserver>\nx\n</killserver>\n<bogus>\nx\n</bogus>\n</client>\n", "the harness does not act on <client><bogus>")]
    [DataRow("<client>\n<features>\nunittest\nno-such-feature\n</features>\n</client>\n", "Curl lacks the feature no-such-feature")]
    public void FindSkipReason_PortedUnittestCase_AnotherReasonStillWins(string sections, string expected)
    {
        string? reason = UpstreamCaseScreening.FindSkipReason(EmptyExpansion, ParsedTestCase.From(sections), Features, isPortedUnitProgram: _ => true, testNumber: 1302);

        Assert.AreEqual(expected, reason);
    }

    [TestMethod]
    public void FindSkipReason_UnportedUnittestCaseWithAnUnsupportedPart_ThePartWins()
    {
        string? reason = UpstreamCaseScreening.FindSkipReason(EmptyExpansion, ParsedTestCase.From("<client>\n<features>\nunittest\n</features>\n<bogus>\nx\n</bogus>\n</client>\n"), Features, testNumber: 1302);

        Assert.AreEqual("the harness does not act on <client><bogus>", reason);
    }

    [TestMethod]
    [DataRow("<client>\n<tool>\ntool1302\n</tool>\n<command>\narg\n</command>\n</client>\n", UpstreamCaseScreening.ToolNotRun)]
    [DataRow("<client>\n<features>\nTrackMemory\n</features>\n<command>\nhttp://127.0.0.1/\n</command>\n</client>\n", "Curl lacks the feature TrackMemory")]
    [DataRow("<client>\n<name>\nno command\n</name>\n</client>\n", "the case has no <client><command>")]
    public void FindSkipReason_CaseWithoutUnittest_IsUnaffectedByTheUnitProgramTable(string sections, string expected)
    {
        string? reason = UpstreamCaseScreening.FindSkipReason(EmptyExpansion, ParsedTestCase.From(sections), Features, isPortedUnitProgram: _ => true, testNumber: 1302);

        Assert.AreEqual(expected, reason);
    }

    [TestMethod]
    public void LibTestProgramName_NullCase_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamCaseScreening.LibTestProgramName(null!));
    }

    [TestMethod]
    [DataRow("<verify>\n<protocol>\nUSER anonymous\nEPSV\nTYPE I\nSIZE 1\nRETR 1\n</protocol>\n</verify>\n")]
    [DataRow("<verify>\n<protocol>\nPASV\nLIST\nNLST\nMDTM 1\nREST 3\n</protocol>\n</verify>\n")]
    public void FindSkipReason_PassiveFtpTransfer_RunsTheCase(string sections)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test sections", sections);

        string? reason = Screen(RunnableClient + sections);
        diagnostics.Act("skip reason", reason);

        diagnostics.Assert("skip reason", "null", reason);
        Assert.IsNull(reason);
    }

    [TestMethod]
    [DataRow("<client>\n<tool>\nlib1\n</tool>\n</client>\n", "libtest program not ported")]
    [DataRow("<client>\n<tool>\nunit1300\n</tool>\n</client>\n", "the harness does not act on <client><tool>")]
    [DataRow("<client>\n<tool>\nlib\n</tool>\n</client>\n", "the harness does not act on <client><tool>")]
    [DataRow("<client>\n<tool>\nlibx1\n</tool>\n</client>\n", "the harness does not act on <client><tool>")]
    [DataRow("<verify>\n<upload>\nx\n</upload>\n</verify>\n", "the harness records <verify><upload> only for the smtp, imap, smtps, imaps, tftp, ftp, ftps, scp and sftp servers")]
    [DataRow("<client>\n<features>\nhttp\nDebug\n</features>\n</client>\n", "Curl lacks the feature Debug")]
    [DataRow("<client>\n<features>\n!SSL\n</features>\n</client>\n", "the case needs Curl without the feature SSL")]
    [DataRow("<reply>\n<servercmd>\ndelay: 5\n</servercmd>\n</reply>\n", "the sws emulation does not carry out the server command delay")]
    [DataRow("<client>\n<file name=\"relative.txt\">\n</file>\n</client>\n", "<client><file> does not name a file by an absolute path")]
    [DataRow("<verify>\n<file3>\n</file3>\n</verify>\n", "<verify><file3> does not name a file by an absolute path")]
    [DataRow("<verify>\n<strip>\n(\n</strip>\n</verify>\n", "the strip pattern ( is not a .NET regular expression")]
    [DataRow("<verify>\n<stripfile2>\n$_ = ''\n</stripfile2>\n</verify>\n", "the harness does not run the Perl $_ = ''")]
    [DataRow("<verify>\n<errorcode>\nlots\n</errorcode>\n</verify>\n", "the expected exit code lots is not a number")]
    [DataRow("<verify>\n<errorcode>\n77,lots\n</errorcode>\n</verify>\n", "the expected exit code 77,lots is not a number")]
    [DataRow("<verify>\n<protocol>\nUSER anonymous\nLPRT 4,4,127,0,0,1,2,35,46\n</protocol>\n</verify>\n", "the FTP stand-in does not carry out the case's LPRT")]
    public void FindSkipReason_NamesWhatTheHarnessCannotDo(string sections, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test sections", sections);
        string? reason = Screen(RunnableClient + sections);
        diagnostics.Act("skip reason", reason);
        diagnostics.Assert("skip reason", expected, reason);
        Assert.AreEqual(expected, reason);
    }

    [TestMethod]
    [DataRow("sftp", "67")]
    [DataRow("scp", "60")]
    [DataRow("sftp", " 2 ")]
    [DataRow("sftp", "0")]
    public void FindSkipReason_SshCase_IsNull(string server, string errorCode)
    {
        string? reason = Screen($"<client>\n<server>\n{server}\n</server>\n<command>\n-u user: {server}://127.0.0.1:9003/f\n</command>\n</client>\n<verify>\n<errorcode>\n{errorCode}\n</errorcode>\n</verify>\n");

        Assert.IsNull(reason);
    }

    // BL-1918: the SSH stand-in serves SFTP, so an SFTP upload is screened in, never for %SFTP_PWD.
    [TestMethod]
    public void FindSkipReason_SftpUploadNamingSftpPwd_IsNull()
    {
        string? reason = Screen("<client>\n<server>\nsftp\n</server>\n<command>\n-T f sftp://127.0.0.1:9003%SFTP_PWD/f\n</command>\n</client>\n<verify>\n<upload>\nx\n</upload>\n</verify>\n");

        Assert.IsNull(reason);
    }

    // BL-1917: the SSH stand-in runs scp, so an SCP transfer, download or upload, is screened in.
    [TestMethod]
    [DataRow("")]
    [DataRow("<verify>\n<errorcode>\n0\n</errorcode>\n</verify>\n")]
    [DataRow("<verify>\n<upload>\nx\n</upload>\n</verify>\n")]
    public void FindSkipReason_ScpTransferCase_IsNull(string verify)
    {
        string? reason = Screen($"<client>\n<server>\nscp\n</server>\n<command>\n-u %USER: scp://127.0.0.1:%SSHPORT%SCP_PWD/f\n</command>\n</client>\n{verify}");

        Assert.IsNull(reason);
    }

    [TestMethod]
    public void FindSkipReason_FtpCaseVerifyingCwdFullySimulated_IsNullSinceTheStandInServesTheSimulatedDirectories()
    {
        string? reason = Screen("<client>\n<server>\nftp\n</server>\n<command>\nftp://127.0.0.1:8993/fully_simulated/UNIX/*.txt\n</command>\n</client>\n<verify>\n<protocol>\nCWD fully_simulated\n</protocol>\n</verify>\n");

        Assert.IsNull(reason);
    }

    [TestMethod]
    public void FindSkipReason_TftpServer_IsNull()
    {
        string? reason = Screen("<client>\n<server>\ntftp\n</server>\n<command>\ntftp://127.0.0.1:9003//1\n</command>\n</client>\n");

        Assert.IsNull(reason);
    }

    [TestMethod]
    [DataRow("<client>\n<server>\nhttp\nhttptls\n</server>\n<command>\na\n</command>\n</client>\n", "the harness does not emulate the httptls server")]
    [DataRow("<client>\n<name>\nno command\n</name>\n</client>\n", "the case has no <client><command>")]
    [DataRow("<client>\n<command type=\"perl\">\nx\n</command>\n</client>\n", "the harness does not run a perl command")]
    [DataRow("<client>\n<command>\nhttp://h/ | cat\n</command>\n</client>\n", "the command needs a shell for its |")]
    [DataRow("<client>\n<command>\n--ssl-no-revoke -I https://revoked.badssl.com/\n</command>\n</client>\n", "the case reaches revoked.badssl.com on the internet, which the harness does not")]
    [DataRow("<client>\n<command>\n-I http://www.example.com:8080/\n</command>\n</client>\n", "the case reaches www.example.com on the internet, which the harness does not")]
    public void FindSkipReason_ClientTheHarnessCannotRun_IsTheReason(string sections, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test sections", sections);
        string? reason = Screen(sections);
        diagnostics.Act("skip reason", reason);
        diagnostics.Assert("skip reason", expected, reason);
        Assert.AreEqual(expected, reason);
    }

    [TestMethod]
    [DataRow("<client>\n<command>\nhttp://127.0.0.1:8990/x file:///dir/x http://localhost/\n</command>\n</client>\n")]
    [DataRow("<client>\n<server>\nhttp\n</server>\n<command>\n-x http://127.0.0.1:8990 http://www.example.com/\n</command>\n</client>\n")]
    [DataRow("<client>\n<command>\n-v%08 http://example.com\n</command>\n</client>\n<verify>\n<errorcode>\n2\n</errorcode>\n</verify>\n")]
    [DataRow("<client>\n<server>\nhttp\nsocks4\n</server>\n<command>\n--socks4 127.0.0.1:8994 http://127.0.0.1:8990/x\n</command>\n</client>\n")]
    [DataRow("<client>\n<server>\nsocks5\n</server>\n<command>\n--socks5 127.0.0.1:8994 http://127.0.0.1:8990/x\n</command>\n</client>\n")]
    [DataRow("<client>\n<server>\nmqtt\n</server>\n<command>\nmqtt://127.0.0.1:8998/1190\n</command>\n</client>\n")]
    [DataRow("<client>\n<server>\nhttp\nsocks5unix\n</server>\n<command>\n--socks5 localhost/log/server/socks5.sock http://127.0.0.1:8990/x\n</command>\n</client>\n")]
    [DataRow("<client>\n<server>\nhttps-mtls\n</server>\n<command>\nhttps://localhost:9021/x\n</command>\n</client>\n")]
    [DataRow("<client>\n<setenv>\nhttp_proxy=http://127.0.0.1:8990/\n</setenv>\n<command>\nhttp://127.0.0.1:8990/x\n</command>\n</client>\n")]
    [DataRow("<client>\n<command>\nhttp://127.0.0.1:8990/x\n</command>\n<stdout>\nignored\n</stdout>\n</client>\n")]
    [DataRow("<client>\n<command>\nhttp://a-site-never-accessed.example.org/1592\n</command>\n</client>\n")]
    public void FindSkipReason_NoInternetHostOrAServerNamed_ReturnsNull(string sections)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test sections", sections);
        string? reason = Screen(sections);
        diagnostics.Act("skip reason", reason);
        diagnostics.Assert("skip reason", "null", reason ?? "null");
        Assert.IsNull(reason, reason);
    }

    [TestMethod]
    public void FindFileOutsideLogDirectory_EveryFileInside_ReturnsNull()
    {
        string logDirectory = Rooted("log");
        string sections = $"<client>\n<command>\nhttp://x/ > '{logDirectory}/out'\n</command>\n<file name=\"{logDirectory}/in\">\n</file>\n</client>\n<verify>\n<file1 name=\"{logDirectory}/sub/out\">\n</file1>\n</verify>\n";

        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test sections", Neutral(sections));
        diagnostics.Arrange("log directory", Neutral(logDirectory));
        string? reason = UpstreamCaseScreening.FindFileOutsideLogDirectory(ParsedTestCase.From(sections), logDirectory);
        diagnostics.Act("outside-file reason", reason ?? "(none)");
        diagnostics.Assert("outside-file reason", "(none)", reason ?? "(none)");
        Assert.IsNull(reason, reason);
    }

    [TestMethod]
    [DataRow("<client>\n<file name=\"{0}/in\">\n</file>\n</client>\n", "<client><file> names {0}/in, outside the case's log directory")]
    [DataRow("<verify>\n<file2 name=\"{0}/log/../out\">\n</file2>\n</verify>\n", "<verify><file2> names {0}/log/../out, outside the case's log directory")]
    [DataRow("<verify>\n<file name=\"{0}/logs/out\">\n</file>\n</verify>\n", "<verify><file> names {0}/logs/out, outside the case's log directory")]
    [DataRow("<client>\n<command>\nhttp://x/ >> '{0}/out'\n</command>\n</client>\n", "the command redirects its output to {0}/out, outside the case's log directory")]
    public void FindFileOutsideLogDirectory_FileOutside_IsTheReason(string sections, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string parent = Rooted("parent");

        diagnostics.Arrange("sections template", sections);
        string? reason = UpstreamCaseScreening.FindFileOutsideLogDirectory(ParsedTestCase.From(string.Format(CultureInfo.InvariantCulture, sections, parent)), parent + "/log");

        string expectedReason = string.Format(CultureInfo.InvariantCulture, expected, parent);
        diagnostics.Act("outside-file reason", reason);
        diagnostics.Assert("outside-file reason", Neutral(expectedReason), Neutral(reason));
        Assert.AreEqual(expectedReason, reason);
    }

    [TestMethod]
    [DataRow("<client>\n<precheck>\nmkdir /log/data ; mkdir /log/log; cp /tests/data/test1 /log/data; echo 'test:1:Test should not run' > /log/exclude\n</precheck>\n</client>\n")]
    [DataRow("<verify>\n<postcheck>\ngrep -q \"Test should not run for unit test 1182\" /log/stdout1182\n</postcheck>\n</verify>\n")]
    public void FindSkipReason_MkdirCpEchoPrecheckAndGrepPostcheck_AreInterpreted(string sections)
    {
        string? reason = Screen(RunnableClient + sections);

        Assert.DoesNotContain("the harness does not interpret", reason ?? "");
    }

    [TestMethod]
    [DataRow("<client>\n<precheck>\nmkdir /log/data; rm /log/data\n</precheck>\n</client>\n", "client><precheck")]
    [DataRow("<client>\n<precheck>\ngrep -q \"text\" /log/stdout1\n</precheck>\n</client>\n", "client><precheck")]
    [DataRow("<verify>\n<postcheck>\nmkdir /log/data\n</postcheck>\n</verify>\n", "verify><postcheck")]
    public void FindSkipReason_ShellCheckLineWithAnotherCommand_IsUninterpreted(string sections, string part)
    {
        string? reason = Screen(RunnableClient + sections);

        Assert.Contains($"the harness does not interpret the <{part}> line", reason ?? "");
    }

    [TestMethod]
    [DataRow("<client>\n<precheck>\n%PERL %SRCDIR/libtest/test610.pl mkdir /log/test610.dir\n</precheck>\n</client>\n")]
    [DataRow("<verify>\n<postcheck>\n%PERL %SRCDIR/libtest/test610.pl move /log/a /log/b rmdir /log/c rm /log/d\n</postcheck>\n</verify>\n")]
    public void FindSkipReason_Test610ScriptLines_AreNotPerlTheHarnessDoesNotRun(string sections)
    {
        string? reason = Screen(RunnableClient + sections);

        Assert.DoesNotContain("the harness does not run the Perl", reason ?? "");
    }

    [TestMethod]
    [DataRow("<client>\n<precheck>\n%PERL %SRCDIR/libtest/test613.pl prepare /log/test613.dir\n</precheck>\n</client>\n")]
    [DataRow("<verify>\n<postcheck>\n%PERL %SRCDIR/libtest/test613.pl postprocess /log/test1445.dir /log/curl1445.out 946728000\n</postcheck>\n</verify>\n")]
    public void FindSkipReason_Test613ScriptLines_AreNotPerlTheHarnessDoesNotRun(string sections)
    {
        string? reason = Screen(RunnableClient + sections);

        Assert.DoesNotContain("the harness does not run the Perl", reason ?? "");
    }

    [TestMethod]
    [DataRow("<client>\n<precheck>\nperl /tests/libtest/test613.pl prepare /log/test1445.dir\n</precheck>\n</client>\n")]
    [DataRow("<verify>\n<postcheck>\nperl /tests/libtest/test610.pl gone /log/upload.612\n</postcheck>\n</verify>\n")]
    public void FindSkipReason_ExpandedEmulatedScriptCheckLine_IsNoReasonToSkip(string sections)
    {
        string? reason = Screen(RunnableClient + sections);

        Assert.IsNull(reason);
    }

    [TestMethod]
    [DataRow("perl /src/libtest/test1013.pl ../curl-config /log/stdout1014 features > /log/result1014")]
    [DataRow("perl /src/libtest/test1022.pl ../curl-config /log/stdout1023 vernum")]
    public void FindSkipReason_CurlConfigComparisonScript_IsNoReasonToSkip(string line)
    {
        string? reason = Screen(RunnableClient + $"<verify>\n<postcheck>\n{line}\n</postcheck>\n</verify>\n");

        Assert.IsNull(reason);
    }

    [TestMethod]
    public void FindSkipReason_PerlCommandWhoseScriptHasAnInterpretation_IsNull()
    {
        string sections = "<client>\n<command type=\"perl\">\n/src/tests/test1707.pl curl /log/out1707 -h all\n</command>\n</client>\n";

        string? reason = UpstreamCaseScreening.FindSkipReason(CleanExpansion, ParsedTestCase.From(sections), Features, interpretsPerlScript: script => script == "test1707.pl");

        Assert.IsNull(reason);
    }

    [TestMethod]
    public void FindSkipReason_PerlCommandWhoseScriptHasNoInterpretation_DoesNotRunAPerlCommand()
    {
        string sections = "<client>\n<command type=\"perl\">\n/src/tests/test971.pl /src/docs\n</command>\n</client>\n";

        string? reason = UpstreamCaseScreening.FindSkipReason(CleanExpansion, ParsedTestCase.From(sections), Features, interpretsPerlScript: script => script == "test1707.pl");

        Assert.AreEqual("the harness does not run a perl command", reason);
    }

    [TestMethod]
    public void FindSkipReason_ShellCommandNamingAnInterpretedScript_DoesNotRunAShellCommand()
    {
        string sections = "<client>\n<command type=\"shell\">\ntest1707.pl\n</command>\n</client>\n";

        string? reason = UpstreamCaseScreening.FindSkipReason(CleanExpansion, ParsedTestCase.From(sections), Features, interpretsPerlScript: _ => true);

        Assert.AreEqual("the harness does not run a shell command", reason);
    }

    [TestMethod]
    [DataRow("<client>\n<command type=\"perl\">\n/src/tests/test1707.pl curl\n</command>\n</client>\n", "test1707.pl")]
    [DataRow("<client>\n<command type=\"perl\">\ntest971.pl\n</command>\n</client>\n", "test971.pl")]
    [DataRow("<client>\n<command type=\"perl\">\n</command>\n</client>\n", null)]
    [DataRow("<client>\n<command>\n/src/tests/test1707.pl\n</command>\n</client>\n", null)]
    [DataRow("<client>\n<name>\nno command\n</name>\n</client>\n", null)]
    public void PerlScriptName_Command_IsTheScriptsFileName(string sections, string? expected) =>
        Assert.AreEqual(expected, UpstreamCaseScreening.PerlScriptName(ParsedTestCase.From(sections)));

    [TestMethod]
    public void PerlScriptName_NullCase_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamCaseScreening.PerlScriptName(null!));

    private static string? Screen(string sections) =>
        UpstreamCaseScreening.FindSkipReason(CleanExpansion, ParsedTestCase.From(sections), Features);

    private static string Neutral(string? text) =>
        (text ?? "(none)").Replace(Path.GetTempPath().Replace('\\', '/'), "<temp>/");

    private static string Rooted(string name) =>
        Path.Combine(Path.GetTempPath(), name).Replace('\\', '/');

    [TestMethod]
    [DataRow("<client>\n<precheck>\nperl -e 'if(\"127.0.0.1\" !~ /[.]0[.]0[.]1$/) {print \"Test only works for HOSTIPs ending with .0.0.1\"; exit(1)}'\n</precheck>\n</client>\n")]
    [DataRow("<verify>\n<postcheck>\nperl -e 'exit((stat(\"/log/1443\"))[9] != 960898200)'\n</postcheck>\n</verify>\n")]
    [DataRow("<client>\n<precheck>\nresolve --ipv6 ::1\n</precheck>\n</client>\n")]
    public void FindSkipReason_CheckOfInterpretedOneLiners_DoesNotSkip(string sections)
    {
        Assert.IsNull(Screen(RunnableClient + sections));
    }

    [TestMethod]
    [DataRow("client", "precheck", "resolve --ipv6 ip6-localhost")]
    [DataRow("client", "precheck", "sh -c true")]
    [DataRow("verify", "postcheck", "resolve --ipv4 localhost")]
    [DataRow("verify", "postcheck", "sh -c true")]
    public void FindSkipReason_PrecheckOrPostcheckPart_IsNeverAnUnactedPart(string section, string name, string line)
    {
        string? reason = Screen(RunnableClient + $"<{section}>\n<{name}>\n{line}\n</{name}>\n</{section}>\n");

        Assert.DoesNotContain($"the harness does not act on <{section}><{name}>", reason ?? "");
    }

    [TestMethod]
    [DataRow("client", "precheck", "perl -e \"print 'x' unless 1;\"")]
    [DataRow("verify", "postcheck", "sh -c true")]
    public void FindSkipReason_CheckLineNotInterpreted_NamesTheLine(string section, string name, string line)
    {
        string? reason = Screen(RunnableClient + $"<{section}>\n<{name}>\n{line}\n</{name}>\n</{section}>\n");

        Assert.AreEqual($"the harness does not interpret the <{section}><{name}> line {line}", reason);
    }

    [TestMethod]
    [DataRow("lib678", null)]
    [DataRow("lib679", "the harness does not interpret the <client><precheck> line ./libtest/libtests lib679 check")]
    public void FindSkipReason_LibTestsPrecheckLine_RunsOnlyWhenItsProgramIsPorted(string program, string? expected)
    {
        UpstreamTestCase testCase = ParsedTestCase.From(RunnableClient + $"<client>\n<precheck>\n./libtest/libtests {program} check\n</precheck>\n</client>\n");

        string? reason = UpstreamCaseScreening.FindSkipReason(CleanExpansion, testCase, Features, isPortedLibTest: name => name == "lib678");

        Assert.AreEqual(expected, reason);
    }

    [TestMethod]
    [DataRow("https", "test-localhost.pem")]
    [DataRow("https test-localhost-san-first.pem", "test-localhost-san-first.pem")]
    public void HttpsCertificateFile_HttpsServerLine_NamesTheCertificate(string serverLine, string expected)
    {
        UpstreamTestCase testCase = ParsedTestCase.From($"<client>\n<server>\nhttp\n{serverLine}\n</server>\n<command>\nhttp://h/1\n</command>\n</client>\n");

        Assert.AreEqual(expected, UpstreamCaseScreening.HttpsCertificateFile(testCase));
        Assert.IsNull(UpstreamCaseScreening.FindSkipReason(CleanExpansion, testCase, Features));
    }

    [TestMethod]
    public void HttpsCertificateFile_NoHttpsServer_IsNull()
    {
        UpstreamTestCase testCase = ParsedTestCase.From("<client>\n<server>\nhttp\nhttps a b\n</server>\n<command>\nhttp://h/1\n</command>\n</client>\n");

        Assert.IsNull(UpstreamCaseScreening.HttpsCertificateFile(testCase));
        Assert.AreEqual("the harness does not emulate the https a b server", UpstreamCaseScreening.FindSkipReason(CleanExpansion, testCase, Features));
    }

    [TestMethod]
    public void FindSkipReason_HttpsThroughTheHttpProxy_IsNotSkipped()
    {
        UpstreamTestCase testCase = ParsedTestCase.From("<client>\n<server>\nhttps\nhttp-proxy\n</server>\n<command>\nhttp://h/1\n</command>\n</client>\n");

        string? reason = UpstreamCaseScreening.FindSkipReason(CleanExpansion, testCase, Features);

        Assert.IsNull(reason);
    }

    [TestMethod]
    public void FindSkipReason_HttpProxyWithoutHttps_IsNotSkippedForTheTunnel()
    {
        UpstreamTestCase testCase = ParsedTestCase.From("<client>\n<server>\nhttp-proxy\n</server>\n<command>\nhttp://h/1\n</command>\n</client>\n");

        Assert.IsNull(UpstreamCaseScreening.FindSkipReason(CleanExpansion, testCase, Features));
    }

    // ADR-0488: a case another platform's Curl can run is skipped here for that platform's feature
    // only; one no platform's Curl can run, or one this platform can, is not.
    [TestMethod]
    [DataRow("SSL\nSchannel", "linux", "Curl lacks the feature Schannel")]
    [DataRow("SSL\nSchannel", "darwin", "Curl lacks the feature Schannel")]
    [DataRow("!Schannel", "MSWin32", "the case needs Curl without the feature Schannel")]
    [DataRow("OpenSSL", "MSWin32", "Curl lacks the feature OpenSSL")]
    [DataRow("SSL\nSchannel", "MSWin32", null)]
    [DataRow("Schannel\nDebug", "MSWin32", null)]
    [DataRow("Schannel\nDebug", "linux", null)]
    [DataRow("Schannel\nOpenSSL", "linux", null)]
    [DataRow("http", "linux", null)]
    public void FindFeatureOfAnotherPlatform_CaseFeatures_NamesOnlyAFeatureAnotherPlatformMeets(string caseFeatures, string operatingSystemName, string? expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCurlPlatform platform = UpstreamCurlPlatform.All.Single(candidate => candidate.OperatingSystemName == operatingSystemName);
        UpstreamTestCase testCase = ParsedTestCase.From($"<client>\n<features>\n{caseFeatures}\n</features>\n</client>\n");
        diagnostics.Arrange("case features", caseFeatures);
        diagnostics.Arrange("platform", operatingSystemName);

        string? reason = UpstreamCaseScreening.FindFeatureOfAnotherPlatform(testCase, platform.Features);

        diagnostics.Act("reason", reason);
        diagnostics.Assert("reason", expected ?? "null", reason ?? "null");
        Assert.AreEqual(expected, reason);
    }

    // ADR-0497: a feature the harness stands in for (Debug) is met on every platform, so a case needing it
    // and another platform's feature is skipped here for that feature only.
    [TestMethod]
    [DataRow("Debug\nOpenSSL\nSSL", "MSWin32", "Curl lacks the feature OpenSSL")]
    [DataRow("Schannel\nDebug", "linux", "Curl lacks the feature Schannel")]
    [DataRow("Debug\nhttp/2\nSSL", "linux", null)]
    public void FindFeatureOfAnotherPlatform_PlatformWithTheDebugStandIn_CountsDebugAsMetEverywhere(string caseFeatures, string operatingSystemName, string? expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCurlPlatform platform = UpstreamCurlPlatform.All.Single(candidate => candidate.OperatingSystemName == operatingSystemName).WithFeatures(["Debug"]);
        UpstreamTestCase testCase = ParsedTestCase.From($"<client>\n<features>\n{caseFeatures}\n</features>\n</client>\n");
        diagnostics.Arrange("case features", caseFeatures);
        diagnostics.Arrange("platform", $"{operatingSystemName} with Debug");

        string? reason = UpstreamCaseScreening.FindFeatureOfAnotherPlatform(testCase, platform.Features);

        diagnostics.Act("reason", reason);
        diagnostics.Assert("reason", expected ?? "null", reason ?? "null");
        Assert.AreEqual(expected, reason);
    }

    [TestMethod]
    public void FindFeatureOfAnotherPlatform_NullArgument_Throws()
    {
        UpstreamTestCase testCase = ParsedTestCase.From("<client>\n</client>\n");

        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamCaseScreening.FindFeatureOfAnotherPlatform(null!, Features));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamCaseScreening.FindFeatureOfAnotherPlatform(testCase, null!));
    }
}
