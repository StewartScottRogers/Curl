namespace Curl.Extension.ResumableUpload.Fields;

/// <summary>The limits a server places on an upload, as its <c>Upload-Limit</c> field states them (draft-ietf-httpbis-resumable-upload §8.2); each is <see langword="null"/> when the server states none.</summary>
/// <param name="MaxSize">The largest upload length, in bytes (<c>max-size</c>).</param>
/// <param name="MinSize">The smallest upload length, in bytes (<c>min-size</c>).</param>
/// <param name="MaxAppendSize">The largest body of one creation or append request, in bytes (<c>max-append-size</c>).</param>
/// <param name="MinAppendSize">The smallest body of a creation or append request that does not complete the upload, in bytes (<c>min-append-size</c>).</param>
/// <param name="Expires">The seconds after which the upload resource may expire (<c>expires</c>).</param>
public sealed record ResumableUploadLimit(
    long? MaxSize = null,
    long? MinSize = null,
    long? MaxAppendSize = null,
    long? MinAppendSize = null,
    long? Expires = null);
