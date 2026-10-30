using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.CompressionDictionary;

/// <summary>
/// The <c>--compression-dictionary</c> extension (ADR-1154): Compression Dictionary Transport (RFC 9842). It stores
/// responses that carry <c>Use-As-Dictionary</c> in a directory, advertises the best matching one with
/// <c>Available-Dictionary</c> and the <c>dcb</c> and <c>dcz</c> content codings, and decodes a response compressed
/// against it. <c>Curl.Extension.Composition</c> registers its option, description and transfer hook.
/// </summary>
public static class CompressionDictionaryExtension
{
    /// <summary>Gets the <c>--compression-dictionary &lt;dir&gt;</c> option.</summary>
    public static ExtensionOption Option { get; } = new(
        "compression-dictionary",
        ExtensionOptionArgument.Value,
        "Keep shared dictionaries in <dir>: store Use-As-Dictionary responses, advertise them and decode dcb and dcz bodies");

    /// <summary>Gets what <c>curl --extensions</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "compression-dictionary",
        "Stores shared dictionaries a server offers with Use-As-Dictionary and uses them to receive dcb (Brotli) and dcz (Zstandard) responses (RFC 9842).",
        [],
        [Option],
        new Uri("https://www.rfc-editor.org/rfc/rfc9842"));

    /// <summary>Gives the extension's transfer hook when the option group gave <c>--compression-dictionary</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook over the named directory, or <see langword="null" /> when the option was not given.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.ValueOf(Option) is { } path ? new CompressionDictionaryTransferHook(path) : null;
    }
}
