using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.StructuredFields;

/// <summary>
/// The <c>--structured-fields</c> extension (ADR-1152): shows the response's known Structured Field values (RFC 9651)
/// readably in <c>-v</c> output and as <c>-w</c> variables. <c>Curl.Extension.Composition</c> registers its option,
/// description and transfer hook.
/// </summary>
public static class StructuredFieldsExtension
{
    /// <summary>Gets the <c>--structured-fields</c> option.</summary>
    public static ExtensionOption Option { get; } = new(
        "structured-fields",
        ExtensionOptionArgument.None,
        "Show known Structured Field response headers readably in -v and as -w %{structured_fields} and %{structured_field.<name>}");

    /// <summary>Gets what <c>curl --extended-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "structured-fields",
        "Parses the response's known Structured Fields (Cache-Status, Priority, Content-Digest and others) and shows each typed and readable in verbose output and -w variables (RFC 9651).",
        [],
        [Option],
        new Uri("https://www.rfc-editor.org/rfc/rfc9651"));

    /// <summary>Gives the extension's transfer hook when the option group gave <c>--structured-fields</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> when <c>--structured-fields</c> was not given.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.IsGiven(Option) ? new StructuredFieldsTransferHook() : null;
    }
}
