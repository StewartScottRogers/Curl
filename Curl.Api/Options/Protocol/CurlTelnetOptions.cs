using Curl.Api.Options.Protocols;

namespace Curl.Api;

/// <summary>
/// TELNET settings, mapped one to one onto <see cref="EasyTelnetOptions"/> (BL-2503, ADR-0640). Set on
/// <see cref="CurlClientOptions.Telnet"/> for every request or on <see cref="CurlRequest.Telnet"/> for
/// one; a request's record replaces the client's whole.
/// </summary>
public sealed record CurlTelnetOptions : IEasyOptionRecord<EasyTelnetOptions>
{
    /// <summary><c>CURLOPT_TELNETOPTIONS</c>: the <c>NAME=value</c> options negotiated. Default none.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public IReadOnlyList<string> TelnetOptions
    {
        get;
        init => field = TransportOptionGuard.NotNull(value, nameof(TelnetOptions));
    } = [];

    /// <summary>An <see cref="EasyTelnetOptions"/> holding every member of this record.</summary>
    /// <returns>The seam's TELNET options.</returns>
    EasyTelnetOptions IEasyOptionRecord<EasyTelnetOptions>.ToEasy() => new() { TelnetOptions = TelnetOptions };
}
