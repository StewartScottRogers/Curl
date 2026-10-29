namespace Curl.Extension.Abstractions.Transfer;

/// <summary>
/// Runs an extension's <see cref="ITransferHook" />s around one of curl's protocol handlers (ADR-1007):
/// the hooks' request headers go after curl's own, the handler writes the body through the hooks' streams
/// before curl's output, and each hook reads the result before curl reports it.
/// </summary>
/// <param name="handler">Curl's handler.</param>
/// <param name="hooks">The hooks, in the order they see the request; the first sees the body first.</param>
internal sealed class TransferHookingProtocolHandler(IProtocolHandler handler, IReadOnlyList<ITransferHook> hooks) : IProtocolHandler
{
    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes => handler.SupportedSchemes;

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        Stream output = context.Output;
        for (int index = hooks.Count - 1; index >= 0; index--)
        {
            output = hooks[index].ResponseBodyOver(output);
        }

        HttpRequestOptions? http = context.Http is null
            ? null
            : context.Http with { Headers = [.. context.Http.Headers, .. hooks.SelectMany(hook => hook.RequestHeadersFor(context))] };
        TransferResult result = await handler.ExecuteAsync(new HookedTransferContext(context, output, http)).ConfigureAwait(false);
        await output.FlushAsync(context.CancellationToken).ConfigureAwait(false);
        foreach (ITransferHook hook in hooks)
        {
            hook.TransferFinished(result);
        }

        return result;
    }
}
