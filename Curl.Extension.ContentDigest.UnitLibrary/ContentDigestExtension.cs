using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.ContentDigest;

/// <summary>
/// The <c>--content-digest</c> extension (ADR-1094): HTTP digest fields (RFC 9530). It sends a <c>-d</c> body's
/// <c>Content-Digest</c> and <c>Repr-Digest</c>, asks for the server's with <c>Want-Content-Digest</c> and
/// <c>Want-Repr-Digest</c>, and fails the transfer when a digest the server sent does not match the body.
/// <c>Curl.Extension.Composition</c> registers its option, description and transfer hook.
/// </summary>
public static class ContentDigestExtension
{
    /// <summary>Gets the <c>--content-digest</c> option.</summary>
    public static ExtensionOption Option { get; } = new(
        "content-digest",
        ExtensionOptionArgument.None,
        "Send Content-Digest and Repr-Digest with -d data and check the server's against the body (exit 56 on a mismatch)");

    /// <summary>Gets what <c>curl --extended-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "content-digest",
        "Sends SHA-256 Content-Digest and Repr-Digest fields with uploads and checks the server's digest fields against the response body (RFC 9530).",
        [],
        [Option],
        new Uri("https://www.rfc-editor.org/rfc/rfc9530"));

    /// <summary>Gives the extension's transfer hook when the option group gave <c>--content-digest</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> when <c>--content-digest</c> was not given.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.IsGiven(Option) ? new ContentDigestTransferHook() : null;
    }
}
