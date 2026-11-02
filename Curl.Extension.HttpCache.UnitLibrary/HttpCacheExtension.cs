using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.HttpCache;

/// <summary>
/// The <c>--cache-dir</c> extension (ADR-1069): a private HTTP cache on disk (RFC 9111) that stores responses,
/// answers from a fresh one and revalidates a stale one with its <c>ETag</c> and <c>Last-Modified</c>.
/// <c>Curl.Extension.Composition</c> registers its option, description and transfer hook.
/// </summary>
public static class HttpCacheExtension
{
    /// <summary>Gets the <c>--cache-dir &lt;dir&gt;</c> option.</summary>
    public static ExtensionOption Option { get; } = new(
        "cache-dir",
        ExtensionOptionArgument.Value,
        "Cache HTTP GET responses in <dir>: reuse fresh ones, revalidate stale ones with ETag and Last-Modified");

    /// <summary>Gets what <c>curl --extended-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "cache-dir",
        "Caches HTTP GET responses on disk, reusing fresh ones and revalidating stale ones with ETag and Last-Modified (RFC 9111).",
        [],
        [Option],
        new Uri("https://www.rfc-editor.org/rfc/rfc9111"));

    /// <summary>Gives the extension's transfer hook when the option group gave <c>--cache-dir</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook over the named directory, or <see langword="null" /> when <c>--cache-dir</c> was not given.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.ValueOf(Option) is { } path ? new HttpCacheTransferHook(new CacheDirectory(path)) : null;
    }
}
