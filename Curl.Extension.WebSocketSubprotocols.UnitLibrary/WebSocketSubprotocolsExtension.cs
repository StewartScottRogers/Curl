using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.WebSocketSubprotocols;

/// <summary>
/// The <c>--ws-subprotocol</c> extension (ADR-1085): names a WebSocket subprotocol for a ws or wss transfer and
/// sends that subprotocol's opening messages (graphql-transport-ws, WAMP basic profile, STOMP 1.2), one text frame
/// each. <c>Curl.Extension.Composition</c> registers its option, description and transfer hook.
/// </summary>
public static class WebSocketSubprotocolsExtension
{
    /// <summary>Gets the <c>--ws-subprotocol &lt;name argument&gt;</c> option.</summary>
    public static ExtensionOption Option { get; } = new(
        "ws-subprotocol",
        ExtensionOptionArgument.Value,
        "Speak a WebSocket subprotocol: \"graphql-transport-ws <query>\", \"wamp.2.json <realm> [topic]\" or \"v12.stomp [destination]\"");

    /// <summary>Gets what <c>curl --extensions-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "ws-subprotocol",
        "Speaks graphql-transport-ws, WAMP or STOMP 1.2 over WebSocket: names the subprotocol and sends its opening messages, one text frame each.",
        [],
        [Option],
        new Uri("https://github.com/enisdenjo/graphql-ws/blob/master/PROTOCOL.md"));

    /// <summary>Gives the extension's transfer hook when the option group gave <c>--ws-subprotocol</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> when <c>--ws-subprotocol</c> was not given.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.ValueOf(Option) is { } value ? new WebSocketSubprotocolTransferHook(value) : null;
    }
}
