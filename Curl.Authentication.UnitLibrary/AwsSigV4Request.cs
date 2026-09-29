namespace Curl.Authentication;

/// <summary>
/// The parts of one HTTP request that <see cref="AwsSigV4Signer" /> signs, as curl 8.21.0 holds
/// them when it computes <c>--aws-sigv4</c>'s headers.
/// </summary>
public sealed record AwsSigV4Request
{
    /// <summary>
    /// Gets the <c>--aws-sigv4</c> value, <c>provider1[:provider2[:region[:service]]]</c>; empty
    /// means <c>aws:amz</c>, as in curl.
    /// </summary>
    public required string SigV4Parameter { get; init; }

    /// <summary>Gets the user name from <c>-u</c>, the access key ID.</summary>
    public required string UserName { get; init; }

    /// <summary>Gets the password from <c>-u</c>, the secret access key.</summary>
    public required string Password { get; init; }

    /// <summary>Gets the request method, e.g. <c>GET</c>, as it goes on the request line.</summary>
    public required string Method { get; init; }

    /// <summary>
    /// Gets the URL's host name, which names the service and region (<c>service.region.…</c>)
    /// when the parameter does not.
    /// </summary>
    public required string HostName { get; init; }

    /// <summary>
    /// Gets the value of the <c>Host</c> header curl sends, e.g. <c>127.0.0.1:18628</c>, signed
    /// unless a custom <c>Host</c> header replaces it.
    /// </summary>
    public required string HostHeaderValue { get; init; }

    /// <summary>Gets the URL's path as curl's URL parser leaves it, e.g. <c>/bucket/key%20a</c>.</summary>
    public required string Path { get; init; }

    /// <summary>Gets the URL's query without the <c>?</c>; <see langword="null" /> when there is none.</summary>
    public string? Query { get; init; }

    /// <summary>Gets the <c>-H</c> headers, each exactly as given.</summary>
    public IReadOnlyList<string> CustomHeaders { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether curl sends this request as a GET or a HEAD (not a POST,
    /// form, MIME or upload), whatever <c>-X</c> puts on the request line.
    /// </summary>
    public bool IsGetOrHead { get; init; }

    /// <summary>
    /// Gets the in-memory request body from <c>-d</c> and its kin; <see langword="null" /> when
    /// there is none. Signed as the payload hash.
    /// </summary>
    public byte[]? PostFields { get; init; }

    /// <summary>
    /// Gets the size of the file <c>-T</c> uploads; -1 when unknown or when nothing is uploaded.
    /// </summary>
    public long UploadFileSize { get; init; } = -1;

    /// <summary>Gets a value indicating whether <c>--path-as-is</c> is set, which curl refuses to sign.</summary>
    public bool PathAsIs { get; init; }
}
