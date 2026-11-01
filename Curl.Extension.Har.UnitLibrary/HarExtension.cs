using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.Har;

/// <summary>
/// The <c>--har &lt;file&gt;</c> extension (ADR-1146): writes the option group's HTTP transfers to a file as an
/// HTTP Archive, HAR 1.2. <c>Curl.Extension.Composition</c> registers its option, description and transfer hook.
/// </summary>
public static class HarExtension
{
    /// <summary>Gets the <c>--har</c> option.</summary>
    public static ExtensionOption Option { get; } = new(
        "har",
        ExtensionOptionArgument.Value,
        "Write the HTTP transfers to <file> as an HTTP Archive (HAR 1.2)");

    /// <summary>Gets what <c>curl --extensions-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "har",
        "Records each HTTP request and response, with headers, bodies and timings, to a HAR 1.2 HTTP Archive file.",
        [],
        [Option],
        new Uri("https://w3c.github.io/web-performance/specs/HAR/Overview.html"));

    /// <summary>Gives the extension's transfer hook when the option group gave <c>--har</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, writing to the file named, or <see langword="null" /> when <c>--har</c> was not given.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.ValueOf(Option) is { } path ? new HarTransferHook(path) : null;
    }
}
