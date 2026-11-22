using Curl.Api.Messages;
using Curl.Api.Options;
using Curl.Api.RequestBodies;
using Curl.Extension.Composition;

namespace Curl.Api.Extensions;

/// <summary>Pins ADR-1336: every scheme an extension serves gets a <c>Curl.Api</c> body as the console's <c>-d</c> data (or <c>-T</c> upload where the scheme takes one).</summary>
[TestClass]
public sealed class ExtensionBodyParityTests
{
    private static readonly byte[] Body = "hello world"u8.ToArray();

    private static IReadOnlyList<string> Schemes { get; } = ExtensionListing.Schemes(_ => null);

    private static async Task<UploadingEasyTransfer> SendAsync(HttpMethod method, string scheme)
    {
        UploadingEasyTransfer easy = new();
        await using CurlClient client = new(new UploadingCurlEngine(easy));
        _ = await client.SendAsync(new CurlRequest(method, $"{scheme}://h/x") { Body = CurlContent.FromBytes(Body) }, CancellationToken.None);
        return easy;
    }

    [TestMethod]
    [DataRow("mcp://h/x", true)]
    [DataRow("MCP+STDIO://h/x", true)]
    [DataRow("http://h/x", false)]
    [DataRow("no-scheme", false)]
    public void ServedByExtension_Url_ReportsTheScheme(string url, bool expected) =>
        Assert.AreEqual(expected, UploadSchemes.ServedByExtension(url));
    [TestMethod]
    public void Catalogue_ServesSchemes() => Assert.IsTrue(Schemes.Count > 20);

    [TestMethod]
    public async Task SendAsync_PostedBytesToEveryExtensionScheme_ReachTheHandlerAsDashDDataOrUploadDoes()
    {
        foreach (string scheme in Schemes)
        {
            UploadingEasyTransfer easy = await SendAsync(HttpMethod.Post, scheme);
            AssertBodyReachesHandler(scheme, easy, "POST", null);
        }
    }

    [TestMethod]
    public async Task SendAsync_PutBytesToEveryExtensionScheme_ReachTheHandlerAsDashDDataOrUploadDoes()
    {
        foreach (string scheme in Schemes)
        {
            UploadingEasyTransfer easy = await SendAsync(HttpMethod.Put, scheme);
            AssertBodyReachesHandler(scheme, easy, "PUT", "PUT");
        }
    }

    private static void AssertBodyReachesHandler(string scheme, UploadingEasyTransfer easy, string method, string? customRequest)
    {
        EasyOptions options = easy.PerformedOptions!;
        if (UploadSchemes.UploadsWithBody($"{scheme}://h/x"))
        {
            Assert.IsTrue(options.Transfer.Upload, scheme);
            CollectionAssert.AreEqual(Body, easy.Uploads.Single(), scheme);
            return;
        }

        Assert.IsFalse(options.Transfer.Upload, $"{scheme} {method}");
        Assert.IsTrue(options.Http.Post, $"{scheme} {method}");
        CollectionAssert.AreEqual(Body, options.Http.PostFields!.Value.ToArray(), $"{scheme} {method}");
        Assert.AreEqual(customRequest, options.Transfer.CustomRequest, $"{scheme} {method}");
    }
}
