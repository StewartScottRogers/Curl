using System.Text;
using System.Text.Json;
using Curl.Extension.Protocol.McpClient.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.McpClient.Client;

/// <summary>
/// The client side of one MCP exchange (Model Context Protocol 2025-06-18): <c>initialize</c> and
/// <c>notifications/initialized</c> ("Lifecycle &gt; Initialization"), then every page of <c>tools/list</c> or one
/// <c>tools/call</c> ("Server Features &gt; Tools"), writing what the server returns to the output.
/// </summary>
/// <param name="session">The session to the server.</param>
/// <param name="output">Where tool lines and content items go.</param>
/// <param name="cancellationToken">Cancels the exchange.</param>
internal sealed class McpClientFlow(IMcpSession session, Stream output, CancellationToken cancellationToken)
{
    /// <summary>The <c>clientInfo.name</c> curl gives in <c>initialize</c>.</summary>
    internal const string ClientName = "curl";

    /// <summary>The <c>clientInfo.version</c> curl gives in <c>initialize</c>.</summary>
    internal const string ClientVersion = "1.0.0";

    private readonly McpRequestWriter writer = new();

    /// <summary>Gets how many bytes have been written to the output.</summary>
    public long BytesWritten { get; private set; }

    /// <summary>Sends <c>initialize</c>, takes the server's protocol version for the session, and sends <c>notifications/initialized</c>.</summary>
    public async Task InitializeAsync()
    {
        JsonElement result = await RequestAsync(writer.WriteInitialize(ClientName, ClientVersion)).ConfigureAwait(false);
        session.UseProtocolVersion(McpResponseReader.ReadInitializeResult(result).ProtocolVersion);
        await session.SendNotificationAsync(McpRequestWriter.WriteInitializedNotification(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lists every tool, following <c>nextCursor</c> ("Utilities &gt; Pagination"), one line each: its name, then a tab and its
    /// description with line breaks turned into spaces when it has one.
    /// </summary>
    public async Task ListToolsAsync()
    {
        string? cursor = null;
        do
        {
            JsonElement result = await RequestAsync(writer.WriteListTools(cursor)).ConfigureAwait(false);
            McpToolsListResult page = McpResponseReader.ReadToolsListResult(result);
            foreach (McpTool tool in page.Tools)
            {
                await WriteLineAsync(tool.Description is null ? tool.Name : $"{tool.Name}\t{tool.Description.ReplaceLineEndings(" ")}").ConfigureAwait(false);
            }

            cursor = page.NextCursor;
        }
        while (cursor is not null);
    }

    /// <summary>Calls the tool and writes each content item on its own line: a text item's text, any other item as its JSON.</summary>
    /// <param name="call">The tool and its arguments.</param>
    /// <returns><see langword="false"/> when the result has <c>isError</c> set (a tool execution error, "Error Handling").</returns>
    public async Task<bool> CallToolAsync(McpToolCall call)
    {
        JsonElement response = await RequestAsync(writer.WriteCallTool(call.Name, call.ArgumentsJson)).ConfigureAwait(false);
        McpCallToolResult result = McpResponseReader.ReadCallToolResult(response);
        foreach (McpContent item in result.Content)
        {
            await WriteLineAsync(item.Text ?? item.Json).ConfigureAwait(false);
        }

        return !result.IsError;
    }

    private async Task<JsonElement> RequestAsync(McpOutgoingRequest request)
    {
        McpResponse response = await session.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
        return response.Error is { } error
            ? throw new McpClientException(CurlExitCode.WeirdServerReply, $"MCP server error {error.Code}: {error.Message}")
            : response.Result.GetValueOrDefault();
    }

    private async Task WriteLineAsync(string line)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(line + "\n");
        await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        BytesWritten += bytes.Length;
    }
}
