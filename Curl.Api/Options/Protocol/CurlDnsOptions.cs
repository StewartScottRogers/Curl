using Curl.Api.Codes;

namespace Curl.Api;

/// <summary>
/// The <c>dns://</c> extension's options (ADR-1319), set on <see cref="CurlRequest.Dns"/>: what the <c>curl</c> tool's
/// <c>--dns-transport</c> and <c>--dns-json</c> set. They exist only while Curl's extensions are on; with the
/// <c>CURL_EXTENSIONS</c> environment variable <c>0</c> a request that sets this record fails with <see cref="CurlCode.UnknownOption"/>,
/// as the tool refuses an unknown option.
/// </summary>
public sealed record CurlDnsOptions
{
    /// <summary><c>--dns-transport</c>: the transport name (<c>udp</c>, <c>tcp</c>, and the others the option lists). Default <see langword="null"/>: the extension's default.</summary>
    public string? Transport { get; init; }

    /// <summary><c>--dns-json</c>: write the answer as JSON in place of dig-like text. Default <see langword="false"/>.</summary>
    public bool Json { get; init; }

    /// <summary>The option values these settings give the extensions, by long name.</summary>
    /// <returns>One entry per option set; a flag's value is empty.</returns>
    internal IReadOnlyDictionary<string, string> ToExtensionOptions()
    {
        Dictionary<string, string> values = [];
        if (Transport is not null)
        {
            values["dns-transport"] = Transport;
        }

        if (Json)
        {
            values["dns-json"] = string.Empty;
        }

        return values;
    }
}
