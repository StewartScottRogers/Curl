using System.Security.Authentication;

namespace Curl.Networking;

/// <summary>
/// Turns the minimum and ceiling of <see cref="TlsClientOptions" /> into the
/// <see cref="SslProtocols" /> set <see cref="SslStreamTlsProvider" /> offers.
/// </summary>
/// <remarks>
/// This is the one place in <c>Curl.Networking.UnitLibrary</c> that names the TLS 1.0 and
/// TLS 1.1 members .NET marks obsolete (<c>SYSLIB0039</c>): curl still offers them on
/// request, so the provider asks <see cref="System.Net.Security.SslStream" /> for them, and
/// whether the operating system's TLS stack will negotiate them is its answer to give
/// (BL-502). Where it refuses and an official curl build connects, the hand-built TLS client
/// of BL-714 closes the gap.
/// </remarks>
internal static class TlsVersionRange
{
#pragma warning disable SYSLIB0039 // curl still offers TLS 1.0 and 1.1 on request; see the remarks.
    /// <summary>TLS 1.0, <see cref="SslProtocols.Tls" />.</summary>
    internal const SslProtocols Tls10 = SslProtocols.Tls;

    /// <summary>TLS 1.1, <see cref="SslProtocols.Tls11" />.</summary>
    internal const SslProtocols Tls11 = SslProtocols.Tls11;
#pragma warning restore SYSLIB0039

    /// <summary>
    /// The versions to offer, from <paramref name="minimum" /> up to <paramref name="maximum" />.
    /// </summary>
    /// <param name="minimum">
    /// The lowest version; <see cref="TlsVersion.SystemDefault" /> starts the range at TLS 1.0,
    /// as curl's Schannel build does, when a ceiling below TLS 1.3 is set.
    /// </param>
    /// <param name="maximum">The highest version; <see cref="TlsVersion.SystemDefault" /> is TLS 1.3.</param>
    /// <returns>
    /// <see cref="SslProtocols.None" />, which leaves the choice to the operating system, when
    /// neither end is set or only a TLS 1.3 ceiling is; otherwise every version in the range.
    /// </returns>
    /// <exception cref="ArgumentException">Both ends are set and the minimum is above the ceiling.</exception>
    internal static SslProtocols ToSslProtocols(TlsVersion minimum, TlsVersion maximum)
    {
        var lowest = minimum == TlsVersion.SystemDefault ? TlsVersion.Tls10 : minimum;
        var highest = maximum == TlsVersion.SystemDefault ? TlsVersion.Tls13 : maximum;
        if (lowest > highest)
        {
            throw new ArgumentException($"The minimum TLS version {minimum} is above the ceiling {maximum}.", nameof(minimum));
        }

        return minimum == TlsVersion.SystemDefault && highest == TlsVersion.Tls13
            ? SslProtocols.None
            : EveryVersionBetween(lowest, highest);
    }

    private static SslProtocols EveryVersionBetween(TlsVersion lowest, TlsVersion highest)
    {
        var offered = SslProtocols.None;
        for (var version = lowest; version <= highest; version++)
        {
            offered |= ToSslProtocol(version);
        }

        return offered;
    }

    private static SslProtocols ToSslProtocol(TlsVersion version) => version switch
    {
        TlsVersion.Tls10 => Tls10,
        TlsVersion.Tls11 => Tls11,
        TlsVersion.Tls12 => SslProtocols.Tls12,
        _ => SslProtocols.Tls13,
    };
}
