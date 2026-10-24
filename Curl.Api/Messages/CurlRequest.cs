namespace Curl.Api;

/// <summary>
/// One request <see cref="CurlClient.SendAsync"/> performs: the method, the URL, the headers in the
/// order and casing given, and an optional in-memory body. Immutable; change one part with a
/// <see langword="with"/> expression.
/// </summary>
public sealed partial record CurlRequest
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
    /// Names of default headers libcurl adds that the request removes, each sent as the
    /// <c>CURLOPT_HTTPHEADER</c> line <c>Name:</c> after <see cref="Headers"/>. Default: none.
    /// </summary>
    public IReadOnlyList<string> RemovedHeaders
    {
        get;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = [];

    /// <summary>
    /// The request body, or <see langword="null"/> for none: bytes (<c>CURLOPT_POSTFIELDS</c> with
    /// their size; a byte array converts implicitly), a stream (<see cref="CurlContent.FromStream"/>) or a
    /// multipart body (<see cref="CurlMultipartContent"/>).
    /// A body sent with a method other than <c>POST</c> keeps that method. Default <see langword="null"/>.
    /// </summary>
    public CurlContent? Body { get; init; }
}
