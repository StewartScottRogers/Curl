using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.ConnectRpc;

/// <summary>
/// The <c>--connect-rpc</c> extension (ADR-1139): a Connect protocol unary or streaming call over HTTP, the request
/// message given with <c>-d</c>. <c>Curl.Extension.Composition</c> registers its option, description and transfer hook.
/// </summary>
public static class ConnectRpcExtension
{
    /// <summary>Gets the <c>--connect-rpc &lt;unary|stream&gt;</c> option.</summary>
    public static ExtensionOption Option { get; } = new(
        "connect-rpc",
        ExtensionOptionArgument.Value,
        "Send a Connect call, \"unary\" or \"stream\", with the JSON message(s) given by -d as the request");

    /// <summary>Gets what <c>curl --extended-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "connect-rpc",
        "Sends a Connect protocol unary or streaming call with JSON messages and writes the reply messages one per line.",
        [],
        [Option],
        new Uri("https://connectrpc.com/docs/protocol"));

    /// <summary>Gives the extension's transfer hook when the option group gave <c>--connect-rpc</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> when <c>--connect-rpc</c> was not given.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.ValueOf(Option) is { } value ? new ConnectTransferHook(value) : null;
    }
}
