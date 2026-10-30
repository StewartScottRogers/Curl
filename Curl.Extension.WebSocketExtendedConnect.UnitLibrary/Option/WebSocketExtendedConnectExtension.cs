using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;
using Curl.WebSocket;

namespace Curl.Extension.WebSocketExtendedConnect.Option;

/// <summary>
/// The <c>--ws-extended-connect</c> extension (ADR-1100): opens a ws or wss transfer's WebSocket with extended
/// CONNECT over HTTP/2 (RFC 8441) or HTTP/3 (RFC 9220) instead of the HTTP/1.1 upgrade.
/// <c>Curl.Extension.Composition</c> registers its option, description and transfer hook.
/// </summary>
public static class WebSocketExtendedConnectExtension
{
    /// <summary>Gets the <c>--ws-extended-connect &lt;h2|h3&gt;</c> option.</summary>
    public static ExtensionOption Option { get; } = new(
        "ws-extended-connect",
        ExtensionOptionArgument.Value,
        "Open ws:// or wss:// with extended CONNECT over HTTP/2 (h2) or HTTP/3 (h3, wss only)");

    /// <summary>Gets what <c>curl --extensions</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "ws-extended-connect",
        "Opens a ws:// or wss:// WebSocket with extended CONNECT over HTTP/2 (RFC 8441) or HTTP/3 (RFC 9220) instead of the HTTP/1.1 upgrade, sends any -d data as one text message and writes each data message received, answering pings, until the server closes.",
        [],
        [Option],
        new Uri("https://www.rfc-editor.org/rfc/rfc9220"));

    /// <summary>Gives the extension's transfer hook when the option group gave <c>--ws-extended-connect</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> when <c>--ws-extended-connect</c> was not given.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.ValueOf(Option) is { } version ? new ExtendedConnectTransferHook(version, new SystemWebSocketRandomSource()) : null;
    }
}
