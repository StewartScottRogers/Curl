using System.Net;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Certificates;
using Curl.Extension.Dane.Lookup;
using Curl.Extension.Dane.Tlsa;

namespace Curl.Extension.Dane;

/// <summary>
/// Refuses a chain curl accepted when the server's TLSA records, validated with DNSSEC, say it must not stand
/// (ADR-1153, RFC 6698 section 4.1): when they are bogus, when no usable answer came, or when they are secure and have
/// usable records none of which matches the chain. A proved-insecure zone, a proved absence of TLSA records, a set
/// with no usable records and a server named by an IP address leave the chain to curl's own verification.
/// </summary>
/// <param name="resolver">The <c>--dane</c> value naming the recursive resolver.</param>
/// <param name="lookupThrough">Builds the lookup that queries a resolver.</param>
internal sealed class DaneCheckHook(string resolver, Func<DaneResolverAddress, TlsaLookup> lookupThrough) : ICertificateCheckHook
{
    /// <inheritdoc />
    public ExtensionOption Option => DaneExtension.Option;

    /// <inheritdoc />
    public async ValueTask<string?> RefusalOfAsync(IReadOnlyList<ReadOnlyMemory<byte>> peerCertificates, string host, int? port, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(peerCertificates);
        ArgumentNullException.ThrowIfNull(host);
        if (DaneResolverAddress.Parse(resolver) is not { } address)
        {
            return $"DANE: the resolver {resolver} is not a host, host:port or [IPv6]:port";
        }

        if (IPAddress.TryParse(host, out _))
        {
            return null;
        }

        if (port is not { } tcpPort)
        {
            return $"DANE: the port of the connection to {host} is unknown, so its TLSA records cannot be looked up";
        }

        string name = $"_{tcpPort}._tcp.{host}";
        TlsaLookupResult result;
        try
        {
            result = await lookupThrough(address).LookupAsync(host, tcpPort, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            return $"DANE: {name} is not a DNS name, so its TLSA records cannot be looked up";
        }

        return RefusalOf(result, peerCertificates, name);
    }

    private string? RefusalOf(TlsaLookupResult result, IReadOnlyList<ReadOnlyMemory<byte>> peerCertificates, string name)
    {
        if (result.Status == TlsaLookupStatus.Secure)
        {
            return TlsaCertificateMatcher.Match([.. peerCertificates.Select(certificate => certificate.ToArray())], result.Records) == TlsaMatchResult.NoMatch
                ? $"DANE: no TLSA record of {name} matches the server's certificate chain"
                : null;
        }

        if (result.Status == TlsaLookupStatus.Bogus)
        {
            return $"DANE: the DNSSEC validation of {name}'s TLSA records failed (bogus)";
        }

        return result.Status == TlsaLookupStatus.Failed
            ? $"DANE: the resolver {resolver} gave no usable answer for {name}'s TLSA records"
            : null;
    }
}
