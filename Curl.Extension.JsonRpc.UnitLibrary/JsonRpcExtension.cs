using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.JsonRpc;

/// <summary>
/// The <c>--jsonrpc</c> extension (ADR-1028): JSON-RPC 2.0 calls over HTTP, with ids, batches and replies paired
/// with their calls. <c>Curl.Extension.Composition</c> registers its option, description and transfer hook.
/// </summary>
public static class JsonRpcExtension
{
    /// <summary>Gets the <c>--jsonrpc &lt;calls&gt;</c> option.</summary>
    public static ExtensionOption Option { get; } = new(
        "jsonrpc",
        ExtensionOptionArgument.Value,
        "Send JSON-RPC 2.0 calls \"<method> [params]\", several separated by ';' as a batch, '!' before a method for a notification");

    /// <summary>Gets what <c>curl --extensions-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "jsonrpc",
        "Sends JSON-RPC 2.0 calls over HTTP with ids, batches and replies paired with their calls.",
        [],
        [Option],
        new Uri("https://www.jsonrpc.org/specification"));

    /// <summary>Gives the extension's transfer hook when the option group gave <c>--jsonrpc</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> when <c>--jsonrpc</c> was not given.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.ValueOf(Option) is { } value ? new JsonRpcTransferHook(value) : null;
    }
}
