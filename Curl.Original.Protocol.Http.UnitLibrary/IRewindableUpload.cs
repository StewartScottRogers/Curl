namespace Curl.Original.Protocol.Http;

/// <summary>
/// An upload stream that cannot seek but says whether curl's seek callback could still take it
/// back to its start for a resend, as the curl tool's <c>lseek</c> of stdin can when stdin is a
/// file and cannot when it is a pipe off Windows (ADR-0524, BL-2337). A stream that does not
/// implement it is taken as one that can.
/// </summary>
public interface IRewindableUpload
{
    /// <summary>
    /// Gets a value indicating whether curl could rewind the upload: when it could not, a chunked
    /// resend of it to an HTTP/1.0 server fails with exit 65 <c>seek callback returned error 2</c>
    /// before it connects, rather than with exit 25 once connected.
    /// </summary>
    bool CanRewind { get; }
}
