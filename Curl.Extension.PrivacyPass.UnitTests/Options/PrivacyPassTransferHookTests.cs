using System.Security.Cryptography;
using Curl.Core;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;
using Curl.Extension.PrivacyPass.Http;
using Curl.Extension.PrivacyPass.Tokens;
using Curl.Extension.PrivacyPass.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.PrivacyPass.Options;

/// <summary>
/// Pins the redemption flow (RFC 9577 §2.1, §2.2; RFC 9578 §5, §6.1, §6.2, §6.3): a 401 <c>PrivateToken</c> challenge is
/// answered with a token from the issuer and the transfer sent again, anything else passes through unchanged (ADR-1110).
/// </summary>
[TestClass]
public sealed class PrivacyPassTransferHookTests
{
    private const string IssuerUrl = "https://issuer.example/token-request";

    private static readonly string VoprfWwwAuthenticate =
        $"PrivateToken challenge=\"{Base64Url.Encode(VoprfTestIssuer.Challenge)}\", token-key=\"{Base64Url.Encode(VoprfTestIssuer.EncodedPublicKey)}\"";

    [TestMethod]
    public async Task ExecuteAsync_PrivateTokenChallenge_SendsTheTransferAgainWithAVerifyingToken()
    {
        var issuer = new SigningIssuerConnection();
        List<ITransferContext> sent = [];

        var result = await Hook().ExecuteAsync(Context(), Services(new FixedConnector(issuer)), Answering(sent, Challenged()));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(2, sent);
        StringAssert.StartsWith(issuer.Head, "POST /token-request HTTP/1.1\r\nHost: issuer.example\r\n");
        StringAssert.Contains(issuer.Head, "Accept: application/private-token-response\r\nContent-Type: application/private-token-request\r\n");
        // §6.1: token_type 2 then the last byte of SHA-256(token-key).
        CollectionAssert.AreEqual(new byte[] { 0, 2, SHA256.HashData(TestIssuer.Spki)[^1] }, issuer.TokenRequest[..3]);
        var authorization = sent[1].Http!.Headers[^1];
        StringAssert.StartsWith(authorization, "Authorization: PrivateToken token=\"");
        var token = Base64Url.Decode(authorization["Authorization: PrivateToken token=\"".Length..^1])!;
        // §2.2: token_type, nonce(32), challenge_digest(32), token_key_id(32), authenticator(Nk).
        Assert.HasCount(2 + 32 + 32 + 32 + 256, token);
        CollectionAssert.AreEqual(SHA256.HashData(TestIssuer.Challenge), token[34..66]);
        CollectionAssert.AreEqual(SHA256.HashData(TestIssuer.Spki), token[66..98]);
        Assert.IsTrue(TestIssuer.Verifies(token[..98], token[98..]));
    }

    [TestMethod]
    public async Task ExecuteAsync_VoprfChallenge_SendsTheTransferAgainWithTheIssuersPrfOutput()
    {
        var issuer = new EvaluatingIssuerConnection();
        List<ITransferContext> sent = [];

        var result = await Hook().ExecuteAsync(Context(), Services(new FixedConnector(issuer)), Answering(sent, Challenged(VoprfWwwAuthenticate)));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(2, sent);
        // RFC 9578 §5.1: token_type 1 then the last byte of SHA-256(token-key).
        CollectionAssert.AreEqual(new byte[] { 0, 1, SHA256.HashData(VoprfTestIssuer.EncodedPublicKey)[^1] }, issuer.TokenRequest[..3]);
        var authorization = sent[1].Http!.Headers[^1];
        StringAssert.StartsWith(authorization, "Authorization: PrivateToken token=\"");
        var token = Base64Url.Decode(authorization["Authorization: PrivateToken token=\"".Length..^1])!;
        // §5.3: token_type, nonce(32), challenge_digest(32), token_key_id(32), authenticator(Nh = 48).
        Assert.HasCount(2 + 32 + 32 + 32 + VoprfP384Sha384.OutputLength, token);
        CollectionAssert.AreEqual(SHA256.HashData(VoprfTestIssuer.Challenge), token[34..66]);
        CollectionAssert.AreEqual(VoprfTestIssuer.Output(token[..98]), token[98..]);
    }

    [TestMethod]
    public async Task ExecuteAsync_VoprfProofNotVerifying_FailsAsAWeirdReplyWithoutSendingAgain()
    {
        List<ITransferContext> sent = [];

        var result = await Hook().ExecuteAsync(
            Context(), Services(new FixedConnector(new EvaluatingIssuerConnection(corruptProof: true))), Answering(sent, Challenged(VoprfWwwAuthenticate)));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.HasCount(1, sent);
    }

    [TestMethod]
    public void AnswerableChallenge_UnknownTypeBeforeAVoprfOne_PicksTheVoprfOne()
    {
        byte[] unknown = [0x00, 0x03, .. VoprfTestIssuer.Challenge[2..]];
        var field = $"PrivateToken challenge=\"{Base64Url.Encode(unknown)}\", token-key=\"AAAA\", {VoprfWwwAuthenticate}";

        var challenge = PrivacyPassTransferHook.AnswerableChallenge(Challenged(field));

        Assert.AreEqual(PrivacyPassIssuance.VoprfTokenType, challenge!.Challenge.TokenType);
        Assert.IsNull(PrivacyPassTransferHook.AnswerableChallenge(Challenged($"PrivateToken challenge=\"{Base64Url.Encode(unknown)}\", token-key=\"AAAA\"")));
    }

    [TestMethod]
    [DataRow(200, false, DisplayName = "not a 401")]
    [DataRow(401, true, DisplayName = "a 401 without a PrivateToken challenge")]
    public async Task ExecuteAsync_NothingToAnswer_WritesTheFirstResponseOutUnchanged(int status, bool withBasic)
    {
        var context = Context(new MemoryStream());
        List<ITransferContext> sent = [];
        var first = TransferResult.Success(4) with
        {
            Report = new TransferReport { ResponseCode = status, ResponseHeaders = withBasic ? [new("WWW-Authenticate", "Basic realm=x")] : [] },
        };

        var result = await Hook().ExecuteAsync(context, Services(new FixedConnector(new SigningIssuerConnection())), Answering(sent, first));

        Assert.AreSame(first, result);
        Assert.HasCount(1, sent);
        Assert.AreEqual("body", System.Text.Encoding.ASCII.GetString(((MemoryStream)context.Output).ToArray()));
        Assert.AreEqual("head", System.Text.Encoding.ASCII.GetString(((MemoryStream)context.HeaderOutput!).ToArray()));
    }

    [TestMethod]
    [DataRow(403, 256, CurlExitCode.LoginDenied, DisplayName = "issuer refuses")]
    [DataRow(200, 10, CurlExitCode.WeirdServerReply, DisplayName = "short response")]
    [DataRow(200, 256, CurlExitCode.WeirdServerReply, DisplayName = "signature does not verify")]
    public async Task ExecuteAsync_IssuerGivesNoToken_FailsWithoutSendingAgain(int status, int length, CurlExitCode expected)
    {
        var connector = new RecordedConnector(new RecordedConnection(TestIssuer.Response(status, new byte[length])));
        List<ITransferContext> sent = [];

        var result = await Hook().ExecuteAsync(Context(), Services(connector), Answering(sent, Challenged()));

        Assert.AreEqual(expected, result.ExitCode);
        Assert.HasCount(1, sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_IssuerUnreachable_FailsWithTheConnectExitCode()
    {
        var connector = new RecordedConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "refused"));

        var result = await Hook().ExecuteAsync(Context(), Services(connector), Answering([], Challenged()));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_TokenKeyNotAnRsaPssKey_FailsAsAWeirdReply()
    {
        var challenged = Challenged($"PrivateToken challenge=\"{Base64Url.Encode(TestIssuer.Challenge)}\", token-key=\"AAAA\"");

        var result = await Hook().ExecuteAsync(Context(), Services(new FixedConnector(new SigningIssuerConnection())), Answering([], challenged));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_IssuerOriginWhoseDirectoryListsTheKey_IssuesAtTheDirectorysRequestUrl()
    {
        var directory = new RecordedConnection(PrivacyPassIssuerDirectoryTests.Answer(200, Directory(TestIssuer.Spki)));
        var issuer = new SigningIssuerConnection();
        var connector = new SequenceConnector(directory, issuer);
        List<ITransferContext> sent = [];

        var result = await OriginHook().ExecuteAsync(Context(), Services(connector), Answering(sent, Challenged()));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(2, sent);
        StringAssert.StartsWith(directory.Written, "GET /.well-known/private-token-issuer-directory HTTP/1.1\r\nHost: issuer.example\r\n");
        StringAssert.StartsWith(issuer.Head, "POST /issue HTTP/1.1\r\nHost: issuer.example\r\n");
        Assert.AreEqual("issuer.example", connector.Targets[1].Host);
    }

    [TestMethod]
    public async Task ExecuteAsync_IssuerOriginWhoseDirectoryOmitsTheKey_FailsAsAWeirdReplyWithoutIssuing()
    {
        var connector = new SequenceConnector(new RecordedConnection(PrivacyPassIssuerDirectoryTests.Answer(200, Directory([1, 2, 3]))));
        List<ITransferContext> sent = [];

        var result = await OriginHook().ExecuteAsync(Context(), Services(connector), Answering(sent, Challenged()));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.HasCount(1, sent);
        Assert.HasCount(1, connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutServicesOrUrl_FailsInit()
    {
        var withoutServices = await Hook().ExecuteAsync(Context(), _ => throw new AssertFailedException("sent"));
        var withoutUrl = await new PrivacyPassTransferHook(null, RandomNumberGenerator.GetBytes)
            .ExecuteAsync(Context(), Services(new FixedConnector(new SigningIssuerConnection())), _ => throw new AssertFailedException("sent"));

        Assert.AreEqual(PrivacyPassTransferHook.NoServicesMessage, withoutServices.ErrorMessage);
        Assert.AreEqual(PrivacyPassTransferHook.NotAUrlMessage, withoutUrl.ErrorMessage);
        Assert.AreEqual(CurlExitCode.FailedInit, withoutUrl.ExitCode);
    }

    [TestMethod]
    public void ActivatedBy_OptionGivenOrNot_GivesAHookOnlyWhenGiven()
    {
        Assert.IsNull(PrivacyPassOptions.ActivatedBy(ExtensionOptionValues.None));
        Assert.IsInstanceOfType<PrivacyPassTransferHook>(PrivacyPassOptions.ActivatedBy(Values(IssuerUrl)));
        Assert.IsInstanceOfType<PrivacyPassTransferHook>(PrivacyPassOptions.ActivatedBy(Values("not a url")));
    }

    [TestMethod]
    public void CommandLineOptions_WithCurlExtensions0_LeaveOutPrivacyPassSoCurlRefusesIt()
    {
        Assert.DoesNotContain(PrivacyPassOptions.Issuer, ExtensionCommandLineOptions.For(name => name == "CURL_EXTENSIONS" ? "0" : null, _ => false));
        Assert.Contains(PrivacyPassOptions.Issuer, ExtensionCommandLineOptions.For(_ => null, _ => false));
        Assert.Contains(PrivacyPassOptions.Issuer, PrivacyPassOptions.Description.Options);
    }

    private static PrivacyPassTransferHook Hook() => new(new Uri(IssuerUrl), RandomNumberGenerator.GetBytes);

    private static PrivacyPassTransferHook OriginHook() => new(new Uri("https://issuer.example"), RandomNumberGenerator.GetBytes);

    private static string Directory(byte[] tokenKey) =>
        $$"""{"issuer-request-uri": "/issue", "token-keys": [{"token-type": 2, "token-key": "{{Base64Url.Encode(tokenKey)}}"}]}""";

    private static ExtensionOptionValues Values(string value) => new(new Dictionary<string, string> { [PrivacyPassOptions.Issuer.Name] = value });

    private static TransferHookServices Services(IConnector connector) => new(connector, new NoListener());

    private static TransferResult Challenged(string? field = null) => TransferResult.Success(0) with
    {
        Report = new TransferReport { ResponseCode = 401, ResponseHeaders = [new("Date", "x"), new("WWW-Authenticate", field ?? TestIssuer.WwwAuthenticate)] },
    };

    private static TransferContext Context(Stream? headerOutput = null) => new()
    {
        Url = CurlUrl.Parse("https://origin.example/resource"),
        Output = new MemoryStream(),
        HeaderOutput = headerOutput,
        DumpHeaderOutput = headerOutput is null ? null : new MemoryStream(),
        Http = new Curl.Protocol.Abstractions.HttpRequestOptions { Headers = ["X-Own: 1"] },
    };

    // The first send writes "body" and "head" and gives first; any later one succeeds.
    private static Func<ITransferContext, ValueTask<TransferResult>> Answering(List<ITransferContext> sent, TransferResult first) => async context =>
    {
        sent.Add(context);
        if (sent.Count > 1)
        {
            return TransferResult.Success(0);
        }

        await context.Output.WriteAsync("body"u8.ToArray());
        if (context.HeaderOutput is { } headers)
        {
            await headers.WriteAsync("head"u8.ToArray());
        }

        return first;
    };

    // Opens the given connections in turn and records each target.
    private sealed class SequenceConnector(params IConnection[] connections) : IConnector
    {
        public List<ConnectTarget> Targets { get; } = [];

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            Targets.Add(target);
            return ValueTask.FromResult(ConnectResult.Connected(connections[Targets.Count - 1]));
        }
    }

    private sealed class NoListener : IConnectionListener
    {
        public ValueTask<ListenResult> ListenAsync(ListenTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ListenResult.Failed(CurlExitCode.CouldntConnect, "no"));
    }
}
