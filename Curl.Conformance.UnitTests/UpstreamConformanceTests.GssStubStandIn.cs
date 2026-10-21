using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

public sealed partial class UpstreamConformanceTests
{
    // Upstream's debug build links a stub GSS-API (stub_gss_init_sec_context in lib/curl_gssapi.c at curl-8_22_0) that
    // reads CURL_STUB_GSS_CREDS in place of a ticket; the Negotiate cases 2056, 2057 and 2094 rely on it. The harness
    // stands in for it with GssStubSecurityContexts, which RunCurlAsync hands curl when a case sets the variable, so such
    // a case gets Debug (BL-2836). The curl tool and Libcurl never read the variable.
    internal const string StubGssCredentialsVariableName = "CURL_STUB_GSS_CREDS";

    private static bool SetsStubGssCredentialsVariable(string text) =>
        text.Contains("\n" + StubGssCredentialsVariableName + "=", StringComparison.Ordinal);

    internal static ISecurityContextFactory? SecurityContextsFor(IReadOnlyDictionary<string, string> environment) =>
        environment.TryGetValue(StubGssCredentialsVariableName, out string? credentials) ? new GssStubSecurityContexts(credentials) : null;

    [TestMethod]
    [DataRow("CURL_STUB_GSS_CREDS=\"KRB5_Alice\"\n", true)]
    [DataRow("CURL_STUB_GSS_CREDS=\"NTLM_Alice\"\n", true)]
    [DataRow("CURL_OTHER=\"KRB5_Alice\"\n", false)]
    public void PlatformFor_CaseSettingStubGssCredentials_ReportsDebugOnlyThen(string setenv, bool reportsDebug)
    {
        byte[] testFile = Encoding.Latin1.GetBytes($"<testcase>\n<client>\n<server>\nhttp\n</server>\n<setenv>\n{setenv}</setenv>\n</client>\n</testcase>\n");

        UpstreamCurlPlatform platform = PlatformFor(testFile);

        Assert.AreEqual(reportsDebug, platform.Features.Contains("Debug"));
    }

    [TestMethod]
    public async Task GssStub_Krb5Credentials_FirstTokenIsTheOneTest2056Expects()
    {
        string testFile = await File.ReadAllTextAsync(Path.Combine(UpstreamTestDataFolder, "test2056" + UpstreamTestFileExtension), Encoding.Latin1);
        int start = testFile.IndexOf("%b64[", StringComparison.Ordinal) + "%b64[".Length;
        string expected = testFile[start..testFile.IndexOf("]b64%", start, StringComparison.Ordinal)];
        using ISecurityContext context = new GssStubSecurityContexts("\"KRB5_Alice\"").Create(new SecurityContextRequest(SecurityMechanism.Negotiate, "HTTP", "127.0.0.1"));

        SecurityContextStep step = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        Assert.AreEqual(SecurityContextStatus.ContinueNeeded, step.Status);
        Assert.AreEqual(expected, Encoding.Latin1.GetString(step.Token));
        Assert.AreEqual(187, step.Token.Length);
    }

    [TestMethod]
    public async Task GssStub_NtlmCredentialsOnly_FailsWithNoMechanismAsTest2057Expects()
    {
        using ISecurityContext context = new GssStubSecurityContexts("\"NTLM_Alice\"").Create(new SecurityContextRequest(SecurityMechanism.Negotiate, "HTTP", "127.0.0.1"));

        SecurityContextStep step = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        Assert.AreEqual(SecurityContextStatus.NoMechanism, step.Status);
        Assert.IsEmpty(step.Token);
    }

    [TestMethod]
    [DataRow("D", SecurityContextStatus.Completed, true)]
    [DataRow("X", SecurityContextStatus.Refused, false)]
    public async Task GssStub_ServerTokenAfterTheFirst_CompletesOnlyOnD(string serverToken, SecurityContextStatus expected, bool completes)
    {
        using ISecurityContext context = new GssStubSecurityContexts("\"KRB5_Alice\"").Create(new SecurityContextRequest(SecurityMechanism.Negotiate, "HTTP", "127.0.0.1"));
        await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        SecurityContextStep step = await context.NextTokenAsync(Encoding.Latin1.GetBytes(serverToken), CancellationToken.None);

        Assert.AreEqual(expected, step.Status);
        Assert.AreEqual(completes, context.IsCompleted);
    }

    [TestMethod]
    public void SecurityContextsFor_StubCredentialsSetOrNot_GivesTheStubOnlyThen()
    {
        Assert.IsInstanceOfType<GssStubSecurityContexts>(SecurityContextsFor(new Dictionary<string, string> { [StubGssCredentialsVariableName] = "\"KRB5_Alice\"" }));
        Assert.IsNull(SecurityContextsFor(new Dictionary<string, string>()));
    }

    /// <summary>
    /// The debug build's stub GSS-API under SPNEGO: a first token of <c>&lt;creds&gt;:&lt;service&gt;@&lt;host&gt;:1:</c>
    /// padded with <c>A</c> to 187 bytes when the credentials name <c>KRB5</c>, no mechanism otherwise (SPNEGO allows
    /// krb5 alone), and a server token of <c>D</c> completing the context.
    /// </summary>
    private sealed class GssStubSecurityContexts(string credentials) : ISecurityContextFactory
    {
        public ISecurityContext Create(SecurityContextRequest request) => new GssStubSecurityContext(credentials, $"{request.ServiceName}@{request.HostName}");
    }

    private sealed class GssStubSecurityContext(string credentials, string target) : ISecurityContext
    {
        // The stub's token length: APPROX_TOKEN_LEN (250) * 3 / 4.
        private const int TokenLength = 187;

        // The stub's ctx->sent value for a krb5 token.
        private const int Krb5TokenType = 1;

        public bool IsCompleted { get; private set; }

        public ValueTask<SecurityContextStep> NextTokenAsync(ReadOnlyMemory<byte> incomingToken, CancellationToken cancellationToken) =>
            ValueTask.FromResult(incomingToken.IsEmpty ? FirstStep() : StepOn(incomingToken.Span));

        private SecurityContextStep FirstStep() =>
            credentials.Contains("KRB5", StringComparison.Ordinal)
                ? new(SecurityContextStatus.ContinueNeeded, Encoding.Latin1.GetBytes($"{credentials}:{target}:{Krb5TokenType}:".PadRight(TokenLength, 'A')))
                : new(SecurityContextStatus.NoMechanism, []);

        private SecurityContextStep StepOn(ReadOnlySpan<byte> incomingToken)
        {
            IsCompleted = incomingToken.SequenceEqual("D"u8);
            return new(IsCompleted ? SecurityContextStatus.Completed : SecurityContextStatus.Refused, []);
        }

        public byte[]? Wrap(ReadOnlySpan<byte> message, bool encrypt) => throw new NotSupportedException();

        public byte[]? Unwrap(ReadOnlySpan<byte> wrappedMessage) => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }
}
