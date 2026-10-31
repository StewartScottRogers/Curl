namespace Curl.Extended.Protocol.GoogleCloudStorage.Client;

/// <summary>
/// The server a <c>gs://bucket/object</c> request goes to (ADR-1099): the XML API endpoint <c>storage.googleapis.com</c> over
/// TLS, or the emulator <c>STORAGE_EMULATOR_HOST</c> names, the variable Google's client libraries read. The bucket is always
/// the first path segment (the XML API's path-style request URI), so any bucket name works with the one certificate.
/// </summary>
/// <param name="Host">The host to connect to.</param>
/// <param name="Port">The port to connect to.</param>
/// <param name="UseTls">Whether the connection uses TLS.</param>
internal sealed record GcsEndpoint(string Host, int Port, bool UseTls)
{
    /// <summary>The XML API's host.</summary>
    internal const string XmlApiHost = "storage.googleapis.com";

    private const int HttpsPort = 443;
    private const int HttpPort = 80;

    /// <summary>Gets the <c>Host</c> header's value: the host, and the port when it is not the scheme's default.</summary>
    internal string HostHeader => Port == (UseTls ? HttpsPort : HttpPort) ? Host : $"{Host}:{Port}";

    /// <summary>Resolves the endpoint.</summary>
    /// <param name="emulatorHost">
    /// <c>STORAGE_EMULATOR_HOST</c>: an <c>http://</c> or <c>https://</c> URL, or a bare <c>host:port</c> taken as <c>http://</c>;
    /// <see langword="null"/> for Cloud Storage itself.
    /// </param>
    /// <returns>The endpoint, or <see langword="null"/> when <paramref name="emulatorHost"/> is not an HTTP or HTTPS endpoint.</returns>
    internal static GcsEndpoint? For(string? emulatorHost)
    {
        if (emulatorHost is null)
        {
            return new GcsEndpoint(XmlApiHost, HttpsPort, true);
        }

        string url = emulatorHost.Contains("://", StringComparison.Ordinal) ? emulatorHost : "http://" + emulatorHost;
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? new GcsEndpoint(uri.IdnHost, uri.Port, uri.Scheme == Uri.UriSchemeHttps)
            : null;
    }

    /// <summary>Gives the path, before percent-encoding, of <paramref name="objectName"/> in <paramref name="bucket"/>; an empty name gives the bucket itself.</summary>
    /// <param name="bucket">The bucket.</param>
    /// <param name="objectName">The object name, or empty for the bucket.</param>
    /// <returns>The absolute path.</returns>
    internal static string PathOf(string bucket, string objectName) => objectName.Length == 0 ? "/" + bucket : $"/{bucket}/{objectName}";
}
