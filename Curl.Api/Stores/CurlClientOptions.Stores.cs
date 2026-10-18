namespace Curl.Api;

public sealed partial record CurlClientOptions
{
    /// <summary>
    /// The cookie, HSTS and alt-svc stores every transfer uses, shared with any other client that
    /// names them; the caller disposes them. <see langword="null"/> (the default) gives the client
    /// its own, made on first use of <see cref="CurlClient.Stores"/> and disposed with the client.
    /// </summary>
    public CurlStores? Stores { get; init; }
}
