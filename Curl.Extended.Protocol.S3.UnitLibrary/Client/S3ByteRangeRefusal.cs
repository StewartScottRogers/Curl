using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.S3.Client;

/// <summary>
/// Refuses <c>-r</c>/<c>--range</c> and <c>-C</c>/<c>--continue-at</c> on an <c>s3://</c> upload, list or delete
/// (ADR-1375), none of which sends a <c>Range</c> header; a download sends one, so it is let through.
/// </summary>
internal static class S3ByteRangeRefusal
{
    /// <summary>
    /// Gives why <paramref name="context"/> asks for a range the transfer cannot send, or <see langword="null"/> when
    /// it is a download or asks for none. A resume from offset 0 asks for none.
    /// </summary>
    /// <param name="context">The transfer, whose <c>-T</c>, <c>-X</c>, <c>-r</c> and <c>-C</c> are read.</param>
    /// <param name="namesPrefix">Whether the URL names a prefix, so that the transfer lists it.</param>
    /// <returns>The refusal's message, or <see langword="null"/>.</returns>
    internal static string? RefusalOf(ITransferContext context, bool namesPrefix) =>
        IsDownload(context, namesPrefix) ? null : RefusalOfEitherOption(context);

    private static bool IsDownload(ITransferContext context, bool namesPrefix) =>
        context.Upload is null && context.Http?.CustomMethod != "DELETE" && !namesPrefix;

    private static string? RefusalOfEitherOption(ITransferContext context)
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
        $"{context.Url.Scheme}:// does not support {option} on an upload, list or delete; only a download sends a range";
}
