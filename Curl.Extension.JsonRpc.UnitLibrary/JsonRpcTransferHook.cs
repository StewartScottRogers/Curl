using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.JsonRpc;

/// <summary>
/// What <c>--jsonrpc</c> does to an HTTP transfer (ADR-1028): sends its calls as the request body, a POST of
/// <c>application/json</c> in place of any <c>-d</c> data, and writes the server's responses paired with the calls
/// (<see cref="JsonRpcReplyPairing" />). A transfer that is not HTTP runs unchanged; a value that names no valid
/// call fails the transfer with exit 2 before anything is sent.
/// </summary>
/// <param name="value">The value given to <c>--jsonrpc</c>.</param>
internal sealed class JsonRpcTransferHook(string value) : ITransferHook
{
    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        if (context.Http is not { } http)
        {
            return await next(context).ConfigureAwait(false);
        }

        IReadOnlyList<JsonRpcCall> calls;
        try
        {
            calls = JsonRpcCallList.Parse(value);
        }
        catch (FormatException invalid)
        {
            return TransferResult.Failure(CurlExitCode.FailedInit, invalid.Message);
        }

        using MemoryStream reply = new();
        HttpRequestOptions request = http with { Body = new BytesBody(JsonRpcRequestEncoding.Encode(calls), JsonRpcRequestEncoding.MediaType) };
        TransferResult result = await next(new JsonRpcRequestContext(context, reply, request)).ConfigureAwait(false);
        byte[] paired = JsonRpcReplyPairing.Pair(reply.GetBuffer().AsMemory(0, (int)reply.Length), calls);
        await context.Output.WriteAsync(paired, context.CancellationToken).ConfigureAwait(false);
        return result;
    }
}
