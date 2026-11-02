using System.Globalization;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.SegmentedDownload;

/// <summary>
/// The <c>--segments</c> extension (ADR-1087): downloads one HTTP resource as several byte ranges fetched in
/// parallel (RFC 9110 §14) and writes them in order, as one body. <c>Curl.Extension.Composition</c> registers
/// its option, description and transfer hook.
/// </summary>
public static class SegmentedDownloadExtension
{
    /// <summary>The most segments one transfer is split into.</summary>
    public const int MaximumSegments = 64;

    /// <summary>Gets the <c>--segments &lt;n&gt;</c> option.</summary>
    public static ExtensionOption Option { get; } = new(
        "segments",
        ExtensionOptionArgument.Value,
        "Download an HTTP GET as <n> byte ranges fetched in parallel (1 to 64)");

    /// <summary>Gets what <c>curl --extended-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "segments",
        "Downloads one HTTP resource as several byte ranges fetched in parallel and joins them in order (RFC 9110 range requests).",
        [],
        [Option],
        new Uri("https://www.rfc-editor.org/rfc/rfc9110#section-14"));

    /// <summary>Gives the extension's transfer hook when the option group gave <c>--segments</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>
    /// The hook splitting into the given number of segments, a hook that refuses the transfer when the number is not
    /// a whole number from 1 to <see cref="MaximumSegments" />, or <see langword="null" /> when <c>--segments</c> was not given.
    /// </returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.ValueOf(Option) switch
        {
            null => null,
            var text when int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count is >= 1 and <= MaximumSegments
                => new SegmentedDownloadTransferHook(count),
            _ => new InvalidSegmentCountTransferHook(),
        };
    }
}
