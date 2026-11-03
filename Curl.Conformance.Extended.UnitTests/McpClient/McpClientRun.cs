using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.McpClient;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.McpClient;

/// <summary>Runs curl against a Streamable HTTP reference MCP server and holds what the McpClient cases share.</summary>
internal static class McpClientRun
{
    public const int Port = 3000;

    public const string Url = "mcp://localhost:3000/mcp";

    public static readonly KnownFailures KnownFailures = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    public static readonly McpReferenceTool Echo = new("echo", "Echoes its text", """{"content":[{"type":"text","text":"hello"}],"isError":false}""");

    public static Task<CurlRunResult> RunAsync(McpStreamableHttpReferenceServer server, params string[] arguments) =>
        ExtendedConformanceHarness.RunAsync(InProcessCurl.RunAsync, [ReferenceRoute.Tcp(Port, server)], ["-sS", "-m", "30", .. arguments]);

    public static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    /// <summary>The JSON-RPC methods of the POSTs curl sent, in order.</summary>
    public static string[] PostedMethods(McpStreamableHttpReferenceServer server) =>
        [.. server.Requests.Where(request => request.Method == "POST").Select(request => request.JsonRpcMethod ?? string.Empty)];
}
