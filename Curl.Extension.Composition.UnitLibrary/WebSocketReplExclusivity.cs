using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Composition;

/// <summary>
/// Refuses an option group that gives <c>--ws-repl</c> with <c>--ws-subprotocol</c> or <c>--ws-extended-connect</c>:
/// the REPL opens its own plain HTTP/1.1 handshake with no subprotocol, so it would silently drop the other option.
/// The transfer fails with exit 2 naming both options and connects to nothing (ADR-1262).
/// </summary>
public static class WebSocketReplExclusivity
{
    /// <summary>Gives a hook that refuses the transfer when <c>--ws-repl</c> was given with an option it would drop.</summary>
    /// <param name="values">The extension options the option group gave.</param>
    /// <returns>The refusing hook, or <see langword="null" /> when no refused pair was given.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (!values.IsGiven(Curl.Extension.WebSocketRepl.WebSocketReplExtension.Option))
        {
            return null;
        }

        ExtensionOption[] dropped =
        [
            Curl.Extension.WebSocketSubprotocols.WebSocketSubprotocolsExtension.Option,
            Curl.Extension.WebSocketExtendedConnect.Option.WebSocketExtendedConnectExtension.Option,
        ];
        ExtensionOption? other = dropped.FirstOrDefault(values.IsGiven);
        return other is null ? null : new RefusingHook($"--ws-repl and --{other.Name} cannot be used together");
    }

    private sealed class RefusingHook(string message) : ITransferHook
    {
        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next) =>
            ValueTask.FromResult(TransferResult.Failure(CurlExitCode.FailedInit, message));
    }
}
