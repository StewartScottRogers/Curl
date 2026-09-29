using System.Net;
using System.Text;

using Curl.Authentication;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="AwsSigV4HttpAuthenticator" />: a request with <see cref="HttpAuthRequest.AwsSigV4" />
/// is signed before any challenge and never answers one, and every other request goes to the
/// other schemes (BL-629). The signature is the one curl 8.21.0 sent for
/// <c>-sv -u AKID:SECRET --aws-sigv4 aws:amz:us-east-1:s3 http://127.0.0.1:18633/x</c> at
/// 2026-09-29T20:42:00Z, with the <c>-v</c> lines it printed (measured, BL-629 Notes).
/// </summary>
[TestClass]
public sealed class AwsSigV4HttpAuthenticatorTests
{
    private const string Signature = "65b76dcb57fed633eed53e8515f9b2e6a53bbe4d8d5c792def45eebd25da8846";

    private static readonly CurlUrl Url = CurlUrl.Parse("http://127.0.0.1:18633/x");

    private static readonly DateTimeOffset Measured = new(2026, 9, 29, 20, 42, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task CreateAuthorizationAsync_AwsSigV4BeforeAnyChallenge_SignsAndReportsWhatCurlPrints()
    {
        RecordingEvents events = new();
        HttpAuthRequest request = SignedRequest() with { Events = events };

        string? value = await Authenticator().CreateAuthorizationAsync(request, [], CancellationToken.None);

        Assert.AreEqual(
            $"AWS4-HMAC-SHA256 Credential=AKID/20260929/us-east-1/s3/aws4_request, SignedHeaders=host;x-amz-content-sha256;x-amz-date, Signature={Signature}\r\n"
            + "X-Amz-Date: 20260929T204200Z\r\n"
            + "x-amz-content-sha256: e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            value);
        CollectionAssert.AreEqual(
            new[]
            {
                "aws_sigv4: String to sign (enclosed in []) - [AWS4-HMAC-SHA256\n20260929T204200Z\n20260929/us-east-1/s3/aws4_request\ne8d1e6c370aff2389eacec848783f05e19624004eee6d4357afa7d27b3ce09d9]",
                "aws_sigv4: Signature - " + Signature,
                "Server auth using AWS_SIGV4 with user 'AKID'",
            },
            events.Lines);
    }

    [TestMethod]
    public void CreateAuthorization_AwsSigV4_SignsAsTheAsynchronousCallDoes()
    {
        string? value = Authenticator().CreateAuthorization(SignedRequest(), []);

        StringAssert.StartsWith(value, "AWS4-HMAC-SHA256 Credential=AKID/", StringComparison.Ordinal);
    }

    /// <summary>
    /// Measured: an <c>-H</c> <c>Authorization</c> leaves the request unsigned, and curl still
    /// prints <c>Server auth using AWS_SIGV4 with user 'AKID'</c>.
    /// </summary>
    [TestMethod]
    public void CreateAuthorization_CustomAuthorizationHeader_SendsNoValueButReportsTheScheme()
    {
        RecordingEvents events = new();
        HttpAuthRequest request = SignedRequest(["Authorization: mine"]) with { Events = events };

        string? value = Authenticator().CreateAuthorization(request, []);

        Assert.IsNull(value);
        CollectionAssert.AreEqual(new[] { "Server auth using AWS_SIGV4 with user 'AKID'" }, events.Lines);
    }

    [TestMethod]
    public void CreateAuthorization_AwsSigV4AnsweringAChallenge_SendsNothing()
    {
        Assert.IsNull(Authenticator().CreateAuthorization(SignedRequest(), ["AWS4-HMAC-SHA256"]));
    }

    [TestMethod]
    public void CreateAuthorization_AwsSigV4WithoutACredential_SendsNothing()
    {
        Assert.IsNull(Authenticator().CreateAuthorization(SignedRequest() with { Credential = null }, []));
    }

    [TestMethod]
    public void CreateAuthorization_AwsSigV4TheSignerRefuses_FailsWithItsExitCodeAndMessage()
    {
        HttpAuthRequest request = SignedRequest() with { Url = CurlUrl.Parse("http://localhost:18631/x") };
        request = request with { AwsSigV4 = request.AwsSigV4! with { Parameter = "aws" } };

        HttpAuthenticationFailedException failure = Assert.ThrowsExactly<HttpAuthenticationFailedException>(() => Authenticator().CreateAuthorization(request, []));

        Assert.AreEqual(CurlExitCode.UrlMalformat, failure.ExitCode);
        Assert.AreEqual("aws-sigv4: service missing in parameters and hostname", failure.Message);
    }

    [TestMethod]
    public async Task ContinueAuthorizationAsync_AwsSigV4_AnswersNothing()
    {
        Assert.IsNull(await Authenticator().ContinueAuthorizationAsync(SignedRequest(), "x", true, ["y"], CancellationToken.None));
    }

    [TestMethod]
    public async Task EveryCall_WithoutAwsSigV4_GoesToTheOtherSchemes()
    {
        HttpAuthRequest request = SignedRequest() with { AwsSigV4 = null };
        AwsSigV4HttpAuthenticator authenticator = Authenticator();

        Assert.AreEqual("sync", authenticator.CreateAuthorization(request, []));
        Assert.AreEqual("async", await authenticator.CreateAuthorizationAsync(request, [], CancellationToken.None));
        Assert.AreEqual("continued", await authenticator.ContinueAuthorizationAsync(request, "x", true, ["y"], CancellationToken.None));
    }

    [TestMethod]
    public async Task EveryCall_NullRequest_Throws()
    {
        AwsSigV4HttpAuthenticator authenticator = Authenticator();

        Assert.ThrowsExactly<ArgumentNullException>(() => authenticator.CreateAuthorization(null!, []));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await authenticator.CreateAuthorizationAsync(null!, [], CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await authenticator.ContinueAuthorizationAsync(null!, "x", true, ["y"], CancellationToken.None));
    }

    private static AwsSigV4HttpAuthenticator Authenticator() =>
        new(new OtherSchemes(), new AwsSigV4Signer(new FixedUtcClock(Measured), Encoding.Latin1));

    private static HttpAuthRequest SignedRequest(IReadOnlyList<string>? headers = null) =>
        new("GET", Url, "/x", new NetworkCredential("AKID", "SECRET"), null, HttpAuthSchemes.Basic, IsProxy: false)
        {
            AwsSigV4 = new AwsSigV4Inputs("aws:amz:us-east-1:s3", "127.0.0.1:18633", headers ?? []) { IsGetOrHead = true },
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
