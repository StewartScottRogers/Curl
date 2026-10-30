using System.Runtime.InteropServices;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.ClientHints;

/// <summary>
/// The <c>--client-hints</c> extension (ADR-1102): User-Agent Client Hints (RFC 8942, WICG User-Agent Client Hints).
/// It sends the low-entropy <c>Sec-CH-UA</c>, <c>Sec-CH-UA-Mobile</c> and <c>Sec-CH-UA-Platform</c> fields with every
/// HTTP request to a potentially trustworthy URL. <c>Curl.Extension.Composition</c> registers its option, description
/// and transfer hook.
/// </summary>
public static class ClientHintsExtension
{
    /// <summary>Gets the <c>--client-hints</c> option.</summary>
    public static ExtensionOption Option { get; } = new(
        "client-hints",
        ExtensionOptionArgument.None,
        "Send the Sec-CH-UA, Sec-CH-UA-Mobile and Sec-CH-UA-Platform client hints to https and loopback URLs");

    /// <summary>Gets what <c>curl --extensions</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "client-hints",
        "Sends the browser low-entropy User-Agent Client Hints (Sec-CH-UA, Sec-CH-UA-Mobile, Sec-CH-UA-Platform) to https and loopback URLs (RFC 8942, WICG UA-CH).",
        [],
        [Option],
        new Uri("https://wicg.github.io/ua-client-hints/"));

    /// <summary>Gives the extension's transfer hook when the option group gave <c>--client-hints</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> when <c>--client-hints</c> was not given.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.IsGiven(Option) ? new ClientHintsTransferHook(ClientHintsPlatform.NameFor(RuntimeInformation.IsOSPlatform)) : null;
    }
}
