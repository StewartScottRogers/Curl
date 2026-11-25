using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.GoogleCloudStorage.Client;

/// <summary>
/// Refuses <c>-r</c>/<c>--range</c> and <c>-C</c>/<c>--continue-at</c> on a <c>gs://</c> upload or listing (ADR-1359):
/// only an object download sends a <c>Range</c> header, so a resumed listing never appends the whole listing to the
/// partial file.
/// </summary>
internal static class GcsByteRangeRefusal
{
    /// <summary>
    /// Gives why <paramref name="context"/> asks for part of something that is transferred only whole, or
    /// <see langword="null"/> when it asks for all of it. A resume from offset 0 asks for all of it.
    /// </summary>
    /// <param name="context">The upload or listing, whose <c>-r</c> and <c>-C</c> are read.</param>
    /// <returns>The refusal's message, or <see langword="null"/>.</returns>
    internal static string? RefusalOf(ITransferContext context)
    {
        if (context.RangeText is not null || context.Range is not null)
        {
            return MessageFor("-r/--range");
        }

        return context.ResumeUploadFromUnknownOffset || context.ResumeFrom.GetValueOrDefault() != 0
            ? MessageFor("-C/--continue-at")
            : null;
    }

    private static string MessageFor(string option) =>
        $"gs:// does not support {option} on an upload or a listing; only an object download takes a byte range";
}
