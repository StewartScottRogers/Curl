using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.ResumableUpload.Transfer;

namespace Curl.Extension.ResumableUpload;

/// <summary>
/// The <c>--resumable-upload</c> extension (ADR-1151): uploads an http or https transfer's <c>-T</c> body by tus 1.0
/// when the server offers it, otherwise by draft-ietf-httpbis-resumable-upload, resuming after a broken connection.
/// <c>Curl.Extension.Composition</c> registers its option, description and transfer hook.
/// </summary>
public static class ResumableUploadExtension
{
    /// <summary>Gets the <c>--resumable-upload</c> flag.</summary>
    public static ExtensionOption Option { get; } = new(
        "resumable-upload",
        ExtensionOptionArgument.None,
        "Upload -T's body resumably: tus 1.0 when the server offers it, otherwise the IETF resumable upload draft");

    /// <summary>Gets what <c>curl --extensions</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "resumable-upload",
        "Uploads an HTTP -T body resumably, by tus 1.0 when the server's OPTIONS offers it and otherwise by draft-ietf-httpbis-resumable-upload, resuming from the server's offset after a broken connection.",
        [],
        [Option],
        new Uri("https://datatracker.ietf.org/doc/draft-ietf-httpbis-resumable-upload/"));

    /// <summary>Gives the extension's transfer hook when the option group gave <c>--resumable-upload</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The resumable upload hook, or <see langword="null" /> when <c>--resumable-upload</c> was not given.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.IsGiven(Option) ? new ResumableUploadTransferHook() : null;
    }
}
