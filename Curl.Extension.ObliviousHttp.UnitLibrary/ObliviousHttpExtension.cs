using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.ObliviousHttp.Relay;

namespace Curl.Extension.ObliviousHttp;

/// <summary>
/// The <c>--ohttp-relay</c> extension (ADR-1157): sends an HTTP transfer as an Oblivious HTTP request
/// (RFC 9458) through the named relay, encapsulated to the gateway key configuration <c>--ohttp-keys</c>
/// names, or to the one the target's <c>/.well-known/ohttp-gateway</c> serves (RFC 9540).
/// <c>Curl.Extension.Composition</c> registers its options, description and transfer hook.
/// </summary>
public static class ObliviousHttpExtension
{
    /// <summary>Gets the <c>--ohttp-relay &lt;url&gt;</c> option.</summary>
    public static ExtensionOption RelayOption { get; } = new(
        "ohttp-relay",
        ExtensionOptionArgument.Value,
        "Send the HTTP request as an Oblivious HTTP request (RFC 9458) through the relay at <url>");

    /// <summary>Gets the <c>--ohttp-keys &lt;file|url&gt;</c> option.</summary>
    public static ExtensionOption KeysOption { get; } = new(
        "ohttp-keys",
        ExtensionOptionArgument.Value,
        "Read the gateway's application/ohttp-keys from <file>, or GET them from an http:// or https:// <url> (needs --ohttp-relay)");

    /// <summary>Gets what <c>curl --extended-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "ohttp-relay",
        "Sends HTTP requests as Oblivious HTTP through a relay, encapsulated to a gateway's key configuration (RFC 9458, RFC 9540).",
        [],
        [RelayOption, KeysOption],
        new Uri("https://www.rfc-editor.org/rfc/rfc9458"));

    /// <summary>Gives the extension's transfer hook when the option group gave <c>--ohttp-relay</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook for the named relay, or <see langword="null" /> when neither option was given.</returns>
    /// <exception cref="ArgumentException"><c>--ohttp-keys</c> was given without <c>--ohttp-relay</c>.</exception>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        string? keys = values.ValueOf(KeysOption);
        return (values.ValueOf(RelayOption), keys) switch
        {
            ({ } relay, _) => new ObliviousHttpTransferHook(relay, keys),
            (null, null) => null,
            _ => throw new ArgumentException("--ohttp-keys needs --ohttp-relay", nameof(values)),
        };
    }
}
