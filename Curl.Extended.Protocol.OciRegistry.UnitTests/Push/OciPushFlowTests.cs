using System.Net;
using System.Security.Cryptography;
using System.Text;
using Curl.Extended.Protocol.OciRegistry.Client;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.OciRegistry.Push;

/// <summary>
/// Drives <c>oci://</c> pushes (<c>-T</c>) through fake connections: OCI Distribution Specification 1.1, "Pushing blobs"
/// (the <c>HEAD</c> existence check and the monolithic <c>POST</c> then <c>PUT ?digest=</c> upload) and "Pushing
/// manifests", with the Docker/Distribution token authentication specification's Bearer flow for a <c>push,pull</c> scope.
/// </summary>
[TestClass]
public sealed class OciPushFlowTests
{
    private const string Blob = "blob";
    private const string Challenge = "401 Unauthorized\r\nWWW-Authenticate: Bearer realm=\"https://auth.example/token\",service=\"reg\",scope=\"repository:alpine:pull\"";
    private static readonly string BlobDigest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Blob)));

    [TestMethod]
    public async Task PushBlob_RegistryLacksIt_ChecksThenUploadsMonolithically()
    {
        var connector = new SequenceConnector(
            SequenceConnector.Respond("404 Not Found"),
            SequenceConnector.Respond("202 Accepted\r\nLocation: /v2/alpine/blobs/uploads/u1?state=s"),
            SequenceConnector.Respond("201 Created", "done"));

        var (result, output) = await PushAsync(connector, "oci://reg.example/alpine@" + BlobDigest, Blob);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("done", output);
        StringAssert.StartsWith(connector[0].Written, $"HEAD /v2/alpine/blobs/{BlobDigest} HTTP/1.1\r\n");
        StringAssert.StartsWith(connector[1].Written, "POST /v2/alpine/blobs/uploads/ HTTP/1.1\r\n");
        Assert.Contains("Content-Length: 0\r\n", connector[1].Written);
        StringAssert.StartsWith(connector[2].Written, $"PUT /v2/alpine/blobs/uploads/u1?state=s&digest={Uri.EscapeDataString(BlobDigest)} HTTP/1.1\r\n");
        Assert.Contains("Content-Type: " + OciPushFlow.BlobContentType + "\r\n", connector[2].Written);
        StringAssert.EndsWith(connector[2].Written, "\r\n\r\n" + Blob);
        Assert.DoesNotContain("Authorization", connector[2].Written);
    }

    [TestMethod]
    public async Task PushBlob_LocationWithoutQuery_AddsDigestAsTheOnlyParameter()
    {
        var connector = new SequenceConnector(
            SequenceConnector.Respond("404 Not Found"),
            SequenceConnector.Respond("202 Accepted\r\nLocation: https://reg.example/up/u2"),
            SequenceConnector.Respond("201 Created"));

        var (result, _) = await PushAsync(connector, "oci://reg.example/alpine@" + BlobDigest, Blob);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        StringAssert.StartsWith(connector[2].Written, $"PUT /up/u2?digest={Uri.EscapeDataString(BlobDigest)} HTTP/1.1\r\n");
    }

    [TestMethod]
    public async Task PushBlob_RegistryHasIt_StopsAfterTheHead()
    {
        var connector = new SequenceConnector(SequenceConnector.Respond("200 OK"));

        var (result, output) = await PushAsync(connector, "oci://reg.example/alpine@" + BlobDigest, Blob);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsEmpty(output);
        Assert.HasCount(1, connector.Targets);
    }

    [TestMethod]
    [DataRow("sha256:0000000000000000000000000000000000000000000000000000000000000000")]
    [DataRow("sha512:abc")]
    public async Task PushBlob_DigestDiffersFromTheUrls_FailsAsUploadFailedWithoutConnecting(string urlDigest)
    {
        var connector = new SequenceConnector();

        var (result, _) = await PushAsync(connector, "oci://reg.example/alpine@" + urlDigest, Blob);

        Assert.AreEqual(CurlExitCode.UploadFailed, result.ExitCode);
        Assert.AreEqual($"The upload's digest {BlobDigest} does not match the URL's {urlDigest}.", result.ErrorMessage);
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task PushBlob_Unauthorized_AsksForPushPullTokenAndSendsItOnEveryLaterRequest()
    {
        var connector = new SequenceConnector(
            SequenceConnector.Respond(Challenge),
            SequenceConnector.Respond("200 OK", """{"token":"t0k"}"""),
            SequenceConnector.Respond("404 Not Found"),
            SequenceConnector.Respond("202 Accepted\r\nLocation: /v2/alpine/blobs/uploads/u1"),
            SequenceConnector.Respond("201 Created"));

        var (result, _) = await PushAsync(connector, "oci://reg.example/alpine@" + BlobDigest, Blob, new NetworkCredential("u", "p"));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        StringAssert.StartsWith(connector[1].Written, "GET /token?service=reg&scope=repository%3Aalpine%3Apush%2Cpull HTTP/1.1\r\n");
        Assert.Contains("Authorization: Basic dTpw\r\n", connector[1].Written);
        Assert.DoesNotContain("Authorization", connector[0].Written);
        Assert.Contains("Authorization: Bearer t0k\r\n", connector[2].Written);
        Assert.Contains("Authorization: Bearer t0k\r\n", connector[3].Written);
        Assert.Contains("Authorization: Bearer t0k\r\n", connector[4].Written);
    }

    [TestMethod]
    public async Task PushBlob_UploadLocationOnAnotherHost_SendsNoToken()
    {
        var connector = new SequenceConnector(
            SequenceConnector.Respond("404 Not Found"),
            SequenceConnector.Respond(Challenge),
            SequenceConnector.Respond("200 OK", """{"token":"t0k"}"""),
            SequenceConnector.Respond("202 Accepted\r\nLocation: https://storage.example/u1"),
            SequenceConnector.Respond("201 Created"));

        var (result, _) = await PushAsync(connector, "oci://reg.example/alpine@" + BlobDigest, Blob);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.Contains("Authorization: Bearer t0k\r\n", connector[3].Written);
        Assert.AreEqual("storage.example", connector.Targets[4].Host);
        Assert.DoesNotContain("Authorization", connector[4].Written);
    }

    [TestMethod]
    public async Task PushManifest_StillUnauthorizedWithTheToken_FailsAsLoginDenied()
    {
        var connector = new SequenceConnector(
            SequenceConnector.Respond(Challenge),
            SequenceConnector.Respond("200 OK", """{"token":"t0k"}"""),
            SequenceConnector.Respond(Challenge, """{"errors":[{"code":"DENIED","message":"requested access to the resource is denied"}]}"""));

        var (result, _) = await PushAsync(connector, "oci://reg.example/alpine:1", "{}");

        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        Assert.AreEqual("The requested URL returned error: 401 (DENIED: requested access to the resource is denied)", result.ErrorMessage);
        Assert.HasCount(3, connector.Targets);
    }

    [TestMethod]
    public async Task PushBlob_HeadFails_FailsWithTheRegistryError()
    {
        var (result, _) = await PushAsync(new SequenceConnector(SequenceConnector.Respond("500 Internal Server Error")), "oci://reg.example/alpine@" + BlobDigest, Blob);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("The requested URL returned error: 500", result.ErrorMessage);
    }

    [TestMethod]
    public async Task PushBlob_UploadNotAccepted_FailsWithTheRegistryError()
    {
        var connector = new SequenceConnector(
            SequenceConnector.Respond("404 Not Found"),
            SequenceConnector.Respond("404 Not Found", """{"errors":[{"code":"NAME_UNKNOWN","message":"repository name not known to registry"}]}"""));

        var (result, _) = await PushAsync(connector, "oci://reg.example/alpine@" + BlobDigest, Blob);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("The requested URL returned error: 404 (NAME_UNKNOWN: repository name not known to registry)", result.ErrorMessage);
    }

    [TestMethod]
    public async Task PushBlob_SessionWithoutLocation_FailsAsWeirdServerReply()
    {
        var connector = new SequenceConnector(SequenceConnector.Respond("404 Not Found"), SequenceConnector.Respond("202 Accepted"));

        var (result, _) = await PushAsync(connector, "oci://reg.example/alpine@" + BlobDigest, Blob);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("The registry answered 202 without a usable Location.", result.ErrorMessage);
    }

    [TestMethod]
    public async Task PushBlob_PutRefused_FailsWithTheRegistryError()
    {
        var connector = new SequenceConnector(
            SequenceConnector.Respond("404 Not Found"),
            SequenceConnector.Respond("202 Accepted\r\nLocation: /u"),
            SequenceConnector.Respond("400 Bad Request", """{"errors":[{"code":"DIGEST_INVALID","message":"provided digest did not match uploaded content"}]}"""));

        var (result, _) = await PushAsync(connector, "oci://reg.example/alpine@" + BlobDigest, Blob);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("The requested URL returned error: 400 (DIGEST_INVALID: provided digest did not match uploaded content)", result.ErrorMessage);
    }

    [TestMethod]
    public async Task PushManifest_ByTag_PutsItWithItsMediaType()
    {
        const string Manifest = """{"schemaVersion":2,"mediaType":"application/vnd.oci.image.index.v1+json"}""";
        var connector = new SequenceConnector(SequenceConnector.Respond("201 Created"));

        var (result, _) = await PushAsync(connector, "oci://reg.example:5000/library/alpine:3.20", Manifest);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        StringAssert.StartsWith(connector[0].Written, "PUT /v2/library/alpine/manifests/3.20 HTTP/1.1\r\n");
        Assert.Contains("Content-Type: application/vnd.oci.image.index.v1+json\r\n", connector[0].Written);
        StringAssert.EndsWith(connector[0].Written, "\r\n\r\n" + Manifest);
        Assert.AreEqual(new ConnectTarget("reg.example", 5000, true), connector.Targets[0]);
    }

    private static async Task<(TransferResult Result, string Output)> PushAsync(
        SequenceConnector connector, string url, string upload, NetworkCredential? credentials = null)
    {
        using var output = new MemoryStream();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(upload));
        var result = await new OciRegistryProtocolHandler(connector).ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse(url), Output = output, Upload = input, Credentials = credentials });
        return (result, Encoding.UTF8.GetString(output.ToArray()));
    }
}
