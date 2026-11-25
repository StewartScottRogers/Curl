using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Composition;

/// <summary>
/// Pins that the OAuth2 hook runs before <c>--dpop</c>'s, and that together they get a token bound to the DPoP key and
/// send it with a proof holding its <c>ath</c> (RFC 9449 §4.2, §5, §7.1).
/// </summary>
[TestClass]
public sealed class DpopWithOAuth2OrderTests
{
    private const string TokenUrl = "https://server.example.com/token";
    private const string ResourceUrl = "https://api.example.com/resource";
    private const string Token = "Kz~8mXK1EalYznwH-LC-1fBAo.4Ljp~zsPE_NeO.gxU";

    private readonly string _directory = Directory.CreateTempSubdirectory("curl-dpop-oauth2-").FullName;

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    public void Catalogue_DpopWithAnOAuth2Flow_OAuth2RunsFirstSoTheDpopHookSignsTheRequestCarryingTheToken()
    {
        var hooks = TransferHooks.ActivatedBy(Values("k.pem"), ExtensionTransferHookCatalogue.Entries).Hooks;

        var names = hooks.Select(hook => hook.GetType().Name).ToList();
        CollectionAssert.AreEqual(new[] { "OAuth2TransferHook", "DpopTransferHook" }, names, string.Join(",", names));
    }

    [TestMethod]
    public async Task Catalogue_DpopWithTheDeviceFlow_SendsAProofOnTheTokenRequestAndTheBoundTokenWithItsAth()
    {
        var keyFile = Path.Combine(_directory, "key.pem");
        using (var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256))
        {
            await File.WriteAllTextAsync(keyFile, ecdsa.ExportECPrivateKeyPem());
        }

        var server = new AuthorizationServer(
            Json("200 OK", """{"device_code":"dc","user_code":"WDJB-MJHT","verification_uri":"https://example.com/device","expires_in":600,"interval":1}"""),
            Json("400 Bad Request", """{"error":"use_dpop_nonce"}""", "DPoP-Nonce: token-nonce\r\n"),
            Json("200 OK", $$"""{"access_token":"{{Token}}","token_type":"DPoP","expires_in":600}"""));
        var resource = new ResourceServer();
        var hooks = TransferHooks.ActivatedBy(Values(keyFile), ExtensionTransferHookCatalogue.Entries, new TransferHookServices(server, new NoListener()));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(ResourceUrl + "?q=1"),
            Output = new MemoryStream(),
            Http = new Curl.Protocol.Abstractions.HttpRequestOptions(),
            TimeProvider = new ImmediateTimeProvider(),
        };

        var result = await hooks.Around(resource).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.HasCount(3, server.Requests);
        Assert.DoesNotContain("DPoP:", server.Requests[0].ToString());
        AssertTokenRequestProof(server.Requests[1].ToString(), null);
        AssertTokenRequestProof(server.Requests[2].ToString(), "token-nonce");
        Assert.HasCount(2, resource.Requests);
        AssertResourceRequest(resource.Requests[0], null);
        AssertResourceRequest(resource.Requests[1], "resource-nonce");
    }

    private static void AssertTokenRequestProof(string request, string? nonce)
    {
        StringAssert.StartsWith(request, "POST /token HTTP/1.1\r\n");
        var claims = ClaimsOf(request.Split("\r\n").Single(line => line.StartsWith("DPoP: ", StringComparison.Ordinal)));
        Assert.AreEqual("POST", claims.GetProperty("htm").GetString());
        Assert.AreEqual(TokenUrl, claims.GetProperty("htu").GetString());
        Assert.IsFalse(claims.TryGetProperty("ath", out _));
        Assert.AreEqual(nonce, claims.TryGetProperty("nonce", out var given) ? given.GetString() : null);
    }

    private static void AssertResourceRequest(IReadOnlyList<string> headers, string? nonce)
    {
        Assert.HasCount(2, headers);
        Assert.AreEqual("Authorization: DPoP " + Token, headers[0]);
        var claims = ClaimsOf(headers[1]);
        Assert.AreEqual("GET", claims.GetProperty("htm").GetString());
        Assert.AreEqual(ResourceUrl, claims.GetProperty("htu").GetString());
        Assert.AreEqual(Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(Token))), claims.GetProperty("ath").GetString());
        Assert.AreEqual(nonce, claims.TryGetProperty("nonce", out var given) ? given.GetString() : null);
    }

    private static JsonElement ClaimsOf(string proofHeader) =>
        JsonElement.Parse(Base64Url.DecodeFromChars(proofHeader["DPoP: ".Length..].Split('.')[1]));

    private static string Json(string status, string body, string headers = "") =>
        $"HTTP/1.1 {status}\r\nContent-Type: application/json\r\n{headers}Content-Length: {body.Length}\r\n\r\n{body}";

    private ExtensionOptionValues Values(string keyFile) => new(new Dictionary<string, string>
    {
        ["dpop"] = keyFile,
        ["oauth2-device"] = TokenUrl,
        ["oauth2-device-url"] = "https://server.example.com/device",
        ["oauth2-client-id"] = "cid",
        ["oauth2-token-cache"] = _directory,
    });

    /// <summary>Hands out one connection per response, and records what each is sent.</summary>
    private sealed class AuthorizationServer(params string[] responses) : IConnector
    {
        public List<RecordedRequest> Requests { get; } = [];

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            var connection = new RecordedRequest(responses[Requests.Count]);
            Requests.Add(connection);
            return ValueTask.FromResult(ConnectResult.Connected(connection));
        }
    }

    private sealed class RecordedRequest(string response) : IConnection
    {
        private readonly MemoryStream _response = new(Encoding.ASCII.GetBytes(response));
        private readonly MemoryStream _written = new();

        public bool IsSecure => true;

        public EndPoint? RemoteEndPoint => null;

        public override string ToString() => Encoding.ASCII.GetString(_written.ToArray());

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => _response.ReadAsync(buffer, cancellationToken);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => _written.WriteAsync(buffer, cancellationToken);

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>Stands in for curl's HTTP handler: refuses the first request with a nonce and accepts the next.</summary>
    private sealed class ResourceServer : IProtocolHandler
    {
        public List<IReadOnlyList<string>> Requests { get; } = [];

        public IReadOnlyCollection<string> SupportedSchemes => ["https"];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            Requests.Add(context.Http!.Headers);
            var report = Requests.Count == 1
                ? new TransferReport { ResponseCode = 401, ResponseHeaders = [new("DPoP-Nonce", "resource-nonce")] }
                : new TransferReport { ResponseCode = 200 };
            return ValueTask.FromResult(TransferResult.Success(0) with { Report = report });
        }
    }

    private sealed class NoListener : IConnectionListener
    {
        public ValueTask<ListenResult> ListenAsync(ListenTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("The listener was used.");
    }

    /// <summary>Fires every timer at once, so the device flow's polling interval passes without waiting.</summary>
    private sealed class ImmediateTimeProvider : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            System.CreateTimer(callback, state, TimeSpan.Zero, period);
    }
}
