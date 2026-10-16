namespace Curl.Api;

/// <summary>
/// One request <see cref="CurlClient.SendAsync"/> performs: the method, the URL, the headers in the
/// order and casing given, and an optional in-memory body. Immutable; change one part with a
/// <see langword="with"/> expression.
/// </summary>
public sealed record CurlRequest
{
    /// <summary>A request with no headers and no body.</summary>
    /// <param name="method">The request method.</param>
    /// <param name="url">The URL, passed to the engine as given (<c>CURLOPT_URL</c>).</param>
    /// <exception cref="ArgumentNullException"><paramref name="method"/> or <paramref name="url"/> is <see langword="null"/>.</exception>
    public CurlRequest(HttpMethod method, string url)
    {
        Method = method;
        Url = url;
    }

    /// <summary>
    /// The request method: <c>GET</c> and <c>HEAD</c> map to libcurl's own, <c>POST</c> to
    /// <c>CURLOPT_POST</c>, any other to <c>CURLOPT_CUSTOMREQUEST</c>.
    /// </summary>
    public HttpMethod Method
    {
        get;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>The URL (<c>CURLOPT_URL</c>), passed to the engine unparsed so every URL curl takes is accepted.</summary>
    public string Url
    {
        get;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// The request headers (<c>CURLOPT_HTTPHEADER</c>), sent in this order with this casing,
    /// duplicates included. An empty value sends the header with no value (<c>Name;</c>). Default: none.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> Headers
    {
        get;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = [];

    /// <summary>
    /// The request body (<c>CURLOPT_POSTFIELDS</c> with its size), or <see langword="null"/> for none.
    /// A body sent with a method other than <c>POST</c> keeps that method. Default <see langword="null"/>.
    /// </summary>
    public ReadOnlyMemory<byte>? Body { get; init; }
}
