using System.Net;

using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="HttpSignatureHttpAuthenticator" />: a request with
/// <see cref="HttpAuthRequest.HttpSignature" /> is signed before any challenge with curl 8.22.0's
/// <c>Signature-Input</c> and <c>Signature</c> lines and never answers one, and every other request
/// goes to the other schemes (BL-2182). The ed25519 signature is upstream test 5001's, signed
/// with <c>created=0</c> through a clock stopped at the Unix epoch.
/// </summary>
[TestClass]
public sealed class HttpSignatureHttpAuthenticatorTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string Ed25519Key = "9f8362f87a484a954e6e740c5b4c0e84229139a20aa8ab56ff66586f6a7d29c5";

    private const string HmacKey = "00112233445566778899aabbccddeeff";

    [TestMethod]
    public async Task CreateAuthorizationAsync_Ed25519WithQuery_SignsAsUpstreamTest5001AndReportsWhatCurlPrints()
    {
        RecordingEvents events = new();
        HttpAuthRequest request = SignedRequest("http://example.com:8000/5001/resource?action=read", new("ed25519", Ed25519Key, "my-key-1", null), "example.com:8000") with { Events = events };
        Diagnostics.Arrange("url", request.Url);

        string? value = await Authenticator().CreateAuthorizationAsync(request, [], CancellationToken.None);

        Diagnostics.Act("value", value);
        const string parameters = "(\"@method\" \"@authority\" \"@path\" \"@query\");created=0;keyid=\"my-key-1\";alg=\"ed25519\"";
        const string signature = "sig1=:RQniOeqmwdRzGvoDIMJ8XJha75evJWgqo5/66EeuJeEGczZtnP2U/F52Lzd/y7Vd1DCb8oUcCKHrKi2VJI7lBA==:";
        Diagnostics.Assert("value", "Signature-Input: sig1=" + parameters + "\r\nSignature: " + signature, value);
        Assert.AreEqual("Signature-Input: sig1=" + parameters + "\r\nSignature: " + signature, value);
        Assert.HasCount(5, events.Lines);
        Assert.AreEqual("httpsig: Signature-Input params: " + parameters, events.Lines[0]);
        StringAssert.StartsWith(events.Lines[1], "httpsig: Signature base: [\"@method\": GET\n", StringComparison.Ordinal);
        StringAssert.EndsWith(events.Lines[1], "\"@signature-params\": " + parameters + "]", StringComparison.Ordinal);
        Assert.AreEqual("httpsig: Signature-Input: sig1=" + parameters, events.Lines[2]);
        Assert.AreEqual("httpsig: Signature: " + signature, events.Lines[3]);
        Assert.AreEqual("Server auth using HTTPSIG with user ''", events.Lines[4]);
    }

    [TestMethod]
    public void CreateAuthorization_HmacSha256_SignsTheBaseOfUpstreamTest5002WithTheKey()
    {
        HttpAuthRequest request = SignedRequest("http://example.com:7000/5002/resource", new("hmac-sha256", HmacKey, "shared-key-1", null), "example.com:7000");
        Diagnostics.Arrange("url", request.Url);

        string? value = Authenticator().CreateAuthorization(request, []);

        const string parameters = "(\"@method\" \"@authority\" \"@path\");created=0;keyid=\"shared-key-1\";alg=\"hmac-sha256\"";
        string signatureBase = "\"@method\": GET\n\"@authority\": example.com:7000\n\"@path\": /5002/resource\n\"@signature-params\": " + parameters;
        string signature = Convert.ToBase64String(System.Security.Cryptography.HMACSHA256.HashData(Convert.FromHexString(HmacKey), System.Text.Encoding.ASCII.GetBytes(signatureBase)));
        string expected = "Signature-Input: sig1=" + parameters + "\r\nSignature: sig1=:" + signature + ":";
        Diagnostics.Assert("value", expected, value);
        Assert.AreEqual(expected, value);
    }

    [TestMethod]
    public void CreateAuthorization_SignatureHeaderGiven_SignsNothingButReportsTheAuthLineWithTheUser()
    {
        RecordingEvents events = new();
        HttpAuthRequest request = SignedRequest("http://example.com/x", new("ed25519", Ed25519Key, "k", null), "example.com", ["Signature: mine"]) with { Events = events, Credential = new NetworkCredential("u", "p") };
        Diagnostics.Arrange("headers", "Signature: mine");

        string? value = Authenticator().CreateAuthorization(request, []);

        Diagnostics.Assert("value", null, value);
        Assert.IsNull(value);
        CollectionAssert.AreEqual(new[] { "Server auth using HTTPSIG with user 'u'" }, events.Lines);
    }

    [TestMethod]
    [DataRow("zz", "httpsig: invalid hex at position 0 ('zz')", DisplayName = "bad hex")]
    [DataRow("00", "httpsig: ed25519 requires a 32-byte key (got 1)", DisplayName = "short key")]
    public void CreateAuthorization_LibraryRefuses_FailsWithExit43AndItsMessage(string key, string message)
    {
        HttpAuthRequest request = SignedRequest("http://example.com/x", new("ed25519", key, "k", null), "example.com");
        Diagnostics.Arrange("key", key);

        HttpAuthenticationFailedException failure = Assert.ThrowsExactly<HttpAuthenticationFailedException>(() => Authenticator().CreateAuthorization(request, []));

        Diagnostics.Assert("message", message, failure.Message);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, failure.ExitCode);
        Assert.AreEqual(message, failure.Message);
    }

    [TestMethod]
    public void CreateAuthorization_ChallengeGiven_AnswersNothing()
    {
        HttpAuthRequest request = SignedRequest("http://example.com/x", new("ed25519", Ed25519Key, "k", null), "example.com");
        Diagnostics.Arrange("challenge", "Basic");

        string? value = Authenticator().CreateAuthorization(request, ["Basic"]);

        Diagnostics.Assert("value", null, value);
        Assert.IsNull(value);
    }

    [TestMethod]
    public async Task EveryCall_WithSignature_NeverReachesTheOtherSchemes()
    {
        OtherSchemes other = new();
        HttpSignatureHttpAuthenticator authenticator = new(other, new FixedUtcClock(DateTimeOffset.UnixEpoch));
        HttpAuthRequest request = SignedRequest("http://example.com/x", new("ed25519", Ed25519Key, "k", null), "example.com");
        Diagnostics.Arrange("url", request.Url);

        string? continued = await authenticator.ContinueAuthorizationAsync(request, "x", true, ["Basic"], CancellationToken.None);
        string repeated = authenticator.RepeatAuthorization(request, "sent");
        authenticator.EndAuthorization("sent");

        Diagnostics.Assert("repeated", "sent", repeated);
        Assert.IsNull(continued);
        Assert.AreEqual("sent", repeated);
        CollectionAssert.AreEqual(new[] { "sent" }, other.Ended);
    }

    [TestMethod]
    public async Task EveryCall_WithoutSignature_GoesToTheOtherSchemes()
    {
        HttpSignatureHttpAuthenticator authenticator = Authenticator();
        HttpAuthRequest request = new("GET", CurlUrl.Parse("http://example.com/x"), "/x", null, null, HttpAuthSchemes.Basic, IsProxy: false);
        Diagnostics.Arrange("url", request.Url);

        string? created = authenticator.CreateAuthorization(request, []);
        string? createdAsync = await authenticator.CreateAuthorizationAsync(request, [], CancellationToken.None);
        string? continued = await authenticator.ContinueAuthorizationAsync(request, "x", true, ["Basic"], CancellationToken.None);
        string repeated = authenticator.RepeatAuthorization(request, "sent");

        Diagnostics.Assert("created", "sync", created);
        Assert.AreEqual("sync", created);
        Assert.AreEqual("async", createdAsync);
        Assert.AreEqual("continued", continued);
        Assert.AreEqual("repeated sent", repeated);
    }

    [TestMethod]
    public async Task EveryCall_NullRequest_Throws()
    {
        HttpSignatureHttpAuthenticator authenticator = Authenticator();
        Diagnostics.Arrange("request", "(null)");

        Assert.ThrowsExactly<ArgumentNullException>(() => authenticator.CreateAuthorization(null!, []));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await authenticator.CreateAuthorizationAsync(null!, [], CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await authenticator.ContinueAuthorizationAsync(null!, "x", true, ["y"], CancellationToken.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => authenticator.RepeatAuthorization(null!, "x"));
    }

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private static HttpSignatureHttpAuthenticator Authenticator() =>
        new(new OtherSchemes(), new FixedUtcClock(DateTimeOffset.UnixEpoch));

    private static HttpAuthRequest SignedRequest(string url, HttpSignatureSettings settings, string authority, IReadOnlyList<string>? headers = null) =>
        new("GET", CurlUrl.Parse(url), "/", null, null, HttpAuthSchemes.Basic, IsProxy: false)
        {
            HttpSignature = new HttpSignatureInputs(settings, authority, headers ?? []),
        };

    /// <summary>A clock stopped at one instant.</summary>
    private sealed class FixedUtcClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>Stands for the other schemes, answering each call with its own name.</summary>
    private sealed class OtherSchemes : IHttpAuthenticator
    {
        public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges) => "sync";

        public ValueTask<string?> CreateAuthorizationAsync(HttpAuthRequest request, IReadOnlyList<string> challenges, CancellationToken cancellationToken) =>
            ValueTask.FromResult<string?>("async");

        public ValueTask<string?> ContinueAuthorizationAsync(HttpAuthRequest request, string sentAuthorization, bool sentBeforeAnyChallenge, IReadOnlyList<string> challenges, CancellationToken cancellationToken) =>
            ValueTask.FromResult<string?>("continued");

        public string RepeatAuthorization(HttpAuthRequest request, string sentAuthorization) => "repeated " + sentAuthorization;

        public List<string> Ended { get; } = [];

        public void EndAuthorization(string sentAuthorization) => Ended.Add(sentAuthorization);
    }

    /// <summary>Records every <c>-v</c> line reported.</summary>
    private sealed class RecordingEvents : ITransferEvents
    {
        public List<string> Lines { get; } = [];

        public void ReportInfo(string text) => Lines.Add(text);

        public void ReportConnectionOpened(ConnectionOpenedEvent opened)
        {
        }

        public void ReportConnectionReused(ConnectionReusedEvent reused)
        {
        }

        public void ReportTlsHandshake(TlsHandshakeEvent handshake)
        {
        }

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent)
        {
        }

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportDataSent(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportDataReceived(ReadOnlySpan<byte> bytes)
        {
        }
    }
}
