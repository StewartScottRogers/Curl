using Curl.Cli;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Curl.Mcp;

[TestClass]
[DoNotParallelize]
public sealed class CurlAiHelpResourceHandlersTests
{
    [TestMethod]
    public async Task Server_ListsEveryAiHelpResourceThroughTheServiceGraph()
    {
        using IHost host = CurlMcpHost.Build([], _ => null);
        McpServerOptions options = host.Services.GetRequiredService<IOptions<McpServerOptions>>().Value;
        await using McpServer server = CreateServer(host, options);

        ListResourcesResult result = await options.Handlers.ListResourcesHandler!(
            new RequestContext<ListResourcesRequestParams>(server, new JsonRpcRequest { Method = RequestMethods.ResourcesList }, new ListResourcesRequestParams()),
            CancellationToken.None);

        CollectionAssert.AreEqual(
            McpAiHelpResourceCatalogue.Resources.Select(resource => resource.Uri).ToArray(),
            result.Resources.Select(resource => resource.Uri).ToArray());
        Assert.IsTrue(result.Resources.All(resource => resource.MimeType == "text/markdown"));
        Resource http = result.Resources.Single(resource => resource.Uri == "curl://ai-help/http");
        Assert.AreEqual("ai-help-http", http.Name);
        Assert.AreEqual(McpAiHelpResourceCatalogue.Find("curl://ai-help/http")!.Description, http.Description);
    }

    [TestMethod]
    public async Task Server_ReadsACategoryResourceThroughTheServiceGraph()
    {
        using IHost host = CurlMcpHost.Build([], _ => null);
        McpServerOptions options = host.Services.GetRequiredService<IOptions<McpServerOptions>>().Value;
        await using McpServer server = CreateServer(host, options);

        ReadResourceResult result = await options.Handlers.ReadResourceHandler!(ReadRequest(server, "curl://ai-help/http"), CancellationToken.None);

        TextResourceContents contents = (TextResourceContents)result.Contents.Single();
        _ = CurlAiHelpText.TryGetMarkdown("http", out string expected);
        Assert.AreEqual(expected, contents.Text);
        Assert.AreEqual("curl://ai-help/http", contents.Uri);
        Assert.AreEqual("text/markdown", contents.MimeType);
    }

    [TestMethod]
    public async Task Server_ReadingAnUnknownUri_IsAResourceNotFoundError()
    {
        using IHost host = CurlMcpHost.Build([], _ => null);
        McpServerOptions options = host.Services.GetRequiredService<IOptions<McpServerOptions>>().Value;
        await using McpServer server = CreateServer(host, options);

        McpProtocolException error = await Assert.ThrowsExactlyAsync<McpProtocolException>(
            async () => await options.Handlers.ReadResourceHandler!(ReadRequest(server, "curl://ai-help/nope"), CancellationToken.None));

        Assert.AreEqual(McpErrorCode.ResourceNotFound, error.ErrorCode);
        StringAssert.Contains(error.Message, "curl://ai-help/nope");
    }

    [TestMethod]
    public async Task ReadAsync_WithNoParameters_IsAResourceNotFoundError()
    {
        using IHost host = CurlMcpHost.Build([], _ => null);
        McpServerOptions options = host.Services.GetRequiredService<IOptions<McpServerOptions>>().Value;
        await using McpServer server = CreateServer(host, options);
        RequestContext<ReadResourceRequestParams> request = new(server, new JsonRpcRequest { Method = RequestMethods.ResourcesRead }, null!);

        McpProtocolException error = await Assert.ThrowsExactlyAsync<McpProtocolException>(
            async () => await CurlAiHelpResourceHandlers.ReadAsync(request, CancellationToken.None));

        Assert.AreEqual(McpErrorCode.ResourceNotFound, error.ErrorCode);
    }

    private static McpServer CreateServer(IHost host, McpServerOptions options) =>
        McpServer.Create(new StreamServerTransport(Stream.Null, Stream.Null), options, null, host.Services);

    private static RequestContext<ReadResourceRequestParams> ReadRequest(McpServer server, string uri) =>
        new(server, new JsonRpcRequest { Method = RequestMethods.ResourcesRead }, new ReadResourceRequestParams { Uri = uri });
}
