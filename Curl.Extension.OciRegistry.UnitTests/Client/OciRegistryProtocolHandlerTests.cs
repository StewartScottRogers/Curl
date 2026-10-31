using System.Net;
using System.Text;
using Curl.Extension.OciRegistry.Http;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OciRegistry.Client;

/// <summary>
/// Drives <c>oci://</c> pulls through fake connections: OCI Distribution Specification 1.1, "Pulling manifests" and
/// "Pulling blobs", with the Docker/Distribution token authentication specification's Bearer flow.
/// </summary>
[TestClass]
public sealed class OciRegistryProtocolHandlerTests
{
    private const string Manifest = """{"schemaVersion":2}""";
    private const string Challenge = "401 Unauthorized\r\nWWW-Authenticate: Bearer realm=\"https://auth.example/token\",service=\"reg\",scope=\"repository:alpine:pull\"";

    [TestMethod]
    public async Task ExecuteAsync_ManifestByTag_SendsGetWithManifestMediaTypesAndWritesBody()
    {
        var connector = new SequenceConnector(SequenceConnector.Respond("200 OK", Manifest));

        var (result, output) = await PullAsync(connector, "oci://reg.example/library/alpine:3.20");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(Manifest, output);
        Assert.AreEqual(Manifest.Length, result.BytesTransferred);
        StringAssert.StartsWith(connector[0].Written, "GET /v2/library/alpine/manifests/3.20 HTTP/1.1\r\n");
        Assert.Contains("Accept: " + OciRegistryProtocolHandler.ManifestMediaTypes + "\r\n", connector[0].Written);
        Assert.AreEqual(new ConnectTarget("reg.example", 443, true), connector.Targets[0]);
    }

    [TestMethod]
    public async Task ExecuteAsync_BlobByDigestOnPort_SendsBlobGetToThatPort()
    {
        var connector = new SequenceConnector(SequenceConnector.Respond("200 OK", "blob"));

        var (result, output) = await PullAsync(connector, "oci://reg.example:5000/alpine@sha256:abc");

        Assert.AreEqual("blob", output);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        StringAssert.StartsWith(connector[0].Written, "GET /v2/alpine/blobs/sha256:abc HTTP/1.1\r\n");
        Assert.Contains("Accept: */*\r\n", connector[0].Written);
        Assert.AreEqual(5000, connector.Targets[0].Port);
    }

    [TestMethod]
    public async Task ExecuteAsync_PathWithoutName_FailsAsMalformedUrl()
    {
        var (result, _) = await PullAsync(new SequenceConnector(), "oci://reg.example/");

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual(OciRegistryProtocolHandler.NoReferenceMessage, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutContext_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await new OciRegistryProtocolHandler(new SequenceConnector()).ExecuteAsync(null!));

    [TestMethod]
    public async Task ExecuteAsync_Unauthorized_FetchesBearerTokenWithCredentialsAndRetries()
    {
        var connector = new SequenceConnector(
            SequenceConnector.Respond(Challenge),
            SequenceConnector.Respond("200 OK", """{"token":"t0k"}"""),
            SequenceConnector.Respond("200 OK", Manifest));

        var (result, output) = await PullAsync(connector, "oci://reg.example/alpine", new NetworkCredential("u", "p"));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(Manifest, output);
        StringAssert.StartsWith(connector[1].Written, "GET /token?service=reg&scope=repository%3Aalpine%3Apull HTTP/1.1\r\n");
        Assert.Contains("Authorization: Basic dTpw\r\n", connector[1].Written);
        Assert.AreEqual("auth.example", connector.Targets[1].Host);
        Assert.Contains("Authorization: Bearer t0k\r\n", connector[2].Written);
        Assert.DoesNotContain("Authorization", connector[0].Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_TokenEndpointRefuses_FailsAsLoginDenied()
    {
        var connector = new SequenceConnector(SequenceConnector.Respond(Challenge), SequenceConnector.Respond("403 Forbidden"));

        var (result, _) = await PullAsync(connector, "oci://reg.example/alpine");

        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        Assert.AreEqual("The token endpoint answered 403 without a token.", result.ErrorMessage);
        Assert.DoesNotContain("Authorization", connector[1].Written);
    }

    [TestMethod]
    [DataRow("401 Unauthorized")]
    [DataRow("401 Unauthorized\r\nWWW-Authenticate: Bearer realm=\"not a url\"")]
    public async Task ExecuteAsync_UnauthorizedWithoutUsableChallenge_FailsAsLoginDenied(string head)
    {
        var connector = new SequenceConnector(SequenceConnector.Respond(head, """{"errors":[{"code":"UNAUTHORIZED","message":"authentication required"}]}"""));

        var (result, _) = await PullAsync(connector, "oci://reg.example/alpine");

        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        Assert.AreEqual("The requested URL returned error: 401 (UNAUTHORIZED: authentication required)", result.ErrorMessage);
        Assert.HasCount(1, connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_ManifestUnknown_FailsWithTheRegistryError()
    {
        var connector = new SequenceConnector(SequenceConnector.Respond("404 Not Found", """{"errors":[{"code":"MANIFEST_UNKNOWN","message":"manifest unknown"}]}"""));

        var (result, output) = await PullAsync(connector, "oci://reg.example/alpine:nope");

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("The requested URL returned error: 404 (MANIFEST_UNKNOWN: manifest unknown)", result.ErrorMessage);
        Assert.IsEmpty(output);
    }

    [TestMethod]
    public async Task ExecuteAsync_BlobRedirect_FollowsLocationWithoutTheToken()
    {
        var connector = new SequenceConnector(
            SequenceConnector.Respond(Challenge),
            SequenceConnector.Respond("200 OK", """{"token":"t0k"}"""),
            SequenceConnector.Respond("307 Temporary Redirect\r\nLocation: https://storage.example/b?sig=1"),
            SequenceConnector.Respond("200 OK", "blob"));

        var (result, output) = await PullAsync(connector, "oci://reg.example/alpine@sha256:abc");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("blob", output);
        Assert.AreEqual("storage.example", connector.Targets[3].Host);
        StringAssert.StartsWith(connector[3].Written, "GET /b?sig=1 HTTP/1.1\r\n");
        Assert.DoesNotContain("Authorization", connector[3].Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_RedirectWithRelativeLocation_ResolvesItAgainstTheRequest()
    {
        var connector = new SequenceConnector(SequenceConnector.Respond("302 Found\r\nLocation: /other"), SequenceConnector.Respond("200 OK", "x"));

        var (result, _) = await PullAsync(connector, "oci://reg.example/alpine@sha256:abc");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        StringAssert.StartsWith(connector[1].Written, "GET /other HTTP/1.1\r\n");
        Assert.AreEqual("reg.example", connector.Targets[1].Host);
    }

    [TestMethod]
    public async Task ExecuteAsync_RedirectWithoutLocation_FailsAsWeirdServerReply()
    {
        var (result, _) = await PullAsync(new SequenceConnector(SequenceConnector.Respond("301 Moved")), "oci://reg.example/alpine");

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("The registry answered 301 without a usable Location.", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_EndlessRedirects_StopsAtCurlsMaximum()
    {
        var connections = Enumerable.Range(0, OciPullFlow.MaxRedirects + 1)
            .Select(_ => SequenceConnector.Respond("308 Permanent Redirect\r\nLocation: /again"))
            .ToArray();

        var (result, _) = await PullAsync(new SequenceConnector(connections), "oci://reg.example/alpine");

        Assert.AreEqual(CurlExitCode.TooManyRedirects, result.ExitCode);
        Assert.AreEqual("Maximum (50) redirects followed", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectionFails_FailsWithTheExchangeError()
    {
        var (result, _) = await PullAsync(new SequenceConnector(new RecordedConnection(null)), "oci://reg.example/alpine");

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
    }

    private static async Task<(TransferResult Result, string Output)> PullAsync(SequenceConnector connector, string url, NetworkCredential? credentials = null)
    {
        using var output = new MemoryStream();
        var result = await new OciRegistryProtocolHandler(connector).ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse(url), Output = output, Credentials = credentials });
        return (result, Encoding.UTF8.GetString(output.ToArray()));
    }
}
