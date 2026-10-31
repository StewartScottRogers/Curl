namespace Curl.Extension.Protocol.S3.Client;

/// <summary>
/// The server an <c>s3://bucket/key</c> request goes to and how the bucket is named (ADR-1090), as the Amazon S3 user guide's
/// "Virtual hosting of buckets" says: on AWS the bucket is in the host name (<c>bucket.s3.region.amazonaws.com</c>), unless it
/// holds a dot, which the service's TLS certificate does not cover, so it goes in the path; another service's endpoint (MinIO,
/// R2) is always addressed path-style (<c>endpoint/bucket/key</c>).
/// </summary>
/// <param name="Host">The host to connect to.</param>
/// <param name="Port">The port to connect to.</param>
/// <param name="UseTls">Whether the connection uses TLS.</param>
/// <param name="PathStyle">Whether the bucket is the first path segment rather than part of <paramref name="Host"/>.</param>
internal sealed record S3Endpoint(string Host, int Port, bool UseTls, bool PathStyle)
{
    private const int HttpsPort = 443;
    private const int HttpPort = 80;

    /// <summary>Gets the <c>Host</c> header's value: the host, and the port when it is not the scheme's default.</summary>
    internal string HostHeader => Port == (UseTls ? HttpsPort : HttpPort) ? Host : $"{Host}:{Port}";

    /// <summary>Resolves the endpoint for <paramref name="bucket"/>.</summary>
    /// <param name="bucket">The bucket, the <c>s3://</c> URL's host.</param>
    /// <param name="region">The region, which names the AWS host.</param>
    /// <param name="endpointUrl">Another service's <c>http://</c> or <c>https://</c> endpoint, or <see langword="null"/> for AWS.</param>
    /// <returns>The endpoint, or <see langword="null"/> when <paramref name="endpointUrl"/> is not an HTTP or HTTPS URL.</returns>
    internal static S3Endpoint? For(string bucket, string region, string? endpointUrl)
    {
        if (endpointUrl is null)
        {
            string awsHost = $"s3.{region}.amazonaws.com";
            bool dotted = bucket.Contains('.', StringComparison.Ordinal);
            return new S3Endpoint(dotted ? awsHost : $"{bucket}.{awsHost}", HttpsPort, true, dotted);
        }

        return Uri.TryCreate(endpointUrl, UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? new S3Endpoint(uri.IdnHost, uri.Port, uri.Scheme == Uri.UriSchemeHttps, true)
            : null;
    }

    /// <summary>Gives the path, before percent-encoding, of <paramref name="key"/> in <paramref name="bucket"/>; an empty key names the bucket itself.</summary>
    /// <param name="bucket">The bucket.</param>
    /// <param name="key">The object key, or empty for the bucket.</param>
    /// <returns>The absolute path.</returns>
    internal string PathOf(string bucket, string key)
    {
        string bucketPath = PathStyle ? "/" + bucket : string.Empty;
        return PathStyle && key.Length == 0 ? bucketPath : $"{bucketPath}/{key}";
    }
}
