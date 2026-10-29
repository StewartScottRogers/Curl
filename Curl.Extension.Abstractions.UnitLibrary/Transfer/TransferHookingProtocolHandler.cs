namespace Curl.Extension.Abstractions.Transfer;

/// <summary>
/// Runs an extension's <see cref="ITransferHook" />s around one of curl's protocol handlers (ADR-1007):
/// the hooks' request headers go after curl's own, the handler writes the body through the hooks' streams
/// before curl's output, each hook may answer the transfer itself or replace the response the handler
/// received (ADR-1019), and each hook reads the result before curl reports it. When a hook asks for the
/// transfer to be repeated (ADR-1009), it waits that long on the transfer's <see cref="TimeProvider" /> and
/// runs the handler again, asking the hooks' request headers afresh.
/// </summary>
/// <param name="handler">Curl's handler.</param>
/// <param name="hooks">The hooks, in the order they see the request; the first sees the body first.</param>
/// <param name="services">What the host lends the hooks to make their own requests (ADR-1039), or <see langword="null" /> for none.</param>
internal sealed class TransferHookingProtocolHandler(IProtocolHandler handler, IReadOnlyList<ITransferHook> hooks, TransferHookServices? services = null) : IProtocolHandler
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

        TransferResult result = await ExecuteOnceAsync(context, output).ConfigureAwait(false);
        for (TimeSpan? delay = RepeatAfter(result); delay is { } wait; delay = RepeatAfter(result))
        {
            if (!await WaitAsync(context, wait).ConfigureAwait(false))
            {
                break;
            }

            result = await ExecuteOnceAsync(context, output).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>Runs the handler once with the hooks' request headers asked afresh, then lets each hook read the result.</summary>
    private async ValueTask<TransferResult> ExecuteOnceAsync(ITransferContext context, Stream output)
    {
        HttpRequestOptions? http = context.Http is null
            ? null
            : context.Http with { Headers = [.. context.Http.Headers, .. hooks.SelectMany(hook => hook.RequestHeadersFor(context))] };
        ITransferContext hooked = new HookedTransferContext(context, output, context.HeaderOutput, context.DumpHeaderOutput, http);
        TransferResult result = await ExecuteThroughHooksAsync(0, hooked).ConfigureAwait(false);
        await output.FlushAsync(context.CancellationToken).ConfigureAwait(false);
        foreach (ITransferHook hook in hooks)
        {
            hook.TransferFinished(result);
        }

        return result;
    }

    /// <summary>
    /// Runs the hooks' <see cref="ITransferHook.ExecuteAsync(ITransferContext, Func{ITransferContext, ValueTask{TransferResult}})" /> from <paramref name="index" /> on, the first
    /// outermost, each given the rest as its next step, and curl's handler innermost (ADR-1019); each hook
    /// gets the services when the host gave some (ADR-1039).
    /// </summary>
    private ValueTask<TransferResult> ExecuteThroughHooksAsync(int index, ITransferContext context)
    {
        if (index == hooks.Count)
        {
            return handler.ExecuteAsync(context);
        }

        Func<ITransferContext, ValueTask<TransferResult>> next = inner => ExecuteThroughHooksAsync(index + 1, inner);
        return services is null
            ? hooks[index].ExecuteAsync(context, next)
            : hooks[index].ExecuteAsync(context, services, next);
    }

    /// <summary>Gives the delay the first hook asking for a repeat names, or <see langword="null" /> when none asks.</summary>
    private TimeSpan? RepeatAfter(TransferResult result) =>
        hooks.Select(hook => hook.RepeatAfter(result)).FirstOrDefault(delay => delay is not null);

    /// <summary>Waits <paramref name="delay" /> on the transfer's clock; <see langword="false" /> when the transfer was cancelled first.</summary>
    private static async ValueTask<bool> WaitAsync(ITransferContext context, TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay, context.TimeProvider, context.CancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
