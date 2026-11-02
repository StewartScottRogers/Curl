using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.DeltaDownload;

/// <summary>
/// The <c>--zsync</c> extension (ADR-1092): uses a zsync 0.6 control file to take the blocks of a file it already
/// holds from a local seed file and fetch only the changed ones as HTTP byte ranges. <c>Curl.Extension.Composition</c>
/// registers its options, description and transfer hook.
/// </summary>
public static class DeltaDownloadExtension
{
    /// <summary>Gets the <c>--zsync &lt;control-file&gt;</c> option.</summary>
    public static ExtensionOption Option { get; } = new(
        "zsync",
        ExtensionOptionArgument.Value,
        "Fetch only the blocks a local file lacks, by the given .zsync control file");

    /// <summary>Gets the <c>--zsync-seed &lt;file&gt;</c> option.</summary>
    public static ExtensionOption SeedOption { get; } = new(
        "zsync-seed",
        ExtensionOptionArgument.Value,
        "The local file --zsync takes unchanged blocks from (default: the control file's Filename)");

    /// <summary>Gets what <c>curl --extended-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "zsync",
        "Fetches only the changed blocks of a file over HTTP ranges, taking the rest from a local copy, by a zsync 0.6 control file.",
        [],
        [Option, SeedOption],
        new Uri("http://zsync.moria.org.uk/"));

    /// <summary>Gives the extension's transfer hook when the option group gave <c>--zsync</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> when <c>--zsync</c> was not given.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.ValueOf(Option) is { } controlPath ? new DeltaDownloadTransferHook(controlPath, values.ValueOf(SeedOption)) : null;
    }
}
