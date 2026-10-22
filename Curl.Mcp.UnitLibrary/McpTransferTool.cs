using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;

namespace Curl.Mcp;

/// <summary>
/// The logic of the MCP tool <c>transfer</c> (ADR-0548, ADR-0593): it checks the request with
/// <see cref="McpTransferRequestCheck" />, and only when it is valid runs one easy handle of the
/// injected <see cref="ICurlEngine" /> with the options <see cref="McpTransferOptions" /> builds, the
/// body captured up to the cap and the headers collected by <see cref="McpResponseHeaderCollector" />.
/// A <see cref="CurlCode" /> other than <see cref="CurlCode.Ok" /> is a tool error carrying libcurl's
/// error text and the code.
/// </summary>
/// <param name="engine">The transfer engine whose easy handle runs the request.</param>
/// <param name="cap">The most bytes of response body the tool returns.</param>
public sealed class McpTransferTool(ICurlEngine engine, McpResponseCap cap)
{
    /// <summary>
    /// Runs <paramref name="request" />, unless a field is invalid or
    /// <paramref name="cancellationToken" /> is cancelled first.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The MCP request's cancellation token, passed to the transfer.</param>
    /// <returns>The tool's result.</returns>
    public async Task<McpTransferToolResult> RunAsync(McpTransferRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        McpTransferRequestCheck check = McpTransferRequestCheck.Check(request);
        if (!check.IsValid)
        {
            return McpTransferToolResult.Failed(check.Failure!);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return McpTransferToolResult.Failed(McpToolFailure.Cancelled);
        }

        await using IEasyTransfer transfer = engine.CreateEasy();
        using McpCappedCaptureStream body = new(cap);
        McpResponseHeaderCollector headers = new();
        transfer.Options = McpTransferOptions.Build(request, check.Body) with
        {
            Callbacks = new EasyCallbackOptions { Write = data => WriteBody(body, data), Header = headers.Collect },
        };
        CurlCode code;
        try
        {
            code = await transfer.PerformAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return McpTransferToolResult.Failed(McpToolFailure.Cancelled);
        }

        return ToResult(code, transfer, body, headers, cancellationToken);
    }

    /// <summary>The write callback: keeps the chunk (up to the cap) and reports all of it taken, since libcurl aborts the transfer on a short count.</summary>
    private static long WriteBody(McpCappedCaptureStream body, ReadOnlySpan<byte> data)
    {
        body.Write(data);
        return data.Length;
    }

    /// <summary>
    /// Builds the result of a finished transfer: cancellation wins over a late result, a code other
    /// than <see cref="CurlCode.Ok" /> becomes a tool error with libcurl's error text, and success
    /// carries the last response's status, headers, body and info.
    /// </summary>
    private static McpTransferToolResult ToResult(
        CurlCode code,
        IEasyTransfer transfer,
        McpCappedCaptureStream body,
        McpResponseHeaderCollector headers,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return McpTransferToolResult.Failed(McpToolFailure.Cancelled);
        }

        if (code != CurlCode.Ok)
        {
            return McpTransferToolResult.Failed(McpToolFailure.FromCurlExit((int)code, transfer.ErrorText)!);
        }

        return new McpTransferToolResult(
            transfer.Info.ResponseCode,
            headers.Headers,
            body.ToReturnedBytes(),
            McpTransferInfo.From(transfer.Info),
            null);
    }
}
