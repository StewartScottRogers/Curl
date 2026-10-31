using Curl.Extension.Protocol.McpClient.Stdio;
using Curl.Extension.Protocol.McpClient.StreamableHttp;
using Curl.Extension.Protocol.McpClient.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.McpClient.Client;

/// <summary>
/// Serves <c>mcp://host[:port]/endpoint</c> and <c>mcps://</c> over Streamable HTTP and <c>mcp+stdio://localhost/command?args</c>
/// over stdio (ADR-1074): without a body it lists the server's tools, with a <c>-d</c> body (<see cref="McpToolCall"/>) it
/// calls one. The exchange itself is <see cref="McpClientFlow"/>.
/// </summary>
/// <param name="connector">Opens the connection to an HTTP endpoint.</param>
/// <param name="processStarter">Starts a local server.</param>
internal sealed class McpProtocolHandler(IConnector connector, IMcpServerProcessStarter processStarter) : IProtocolHandler
{
    /// <summary>The scheme of the local-process form.</summary>
    internal const string StdioScheme = "mcp+stdio";

    /// <summary>The error text for a <c>-d</c> body that is not a tool call.</summary>
    internal const string NotAToolCall = "MCP: the data must be a JSON object with a string \"name\" and an optional object \"arguments\"";

    /// <summary>The error text for an <c>mcp+stdio</c> URL that names no local program.</summary>
    internal const string NoLocalCommand = "MCP: an mcp+stdio URL names a local program: mcp+stdio://localhost/command?arg1&arg2";

    /// <summary>The error text when the tool reports a tool execution error.</summary>
    internal const string ToolReportedError = "MCP: the tool reported an error";

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes { get; } = ["mcp", McpHttpSession.SecureScheme, StdioScheme];

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        McpToolCall? call = context.PostData is { } body ? McpToolCall.From(body) : null;
        if (context.PostData is not null && call is null)
        {
            return TransferResult.Failure(CurlExitCode.BadFunctionArgument, NotAToolCall);
        }

        IMcpSession session;
        try
        {
            session = OpenSession(context);
        }
        catch (McpClientException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message);
        }

        context.Progress.ReportTransferStarted();
        await using (session.ConfigureAwait(false))
        {
            var flow = new McpClientFlow(session, context.Output, context.CancellationToken);
            return await TransferAsync(flow, session, call, context.CancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Gives the exit code an exception from the exchange ends the transfer with, or <see langword="null"/> for one it does not handle.</summary>
    internal static CurlExitCode? ExitCodeOf(Exception exception) => exception switch
    {
        McpClientException failure => failure.ExitCode,
        McpHttpException or McpWireFormatException or McpServerExitedException => CurlExitCode.WeirdServerReply,
        _ => null,
    };

    private static async Task<TransferResult> TransferAsync(McpClientFlow flow, IMcpSession session, McpToolCall? call, CancellationToken cancellationToken)
    {
        try
        {
            await flow.InitializeAsync().ConfigureAwait(false);
            bool succeeded = true;
            if (call is null)
            {
                await flow.ListToolsAsync().ConfigureAwait(false);
            }
            else
            {
                succeeded = await flow.CallToolAsync(call).ConfigureAwait(false);
            }

            await session.EndAsync(cancellationToken).ConfigureAwait(false);
            return succeeded
                ? TransferResult.Success(flow.BytesWritten)
                : new TransferResult(CurlExitCode.HttpReturnedError, flow.BytesWritten, ToolReportedError);
        }
        catch (Exception failure) when (ExitCodeOf(failure) is { } exitCode)
        {
            return new TransferResult(exitCode, flow.BytesWritten, failure.Message);
        }
    }

    private IMcpSession OpenSession(ITransferContext context)
    {
        if (!string.Equals(context.Url.Scheme, StdioScheme, StringComparison.OrdinalIgnoreCase))
        {
            return new McpHttpSession(connector, context);
        }

        McpStdioCommand command = McpStdioCommand.From(context.Url)
            ?? throw new McpClientException(CurlExitCode.UrlMalformat, NoLocalCommand);
        return McpStdioClientSession.Start(processStarter, command, context.TimeProvider);
    }
}
