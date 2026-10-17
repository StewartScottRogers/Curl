using System.Net;
using System.Text;
using Curl.Api.Messages;
using CurlHttpVersion = Curl.Api.Options.Http.HttpVersion;

namespace Curl.Api.HttpMessageHandler.Versions;

[TestClass]
public sealed class HttpVersionPolicyGuardTests
{
    private static async Task<HttpResponseMessage> SendAsync(ScriptedEasyTransfer easy, Version version, HttpVersionPolicy policy)
    {
        await using CurlClient client = new(new ScriptedCurlEngine(easy), new CurlClientOptions(), ownsEngine: true);
        using HttpClient http = new(new CurlHttpMessageHandler(client));
        return await http.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://example/") { Version = version, VersionPolicy = policy });
    }

    private static ScriptedEasyTransfer Answering(string statusLine)
    {
        ScriptedEasyTransfer easy = new() { BodyBytes = Encoding.UTF8.GetBytes("body") };
        easy.HeaderLines.AddRange([statusLine, "\r\n"]);
        return easy;
    }

    [TestMethod]
    [DataRow(1, 0, HttpVersionPolicy.RequestVersionOrLower, CurlHttpVersion.Http10, "HTTP/1.0 200 OK\r\n")]
    [DataRow(1, 0, HttpVersionPolicy.RequestVersionExact, CurlHttpVersion.Http10, "HTTP/1.1 200 OK\r\n")]
    [DataRow(1, 1, HttpVersionPolicy.RequestVersionOrLower, CurlHttpVersion.Http11, "HTTP/1.1 200 OK\r\n")]
    [DataRow(1, 1, HttpVersionPolicy.RequestVersionExact, CurlHttpVersion.Http11, "HTTP/1.1 200 OK\r\n")]
    [DataRow(1, 1, HttpVersionPolicy.RequestVersionOrHigher, CurlHttpVersion.None, "HTTP/2 200\r\n")]
    [DataRow(2, 0, HttpVersionPolicy.RequestVersionOrLower, CurlHttpVersion.Http2, "HTTP/1.1 200 OK\r\n")]
    [DataRow(2, 0, HttpVersionPolicy.RequestVersionExact, CurlHttpVersion.Http2PriorKnowledge, "HTTP/2 200\r\n")]
    [DataRow(2, 0, HttpVersionPolicy.RequestVersionOrHigher, CurlHttpVersion.Http2, "HTTP/3 200\r\n")]
    [DataRow(3, 0, HttpVersionPolicy.RequestVersionOrLower, CurlHttpVersion.Http3, "HTTP/2 200\r\n")]
    [DataRow(3, 0, HttpVersionPolicy.RequestVersionExact, CurlHttpVersion.Http3Only, "HTTP/3 200\r\n")]
    [DataRow(3, 0, HttpVersionPolicy.RequestVersionOrHigher, CurlHttpVersion.Http3, "HTTP/3 200\r\n")]
    [DataRow(4, 0, HttpVersionPolicy.RequestVersionOrLower, CurlHttpVersion.Http3, "HTTP/3 200\r\n")]
    public async Task Send_AsksTheEngineForTheVersion_AndAcceptsAResponseThePolicyAllows(int major, int minor, HttpVersionPolicy policy, CurlHttpVersion expected, string statusLine)
    {
        ScriptedEasyTransfer easy = Answering(statusLine);

        using HttpResponseMessage response = await SendAsync(easy, new Version(major, minor), policy);

        Assert.AreEqual(expected, easy.PerformedOptions!.Http.HttpVersion);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("body", await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    [DataRow(1, 1, HttpVersionPolicy.RequestVersionExact, "HTTP/2 200\r\n")]
    [DataRow(2, 0, HttpVersionPolicy.RequestVersionOrHigher, "HTTP/1.1 200 OK\r\n")]
    [DataRow(2, 0, HttpVersionPolicy.RequestVersionExact, "HTTP/1.1 200 OK\r\n")]
    [DataRow(3, 0, HttpVersionPolicy.RequestVersionOrHigher, "HTTP/2 200\r\n")]
    [DataRow(3, 0, HttpVersionPolicy.RequestVersionExact, "HTTP/1.1 200 OK\r\n")]
    public async Task Send_ResponseVersionThePolicyRefuses_ThrowsVersionNegotiationError(int major, int minor, HttpVersionPolicy policy, string statusLine)
    {
        HttpRequestException thrown = await Assert.ThrowsExactlyAsync<HttpRequestException>(() => SendAsync(Answering(statusLine), new Version(major, minor), policy));

        Assert.AreEqual(HttpRequestError.VersionNegotiationError, thrown.HttpRequestError);
        StringAssert.Contains(thrown.Message, $"Requesting HTTP version {major}.{minor} with version policy {policy}");
    }

    [TestMethod]
    [DataRow(4, 0, HttpVersionPolicy.RequestVersionExact)]
    [DataRow(4, 0, HttpVersionPolicy.RequestVersionOrHigher)]
    [DataRow(0, 9, HttpVersionPolicy.RequestVersionOrLower)]
    [DataRow(1, 2, HttpVersionPolicy.RequestVersionOrHigher)]
    public async Task Send_VersionTheEngineCannotSpeak_ThrowsBeforeTheTransfer(int major, int minor, HttpVersionPolicy policy)
    {
        ScriptedEasyTransfer easy = Answering("HTTP/1.1 200 OK\r\n");

        HttpRequestException thrown = await Assert.ThrowsExactlyAsync<HttpRequestException>(() => SendAsync(easy, new Version(major, minor), policy));

        Assert.AreEqual(HttpRequestError.VersionNegotiationError, thrown.HttpRequestError);
        StringAssert.Contains(thrown.Message, "is not supported");
        Assert.IsNull(easy.PerformedOptions);
    }

    [TestMethod]
    public async Task Send_ResponseWithNoHttpVersion_MeetsAnExactPolicy()
    {
        ScriptedEasyTransfer easy = new() { BodyBytes = Encoding.UTF8.GetBytes("raw") };

        using HttpResponseMessage response = await SendAsync(easy, HttpVersion.Version30, HttpVersionPolicy.RequestVersionExact);

        Assert.AreEqual("raw", await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    [DataRow(HttpVersionPolicy.RequestVersionExact, 2, true)]
    [DataRow(HttpVersionPolicy.RequestVersionExact, 1, false)]
    [DataRow(HttpVersionPolicy.RequestVersionOrHigher, 3, true)]
    [DataRow(HttpVersionPolicy.RequestVersionOrHigher, 1, false)]
    [DataRow(HttpVersionPolicy.RequestVersionOrLower, 1, true)]
    [DataRow(HttpVersionPolicy.RequestVersionOrLower, 3, true)]
    [DataRow(HttpVersionPolicy.RequestVersionExact, 0, true)]
    public void IsMet_ComparesMajorVersionsByPolicy(HttpVersionPolicy policy, int respondedMajor, bool expected)
    {
        Assert.AreEqual(expected, HttpVersionPolicyGuard.IsMet(HttpVersion.Version20, policy, new Version(respondedMajor, 0)));
    }

    [TestMethod]
    public void SupportedVersion_SpokenVersion_IsItself()
    {
        Assert.AreSame(HttpVersion.Version11, HttpVersionPolicyGuard.SupportedVersion(HttpVersion.Version11, HttpVersionPolicy.RequestVersionExact));
    }
}
