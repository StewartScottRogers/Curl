namespace Curl.Extension.Abstractions.Transfer;

/// <summary>
/// Lets a transfer hook's <see cref="ITransferHook.ExecuteAsync(ITransferContext, TransferHookServices, Func{ITransferContext, ValueTask{TransferResult}})" />
/// run curl's handler with request headers it worked out asynchronously, such as an OAuth 2.0 token it fetched (ADR-1039).
/// </summary>
public static class TransferContextRequestHeaders
{
    /// <summary>Gives <paramref name="context" /> with <paramref name="headers" /> sent after its own request headers.</summary>
    /// <param name="context">The transfer the hook was given, its headers curl's own and the hooks' <see cref="ITransferHook.RequestHeadersFor" />.</param>
    /// <param name="headers">The headers to add, as <c>Name: value</c> lines.</param>
    /// <returns>The same transfer with the headers added; <paramref name="context" /> itself for a transfer that is not HTTP.</returns>
    public static ITransferContext WithRequestHeaders(this ITransferContext context, IEnumerable<string> headers)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(headers);
        return context.Http is null
            ? context
            : new HookedTransferContext(context, context.Output, context.HeaderOutput, context.DumpHeaderOutput, context.Http with { Headers = [.. context.Http.Headers, .. headers] });
    }
}
