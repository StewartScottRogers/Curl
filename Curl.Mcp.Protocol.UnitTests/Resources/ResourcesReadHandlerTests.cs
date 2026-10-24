using System.Text.Json;
using Curl.Mcp.Protocol.JsonRpc;
using Curl.Mcp.Protocol.Model;
using Curl.Mcp.Protocol.Server;

namespace Curl.Mcp.Protocol.Resources;

[TestClass]
public sealed class ResourcesReadHandlerTests
{
    // The error the SDK 2.2.0 server answered with in Curl.Mcp.IntegrationTests/Golden/resources-read-missing.json (BL-3374).
    private const string GoldenMissingError = """{"code":-32002,"message":"Resource not found: 'curl://no-such-resource'."}""";

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    private static ResourcesReadHandler HandlerOver(params IMcpResourceProvider[] providers)
    {
        McpServerRegistry registry = new();
        foreach (IMcpResourceProvider provider in providers)
        {
            registry.Add(provider);
        }

        return new ResourcesReadHandler(registry);
    }

    private static FakeResourceProvider Catalogue() =>
        new(new McpResource("ai-help", "curl://ai-help", "Index.", "text/markdown"), new McpResource("ai-help-all", "curl://ai-help/all", "All.", "text/markdown"));

    [TestMethod]
    public void Constructor_NullRegistry_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ResourcesReadHandler(null!));
    }

    [TestMethod]
    public void Method_Always_IsResourcesRead()
    {
        Assert.AreEqual("resources/read", HandlerOver().Method);
    }

    [TestMethod]
    public async Task HandleAsync_KnownUri_AnswersOneTextContent()
    {
        ResourcesReadHandler handler = HandlerOver(Catalogue());

        JsonElement result = await handler.HandleAsync(Json("""{"uri":"curl://ai-help/all"}"""), CancellationToken.None);

        Assert.AreEqual("""{"contents":[{"uri":"curl://ai-help/all","mimeType":"text/markdown","text":"text of curl://ai-help/all"}]}""", result.GetRawText());
    }

    [TestMethod]
    public async Task HandleAsync_SeveralProviders_ReadsFromTheProviderThatListedTheUri()
    {
        FakeResourceProvider first = Catalogue();
        FakeResourceProvider second = new(new McpResource("other", "test://other", null, null));
        ResourcesReadHandler handler = HandlerOver(first, second);

        JsonElement result = await handler.HandleAsync(Json("""{"uri":"test://other"}"""), CancellationToken.None);

        Assert.AreEqual("""{"contents":[{"uri":"test://other","text":"text of test://other"}]}""", result.GetRawText());
        Assert.IsEmpty(first.UrisRead);
        CollectionAssert.AreEqual(new[] { "test://other" }, second.UrisRead);
    }

    [TestMethod]
    public async Task HandleAsync_UnknownUri_ThrowsResourceNotFoundNamingTheUri()
    {
        ResourcesReadHandler handler = HandlerOver(Catalogue());

        McpProtocolException exception = await Assert.ThrowsExactlyAsync<McpProtocolException>(async () => await handler.HandleAsync(Json("""{"uri":"curl://nope"}"""), CancellationToken.None));

        Assert.AreEqual(JsonRpcErrorCodes.ResourceNotFound, exception.Code);
        Assert.AreEqual("Resource not found: 'curl://nope'.", exception.Message);
    }

    [TestMethod]
    public async Task HandleAsync_GoldenMissingUri_AnswersTheGoldenError()
    {
        ResourcesReadHandler handler = HandlerOver(Catalogue());

        McpProtocolException exception = await Assert.ThrowsExactlyAsync<McpProtocolException>(async () => await handler.HandleAsync(Json("""{"uri":"curl://no-such-resource"}"""), CancellationToken.None));

        JsonElement golden = Json(GoldenMissingError);
        Assert.AreEqual(golden.GetProperty("code").GetInt32(), exception.Code);
        Assert.AreEqual(golden.GetProperty("message").GetString(), exception.Message);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "No params")]
    [DataRow("{}", DisplayName = "No uri")]
    [DataRow("""{"uri":7}""", DisplayName = "Uri not a string")]
    [DataRow("[]", DisplayName = "Params not an object")]
    public async Task HandleAsync_ParamsNamingNoUri_ThrowsResourceNotFoundWithAnEmptyUri(string? parameters)
    {
        ResourcesReadHandler handler = HandlerOver(Catalogue());
        JsonElement? json = parameters is null ? null : Json(parameters);

        McpProtocolException exception = await Assert.ThrowsExactlyAsync<McpProtocolException>(async () => await handler.HandleAsync(json, CancellationToken.None));

        Assert.AreEqual(JsonRpcErrorCodes.ResourceNotFound, exception.Code);
        Assert.AreEqual("Resource not found: ''.", exception.Message);
    }

    [TestMethod]
    public async Task HandleAsync_ProviderFindsNothingForAListedUri_ThrowsResourceNotFound()
    {
        ResourcesReadHandler handler = HandlerOver(new VanishingResourceProvider("test://gone"));

        McpProtocolException exception = await Assert.ThrowsExactlyAsync<McpProtocolException>(async () => await handler.HandleAsync(Json("""{"uri":"test://gone"}"""), CancellationToken.None));

        Assert.AreEqual(JsonRpcErrorCodes.ResourceNotFound, exception.Code);
        Assert.AreEqual("Resource not found: 'test://gone'.", exception.Message);
    }
}
