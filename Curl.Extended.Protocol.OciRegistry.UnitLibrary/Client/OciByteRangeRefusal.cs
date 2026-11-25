using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.OciRegistry.Client;

/// <summary>
/// Refuses <c>-r</c>/<c>--range</c> and <c>-C</c>/<c>--continue-at</c> on an <c>oci://</c> transfer (ADR-1354), which
/// moves only whole manifests and blobs, so a resumed pull never appends the whole artifact to the partial file.
/// </summary>
internal static class OciByteRangeRefusal
{
    /// <summary>
    /// Gives why <paramref name="context"/> asks for part of a manifest or blob, or <see langword="null"/> when it asks
    /// for all of it. A resume from offset 0 asks for all of it.
    /// </summary>
    /// <param name="context">The transfer, whose <c>-r</c> and <c>-C</c> are read.</param>
    /// <returns>The refusal's message, or <see langword="null"/>.</returns>
    internal static string? RefusalOf(ITransferContext context)
    {
        if (context.RangeText is not null || context.Range is not null)
        {
            return MessageFor(context, "-r/--range");
        }

        return context.ResumeUploadFromUnknownOffset || context.ResumeFrom.GetValueOrDefault() != 0
            ? MessageFor(context, "-C/--continue-at")
            : null;
    }

    private static string MessageFor(ITransferContext context, string option) =>
        $"{context.Url.Scheme}:// does not support {option}; it transfers only whole manifests and blobs";
}
