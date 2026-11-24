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

    /// <summary>Splits a <c>gs://</c> URL's query string into its parameters, percent-decoded (a <c>+</c> is a space); a parameter with no <c>=</c> has an empty value.</summary>
    /// <param name="query">The query string without its <c>?</c>, or <see langword="null"/> when the URL has none.</param>
    /// <returns>The parameters in the order the URL gives them.</returns>
    internal static List<KeyValuePair<string, string>> QueryOf(string? query) =>
        [.. (query ?? string.Empty).Split('&', StringSplitOptions.RemoveEmptyEntries).Select(parameter =>
        {
            int equals = parameter.IndexOf('=', StringComparison.Ordinal);
            return equals < 0
                ? new KeyValuePair<string, string>(Decode(parameter), string.Empty)
                : new KeyValuePair<string, string>(Decode(parameter[..equals]), Decode(parameter[(equals + 1)..]));
        })];

    /// <summary>Gives the bucket as the URL's authority text wrote it, because <c>Host</c> rewrites an all-digit or hex name into a dotted quad (EXT-120).</summary>
    /// <param name="originalUrl">The URL as the command line gave it.</param>
    /// <param name="fallback">The normalised host, used when the URL has no <c>://</c> or the authority names no bucket.</param>
    /// <returns>The bucket name.</returns>
    internal static string BucketOf(string originalUrl, string fallback)
    {
        int schemeEnd = originalUrl.IndexOf("://", StringComparison.Ordinal);
        string rest = schemeEnd < 0 ? string.Empty : originalUrl[(schemeEnd + 3)..];
        string authority = rest.Split('/', '?', '#')[0];
        string hostAndPort = authority[(authority.LastIndexOf('@') + 1)..];
        string name = hostAndPort.Split(':')[0];
        return name.Length == 0 || name[0] == '[' ? fallback : name;
    }

    private static string Decode(string text) => Uri.UnescapeDataString(text.Replace('+', ' '));

    /// <summary>Gives the path, before percent-encoding, of <paramref name="objectName"/> in <paramref name="bucket"/>; an empty name gives the bucket itself.</summary>
    /// <param name="bucket">The bucket.</param>
    /// <param name="objectName">The object name, or empty for the bucket.</param>
    /// <returns>The absolute path.</returns>
    internal static string PathOf(string bucket, string objectName) => objectName.Length == 0 ? "/" + bucket : $"/{bucket}/{objectName}";
}
