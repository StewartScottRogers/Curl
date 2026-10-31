using System.Text;
using Curl.Extension.PrivacyPass.Http;
using Curl.Extension.PrivacyPass.Tokens;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.PrivacyPass.Options;

/// <summary>Pins reading an issuer directory (RFC 9578 §4): its request URL, its Blind RSA token keys, and every way it can fail.</summary>
[TestClass]
public sealed class PrivacyPassIssuerDirectoryTests
{
    private static readonly Uri DirectoryUrl = new("https://issuer.example/.well-known/private-token-issuer-directory");

    [TestMethod]
    public void Parse_RelativeRequestUriAndMixedKeys_ResolvesTheUrlAndKeepsOnlyBlindRsaKeys()
    {
        var json = """
            {"issuer-request-uri": "/token-request", "token-keys": [
              {"token-type": 1, "token-key": "AQID"},
              {"token-type": 2, "token-key": "BAUG", "not-before": 1686913811},
              {"token-type": 2, "token-key": "BwgJ=="},
              {"token-type": 2, "token-key": "not base64url!"}]}
            """;

        var directory = PrivacyPassIssuerDirectory.Parse(Encoding.UTF8.GetBytes(json), DirectoryUrl)!;

        Assert.AreEqual(new Uri("https://issuer.example/token-request"), directory.RequestUrl);
        Assert.HasCount(2, directory.TokenKeys);
        Assert.IsTrue(directory.Lists([4, 5, 6]));
        Assert.IsTrue(directory.Lists([7, 8, 9]));
        Assert.IsFalse(directory.Lists([1, 2, 3]));
    }

    [TestMethod]
    public void Parse_AbsoluteRequestUriAndNullKey_TakesTheUrlAndAnEmptyKey()
    {
        var json = """{"issuer-request-uri": "https://other.example/request", "token-keys": [{"token-type": 2, "token-key": null}]}""";

        var directory = PrivacyPassIssuerDirectory.Parse(Encoding.UTF8.GetBytes(json), DirectoryUrl)!;

        Assert.AreEqual(new Uri("https://other.example/request"), directory.RequestUrl);
        Assert.IsTrue(directory.Lists([]));
    }

    [TestMethod]
    [DataRow("not json", DisplayName = "not JSON")]
    [DataRow("[]", DisplayName = "not an object")]
    [DataRow("""{"token-keys": []}""", DisplayName = "no issuer-request-uri")]
    [DataRow("""{"issuer-request-uri": "/r"}""", DisplayName = "no token-keys")]
    [DataRow("""{"issuer-request-uri": null, "token-keys": []}""", DisplayName = "null issuer-request-uri")]
    [DataRow("""{"issuer-request-uri": "http://[", "token-keys": []}""", DisplayName = "issuer-request-uri not a URL")]
    [DataRow("""{"issuer-request-uri": 5, "token-keys": []}""", DisplayName = "issuer-request-uri a number")]
    [DataRow("""{"issuer-request-uri": "/r", "token-keys": [{"token-type": 2.5}]}""", DisplayName = "token-type not an integer")]
    public void Parse_NotADirectoryObject_GivesNull(string json)
    {
        var directory = PrivacyPassIssuerDirectory.Parse(Encoding.UTF8.GetBytes(json), DirectoryUrl);

        Assert.IsNull(directory);
    }

    [TestMethod]
    [DataRow("https://issuer.example", true)]
    [DataRow("https://issuer.example/", true)]
    [DataRow("https://issuer.example/?x=1", false)]
    [DataRow("https://issuer.example/token-request", false)]
    public void IsIssuerOrigin_Url_IsTrueOnlyWithoutPathOrQuery(string url, bool expected)
    {
        var isOrigin = PrivacyPassIssuerDirectory.IsIssuerOrigin(new Uri(url));

        Assert.AreEqual(expected, isOrigin);
    }

    [TestMethod]
    public async Task FetchAsync_DirectoryServed_GetsTheWellKnownPathAndReadsIt()
    {
        var body = """{"issuer-request-uri": "/token-request", "token-keys": []}""";
        var connection = new RecordedConnection(Answer(200, body));

        var directory = await PrivacyPassIssuerDirectory.FetchAsync(Client(connection), new Uri("https://issuer.example"), CancellationToken.None);

        Assert.AreEqual(new Uri("https://issuer.example/token-request"), directory.RequestUrl);
        StringAssert.StartsWith(connection.Written, "GET /.well-known/private-token-issuer-directory HTTP/1.1\r\nHost: issuer.example\r\n");
        StringAssert.Contains(connection.Written, "Accept: application/private-token-issuer-directory\r\n");
    }

    [TestMethod]
    [DataRow(404, "{}", CurlExitCode.LoginDenied, DisplayName = "not found")]
    [DataRow(200, "{}", CurlExitCode.WeirdServerReply, DisplayName = "not a directory")]
    public async Task FetchAsync_NoDirectory_ThrowsWithTheExitCode(int status, string body, CurlExitCode expected)
    {
        var client = Client(new RecordedConnection(Answer(status, body)));

        var failure = await Assert.ThrowsExactlyAsync<PrivacyPassException>(
            () => PrivacyPassIssuerDirectory.FetchAsync(client, new Uri("https://issuer.example"), CancellationToken.None));

        Assert.AreEqual(expected, failure.ExitCode);
    }

    [TestMethod]
    public async Task FetchAsync_IssuerUnreachable_ThrowsWithTheConnectExitCode()
    {
        var client = new PrivacyPassHttpClient(new RecordedConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "refused")));

        var failure = await Assert.ThrowsExactlyAsync<PrivacyPassException>(
            () => PrivacyPassIssuerDirectory.FetchAsync(client, new Uri("https://issuer.example"), CancellationToken.None));

        Assert.AreEqual(CurlExitCode.CouldntConnect, failure.ExitCode);
    }

    internal static string Answer(int status, string body) =>
        $"HTTP/1.1 {status} X\r\nContent-Type: application/private-token-issuer-directory\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\n\r\n{body}";

    private static PrivacyPassHttpClient Client(RecordedConnection connection) => new(new RecordedConnector(connection));
}
