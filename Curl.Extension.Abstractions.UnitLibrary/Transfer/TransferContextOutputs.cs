namespace Curl.Extension.Abstractions.Transfer;

/// <summary>
/// Lets a transfer hook's <see cref="ITransferHook.ExecuteAsync(ITransferContext, Func{ITransferContext, ValueTask{TransferResult}})" /> run curl's handler with the response
/// written somewhere of its choosing (ADR-1019), such as a buffer it keeps to answer from later.
/// </summary>
public static class TransferContextOutputs
{
    /// <summary>Gives <paramref name="context" /> with its body, <c>-i</c> header and <c>-D</c> header output replaced.</summary>
    /// <param name="context">The transfer the hook was given.</param>
    /// <param name="output">Where the handler writes the response body.</param>
    /// <param name="headerOutput">Where the handler writes the headers <c>-i</c> shows, or <see langword="null" /> for none.</param>
    /// <param name="dumpHeaderOutput">Where the handler writes the headers <c>-D</c> saves, or <see langword="null" /> for none.</param>
    /// <returns>The same transfer, every other member taken from <paramref name="context" />.</returns>
    public static ITransferContext WithOutputs(this ITransferContext context, Stream output, Stream? headerOutput, Stream? dumpHeaderOutput)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(output);
        return new HookedTransferContext(context, output, headerOutput, dumpHeaderOutput, context.Http);
    }
}
