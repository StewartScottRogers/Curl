using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.WebSocketRepl.Transfer;
using Curl.WebSocket;

namespace Curl.Extension.WebSocketRepl;

/// <summary>
/// The <c>--ws-repl</c> extension (ADR-1081): on a <c>ws://</c> or <c>wss://</c> URL, opens the connection itself,
/// performs RFC 6455's opening handshake and holds an interactive session, typed lines from standard input sent as
/// messages and received messages written to the transfer's output. <c>Curl.Extension.Composition</c> registers its
/// option, description and transfer hook.
/// </summary>
public static class WebSocketReplExtension
{
    /// <summary>Gets the <c>--ws-repl</c> flag.</summary>
    public static ExtensionOption Option { get; } = new(
        "ws-repl",
        ExtensionOptionArgument.None,
        "Hold an interactive WebSocket session on a ws:// or wss:// URL: send typed lines, show replies, /binary, /ping, /close");

    /// <summary>Gets what <c>curl --extended-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "ws-repl",
        "Holds an interactive WebSocket session: sends each typed line as text, binary or a ping, shows each reply, and answers pings and closes.",
        [],
        [Option],
        new Uri("https://www.rfc-editor.org/rfc/rfc6455"));

    /// <summary>Gives the extension's transfer hook when the option group gave <c>--ws-repl</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, reading lines from standard input, or <see langword="null" /> when <c>--ws-repl</c> was not given.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.IsGiven(Option) ? new WebSocketReplTransferHook(Console.In, new SystemWebSocketRandomSource()) : null;
    }
}
