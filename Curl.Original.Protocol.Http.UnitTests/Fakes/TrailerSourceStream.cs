using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http.Fakes;

/// <summary>
/// A body stream that cannot seek and supplies trailers through <see cref="IUploadTrailerSource" />,
/// as libcurl's read callback does with <c>CURLOPT_TRAILERFUNCTION</c> set, counting each time it is
/// asked; <paramref name="trailers" /> <see langword="null" /> aborts, as <c>CURL_TRAILERFUNC_ABORT</c>.
/// </summary>
/// <param name="content">The bytes it reads.</param>
/// <param name="trailers">The lines it gives, or <see langword="null" /> to abort.</param>
/// <param name="suppliesTrailers">Whether a trailer callback is set at all.</param>
public sealed class TrailerSourceStream(byte[] content, IReadOnlyList<string>? trailers, bool suppliesTrailers = true)
    : MemoryStream(content), IUploadTrailerSource
{
    /// <summary>Gets how many times <see cref="TryTakeTrailers" /> was called.</summary>
    public int Calls { get; private set; }

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public bool SuppliesTrailers => suppliesTrailers;

    /// <inheritdoc />
    public bool TryTakeTrailers(out IReadOnlyList<string> lines)
    {
        Calls++;
        lines = trailers ?? [];
        return trailers is not null;
    }
}
