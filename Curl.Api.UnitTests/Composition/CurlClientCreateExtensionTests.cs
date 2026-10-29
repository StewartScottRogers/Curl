using Curl.Api.Codes;
using Curl.Api.Errors;
using Curl.Protocol.Abstractions;

namespace Curl.Api.Composition;

// The real managed engine with a test extension (BL-4098): the extension's handler answers in process, so nothing is sent.
[TestClass]
public sealed class CurlClientCreateExtensionTests
{
    private const string ExtensionUrl = "example-extension://host/";

    [TestMethod]
    public async Task Create_WithExtensionsOn_SendsAnExtensionSchemeToItsHandler()
    {
        ReachedExtensionHandler handler = new();
        await using CurlClient client = CurlClient.Create(new CurlClientOptions(), _ => null, _ => [handler]);

        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, ExtensionUrl), CancellationToken.None);
        await response.Body.DisposeAsync();

        Assert.AreEqual(1, handler.Transfers);
    }

    [TestMethod]
    public async Task Create_WithCurlExtensionsZero_RefusesTheExtensionSchemeAsUnsupported()
    {
        ReachedExtensionHandler handler = new();
        await using CurlClient client = CurlClient.Create(
            new CurlClientOptions(),
            name => name == "CURL_EXTENSIONS" ? "0" : null,
            _ => [handler]);

        CurlException failure = await Assert.ThrowsExactlyAsync<CurlException>(
            () => client.SendAsync(new CurlRequest(HttpMethod.Get, ExtensionUrl), CancellationToken.None));

        Assert.AreEqual(CurlCode.UnsupportedProtocol, failure.Code);
        Assert.AreEqual(0, handler.Transfers);
    }

    private sealed class ReachedExtensionHandler : IProtocolHandler
    {
        public int Transfers { get; private set; }

        public IReadOnlyCollection<string> SupportedSchemes { get; } = ["example-extension"];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            Transfers++;
            return ValueTask.FromResult(TransferResult.Success(0));
        }
    }
}
