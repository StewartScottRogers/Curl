using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.OAuth2.Cache;
using Curl.Extension.OAuth2.Flows;
using Curl.Extension.OAuth2.Http;
using Curl.Extension.OAuth2.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OAuth2.Options;

/// <summary>
/// Pins that with a DPoP key the hook gets a token bound to it, proving the token and refresh requests (RFC 9449 §5, §8),
/// and keeps it under that key.
/// </summary>
[TestClass]
public sealed class OAuth2TransferHookDpopTests
{
    private const string TokenUrl = "https://server.example.com/token";

    [TestMethod]
    public async Task ExecuteAsync_TokenCached_IsReusedOnlyWithTheKeyItWasBoundTo()
    {
        using var directory = new CacheTestDirectory();
        var connector = new QueuedConnector(
            new RecordedConnection(Issued("for-a", "DPoP")),
            new RecordedConnection(Issued("for-b", "dpop")),
            new RecordedConnection(FlowResponses.Token("unbound")));
        List<string> headers = [];

        foreach (var key in new FakeKey?[] { new("A"), new("A"), new("B"), null, new("B"), new("A") })
        {
            await Run(directory.Path, connector, key, headers);
        }

        CollectionAssert.AreEqual(
            new[]
            {
                "Authorization: DPoP for-a", "Authorization: DPoP for-a", "Authorization: DPoP for-b",
                "Authorization: Bearer unbound", "Authorization: DPoP for-b", "Authorization: DPoP for-a",
            },
            headers);
        Assert.AreEqual(3, connector.ConnectCount);
        StringAssert.Contains(connector.Connections[0].Written, "\r\nDPoP: A:server.example.com:-\r\n");
        Assert.DoesNotContain("DPoP:", connector.Connections[2].Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_EndpointAsksForANonce_SendsTheRequestAgainWithIt()
    {
        var connector = new QueuedConnector(
            new RecordedConnection("HTTP/1.1 400 Bad Request\r\nDPoP-Nonce: n1\r\nContent-Length: 26\r\n\r\n{\"error\":\"use_dpop_nonce\"}"),
            new RecordedConnection(Issued("bound", "DPoP")));

        var result = await Run(null, connector, new FakeKey("A"), []);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        StringAssert.Contains(connector.Connections[1].Written, "\r\nDPoP: A:server.example.com:n1\r\n");
    }

    [TestMethod]
    [DataRow("HTTP/1.1 400 Bad Request\r\nContent-Length: 26\r\n\r\n{\"error\":\"invalid_client\"}", CurlExitCode.LoginDenied)]
    [DataRow("HTTP/1.1 400 Bad Request\r\nContent-Length: 2\r\n\r\n{}", CurlExitCode.WeirdServerReply)]
    [DataRow(null, CurlExitCode.RecvError)]
    public async Task ExecuteAsync_EndpointFailsOtherwise_AsksOnce(string? response, CurlExitCode expected)
    {
        var connector = new QueuedConnector(new RecordedConnection(response));

        var result = await Run(null, connector, new FakeKey("A"), []);

        Assert.AreEqual(expected, result.ExitCode);
        Assert.AreEqual(1, connector.ConnectCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_CachedTokenExpired_ProvesTheRefreshRequest()
    {
        using var directory = new CacheTestDirectory();
        var connector = new QueuedConnector(
            new RecordedConnection(FlowResponses.Json("""{"access_token":"old","token_type":"DPoP","expires_in":0,"refresh_token":"r"}""")),
            new RecordedConnection(Issued("new", "DPoP")));
        List<string> headers = [];

        await Run(directory.Path, connector, new FakeKey("A"), headers);
        await Run(directory.Path, connector, new FakeKey("A"), headers);

        Assert.AreEqual("Authorization: DPoP new", headers[1]);
        StringAssert.Contains(connector.Connections[1].Written, "\r\nDPoP: A:server.example.com:-\r\n");
        StringAssert.Contains(connector.Connections[1].Written, "grant_type=refresh_token");
    }

    [TestMethod]
    public async Task ExecuteAsync_TokenNotBoundToTheKey_FailsWithWeirdServerReply()
    {
        var connector = new QueuedConnector(new RecordedConnection(FlowResponses.Token()));
        List<string> headers = [];

        var result = await Run(null, connector, new FakeKey("A"), headers);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("The token endpoint gave a token of type \"Bearer\", not a DPoP token bound to the --dpop key.", result.ErrorMessage);
        Assert.IsEmpty(headers);
    }

    [TestMethod]
    public async Task ExecuteAsync_KeyCannotBeRead_AsksNoEndpointAndLeavesTheKeysHookToFail()
    {
        var connector = new QueuedConnector();
        List<IReadOnlyList<string>> sent = [];
        var services = new TransferHookServices(connector, new RecordedListener(null!)) { DpopKeySource = new FakeSource(null) };

        await Hook(null).ExecuteAsync(Context(), services, context =>
        {
            sent.Add(context.Http!.Headers);
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        Assert.IsEmpty(sent.Single());
        Assert.AreEqual(0, connector.ConnectCount);
    }

    [TestMethod]
    public async Task PostFormAsync_ToAnEndpointOtherThanTheTokenEndpoint_SendsNoProof()
    {
        var connection = new RecordedConnection(FlowResponses.Json("{}"));
        OAuth2HttpClient client = new(new QueuedConnector(connection))
        {
            DpopProver = new OAuth2DpopTokenProver(new FakeKey("A"), new Uri(TokenUrl), TimeProvider.System),
        };

        await client.PostFormAsync(new Uri("https://server.example.com/device"), new OAuth2FormRequest("a=1", null), CancellationToken.None);

        Assert.DoesNotContain("DPoP:", connection.Written);
    }

    private static string Issued(string accessToken, string type) =>
        FlowResponses.Json($$"""{"access_token":"{{accessToken}}","token_type":"{{type}}","expires_in":3600}""");

    private static ValueTask<TransferResult> Run(string? cacheDirectory, QueuedConnector connector, FakeKey? key, List<string> headers) =>
        Hook(cacheDirectory).ExecuteAsync(
            Context(),
            new TransferHookServices(connector, new RecordedListener(null!)) { DpopKeySource = key is null ? null : new FakeSource(key) },
            context =>
            {
                headers.AddRange(context.Http!.Headers);
                return ValueTask.FromResult(TransferResult.Success(0));
            });

    private static TransferContext Context() => new()
    {
        Url = CurlUrl.Parse("https://api.example.com/resource"),
        Output = new MemoryStream(),
        Http = new Curl.Protocol.Abstractions.HttpRequestOptions(),
    };

    private static OAuth2TransferHook Hook(string? cacheDirectory) => new(
        new OAuth2Configuration(
            OAuth2FlowKind.ClientCredentials,
            new OAuth2FlowRequest(new Uri(TokenUrl), new OAuth2Client("s6BhdRkqt3", null, OAuth2ClientAuthentication.Basic), null),
            null,
            0,
            cacheDirectory),
        null,
        TextWriter.Null,
        count => new byte[count]);

    private sealed class FakeSource(IDpopKey? key) : IDpopKeySource
    {
        public ValueTask<IDpopKey?> ReadDpopKeyAsync(CancellationToken cancellationToken) => ValueTask.FromResult(key);
    }

    /// <summary>A key whose proof names itself, the endpoint's host and the nonce.</summary>
    private sealed class FakeKey(string name) : IDpopKey
    {
        public string Thumbprint => name;

        public string ProofForPost(Uri target, DateTimeOffset issuedAt, string? nonce) => $"{name}:{target.Host}:{nonce ?? "-"}";
    }
}
