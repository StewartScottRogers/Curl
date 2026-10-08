using System.Net;
using System.Text;
using Curl.Authentication.Fakes;
using Curl.Kerberos;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Authentication;

/// <summary>
/// Pins what <see cref="NetrcFile" />, <see cref="AwsSigV4Signer" /> and
/// <see cref="NegotiateHttpAuthenticator" /> write to the diagnostic log, component <c>auth</c>
/// (BL-1151, ADR-0222): the netrc entry matched by host and login and the SigV4 scope at
/// <c>info</c>, each Negotiate round at <c>verbose</c>, a failed Negotiate context at
/// <c>warning</c>, and never a password, secret key, signature or token's base64.
/// </summary>
[TestClass]
public sealed class AuthDiagnosticLogNetrcSigV4NegotiateTests
{
    private const string Secret = "s3cret";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("machine example.test login alice password s3cret\n", "netrc entry matched for host example.test, login alice", DisplayName = "With a login")]
    [DataRow("machine example.test password s3cret\n", "netrc entry matched for host example.test, login (none)", DisplayName = "Without a login")]
    public void NetrcFind_EntryMatches_LogsTheHostAndLoginAtInfoAndNotThePassword(string text, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("netrc", text);
        diagnostics.Arrange("host", "example.test");
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);

        NetrcLookupResult result = NetrcFile.Find(text, "example.test", userName: null, log);

        diagnostics.Act("outcome", result.Outcome);
        diagnostics.Act("log", Joined(log));
        diagnostics.Diff("info line", expected, string.Join(" | ", log.At(DiagnosticLogLevel.Info)));
        Assert.AreEqual(NetrcLookupOutcome.Found, result.Outcome);
        CollectionAssert.AreEqual(new[] { expected }, log.At(DiagnosticLogLevel.Info));
        Assert.AreEqual(DiagnosticLogComponents.Auth, log.Lines[0].Component);
        AssertNever(log, Secret);
    }

    [TestMethod]
    [DataRow("machine other.test login alice password s3cret\n", DisplayName = "No entry")]
    [DataRow("machine example.test login \"alice\n", DisplayName = "Syntax error")]
    public void NetrcFind_NoEntryMatches_LogsNothing(string text)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("netrc", text);
        diagnostics.Arrange("host", "example.test");
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);

        NetrcLookupResult result = NetrcFile.Find(text, "example.test", userName: null, log);

        diagnostics.Act("outcome", result.Outcome);
        diagnostics.Act("log", Joined(log));
        diagnostics.Assert("line count", 0, log.Lines.Count);
        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public void Sign_WithALog_LogsTheScopeAtInfoAndNeitherTheSecretKeyNorTheSignature()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("sigv4", "aws:amz:us-east-1:s3");
        diagnostics.Arrange("credentials", "AKID:" + Secret);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        AwsSigV4Signer signer = new(new FixedTimeProvider(FakeKdc.Now), Encoding.UTF8, log);

        AwsSigV4SigningResult result = signer.Sign(new AwsSigV4Request
        {
            SigV4Parameter = "aws:amz:us-east-1:s3",
            UserName = "AKID",
            Password = Secret,
            Method = "GET",
            HostName = "example.test",
            HostHeaderValue = "example.test",
            Path = "/",
        });

        diagnostics.Act("authorization", result.HeaderLines[0]);
        diagnostics.Act("log", Joined(log));
        diagnostics.Diff("info line", "AWS SigV4 scope: provider aws:amz, region us-east-1, service s3", string.Join(" | ", log.At(DiagnosticLogLevel.Info)));
        CollectionAssert.AreEqual(new[] { "AWS SigV4 scope: provider aws:amz, region us-east-1, service s3" }, log.At(DiagnosticLogLevel.Info));
        Assert.HasCount(1, log.Lines);
        string authorization = result.HeaderLines[0];
        string signature = authorization[(authorization.IndexOf("Signature=", StringComparison.Ordinal) + "Signature=".Length)..];
        AssertNever(log, Secret);
        AssertNever(log, signature);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_ContextNeedsAnotherRound_LogsTheRoundAtVerboseWithoutTheToken()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("context status", SecurityContextStatus.ContinueNeeded);
        diagnostics.Bytes("scripted token", [0x60, 0x82, 0x01]);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x60, 0x82, 0x01]));

        string? value = await new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context), diagnosticLog: log).CreateAuthorizationAsync(Request(), CancellationToken.None);

        diagnostics.Act("authorization", value);
        diagnostics.Act("log", Joined(log));
        diagnostics.Diff("verbose line", "Negotiate needs another round", string.Join(" | ", log.At(DiagnosticLogLevel.Verbose)));
        CollectionAssert.AreEqual(new[] { "Negotiate needs another round" }, log.At(DiagnosticLogLevel.Verbose));
        Assert.HasCount(1, log.Lines);
        AssertNever(log, value!["Negotiate ".Length..]);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x01 }, DisplayName = "With a final token")]
    [DataRow(new byte[0], DisplayName = "Without a token")]
    public async Task CreateAuthorizationAsync_ContextCompletes_LogsCompletedAtVerbose(byte[] token)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("context status", SecurityContextStatus.Completed);
        diagnostics.Bytes("final token", token);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.Completed, token));

        string? value = await new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context), diagnosticLog: log).CreateAuthorizationAsync(Request(), CancellationToken.None);

        diagnostics.Act("authorization", value ?? "(null)");
        diagnostics.Act("log", Joined(log));
        diagnostics.Diff("verbose line", "Negotiate completed", string.Join(" | ", log.At(DiagnosticLogLevel.Verbose)));
        CollectionAssert.AreEqual(new[] { "Negotiate completed" }, log.At(DiagnosticLogLevel.Verbose));
        Assert.HasCount(1, log.Lines);
    }

    [TestMethod]
    public async Task ContinueAuthorizationAsync_SecondRoundCompletes_LogsBothRoundsAtVerbose()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("server challenge", "Negotiate BA==");
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        ScriptedSecurityContext context = new(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]),
            new SecurityContextStep(SecurityContextStatus.Completed, [0x02]));
        NegotiateHttpAuthenticator authenticator = new(new ScriptedSecurityContextFactory(context), diagnosticLog: log);
        string? sent = await authenticator.CreateAuthorizationAsync(Request(), CancellationToken.None);

        await authenticator.ContinueAuthorizationAsync(Request(), sent!, ["Negotiate BA=="], CancellationToken.None);

        diagnostics.Act("first authorization", sent);
        diagnostics.Act("log", Joined(log));
        diagnostics.Diff("verbose lines", "Negotiate needs another round | Negotiate completed", string.Join(" | ", log.At(DiagnosticLogLevel.Verbose)));
        CollectionAssert.AreEqual(new[] { "Negotiate needs another round", "Negotiate completed" }, log.At(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_ContextFails_LogsItsStatusAtWarning()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("context status", SecurityContextStatus.NoCredentials);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.NoCredentials, []));

        string? value = await new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context), diagnosticLog: log).CreateAuthorizationAsync(Request(), CancellationToken.None);

        diagnostics.Act("authorization", value ?? "(null)");
        diagnostics.Act("log", Joined(log));
        diagnostics.Diff("warning line", "Negotiate context failed: NoCredentials", string.Join(" | ", log.At(DiagnosticLogLevel.Warning)));
        CollectionAssert.AreEqual(new[] { "Negotiate context failed: NoCredentials" }, log.At(DiagnosticLogLevel.Warning));
        Assert.HasCount(1, log.Lines);
    }

    [TestMethod]
    public async Task StepWithoutAnsweringAsync_ContextFails_LogsItsStatusAtWarning()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("context status", SecurityContextStatus.NoMechanism);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Warning);
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.NoMechanism, []));

        await new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context), diagnosticLog: log).StepWithoutAnsweringAsync(Request(), ["Negotiate"], CancellationToken.None);

        diagnostics.Act("log", Joined(log));
        diagnostics.Diff("warning line", "Negotiate context failed: NoMechanism", string.Join(" | ", log.At(DiagnosticLogLevel.Warning)));
        CollectionAssert.AreEqual(new[] { "Negotiate context failed: NoMechanism" }, log.At(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_LogAtInfo_WritesNoRound()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Info);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]));

        string? value = await new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context), diagnosticLog: log).CreateAuthorizationAsync(Request(), CancellationToken.None);

        diagnostics.Act("authorization", value ?? "(null)");
        diagnostics.Act("log", Joined(log));
        diagnostics.Assert("line count", 0, log.Lines.Count);
        Assert.IsEmpty(log.Lines);
    }

    private static HttpAuthRequest Request() =>
        new("GET", CurlUrl.Parse("http://server.example.test/"), "/", new NetworkCredential("u", Secret), null, HttpAuthSchemes.Negotiate, IsProxy: false);

    private static string Joined(RecordingDiagnosticLog log)
    {
        List<string> messages = [];
        foreach ((_, _, string message) in log.Lines)
        {
            messages.Add(message);
        }

        return string.Join(" | ", messages);
    }

    private static void AssertNever(RecordingDiagnosticLog log, string forbidden)
    {
        foreach ((_, _, string message) in log.Lines)
        {
            Assert.DoesNotContain(forbidden, message);
        }
    }
}
