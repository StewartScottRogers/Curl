namespace Curl.Extension.Abstractions.Transfer;

/// <summary>
/// Lets a transfer hook's <see cref="ITransferHook.ExecuteAsync(ITransferContext, TransferHookServices, Func{ITransferContext, ValueTask{TransferResult}})" />
/// run curl's handler without <c>--compressed</c>, so it receives the body as the server encoded it and decodes it
/// itself, such as a coding curl's handler refuses (BL-4351).
/// </summary>
public static class TransferContextContentDecoding
{
    /// <summary>Gives <paramref name="context" /> with <see cref="HttpRequestOptions.Compressed" /> off.</summary>
    /// <param name="context">The transfer the hook was given.</param>
    /// <returns>
    /// The same transfer, whose handler neither sends its own <c>Accept-Encoding</c> nor decodes the content;
    /// <paramref name="context" /> itself for a transfer that is not HTTP.
    /// </returns>
    public static ITransferContext WithContentDecodingOff(this ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Http is null
            ? context
            : new HookedTransferContext(context, context.Output, context.HeaderOutput, context.DumpHeaderOutput, context.Http with { Compressed = false });
    }
}
