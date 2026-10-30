using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Certificates;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Dane.Dnssec;
using Curl.Extension.Dane.Lookup;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Dane;

/// <summary>
/// The <c>--dane &lt;resolver&gt;</c> extension (ADR-1155): DNSSEC (RFC 4033-4035) and DANE TLSA (RFC 6698, RFC 7671).
/// After curl verified a server's chain, it looks up <c>_&lt;port&gt;._tcp.&lt;host&gt;</c>'s TLSA records through the
/// named recursive resolver, validates them itself from the root trust anchors down, and refuses the connection with
/// exit 60 when they are bogus, cannot be fetched, or are secure and none matches the chain.
/// <c>Curl.Extension.Composition</c> registers its option, description and certificate check hook.
/// </summary>
public static class DaneExtension
{
    /// <summary>Gets the <c>--dane</c> option.</summary>
    public static ExtensionOption Option { get; } = new(
        "dane",
        ExtensionOptionArgument.Value,
        "Check the server certificate against its DNSSEC-validated TLSA records, queried through this resolver host[:port]");

    /// <summary>Gets what <c>curl --extensions</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "dane",
        "Validates the server's TLSA records with DNSSEC from the root trust anchors and refuses a certificate chain they do not match (exit 60) (RFC 4033-4035, RFC 6698, RFC 7671).",
        [],
        [Option],
        new Uri("https://www.rfc-editor.org/rfc/rfc6698"));

    /// <summary>Gets how long one DNS query, over UDP and then TCP, may take: the common resolver timeout of five seconds.</summary>
    internal static TimeSpan QueryTimeout { get; } = TimeSpan.FromSeconds(5);

    /// <summary>Gives the extension's certificate check hook when the option group gave <c>--dane</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> when <c>--dane</c> was not given.</returns>
    public static ICertificateCheckHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.ValueOf(Option) is { } resolver ? new DaneCheckHook(resolver, LookupThrough) : null;
    }

    /// <summary>Builds a lookup that queries <paramref name="resolver" /> over the network, checked against the system clock.</summary>
    /// <param name="resolver">The recursive resolver <c>--dane</c> named.</param>
    /// <returns>The lookup, from the root trust anchors.</returns>
    internal static TlsaLookup LookupThrough(DaneResolverAddress resolver)
    {
        var systemResolver = new SystemDnsResolver();
        var tlsProvider = new SslStreamTlsProvider(new TlsClientOptions(), TimeProvider.System);
        var transport = new DnssecQueryTransport(
            new UdpDatagramConnector(systemResolver, TimeProvider.System),
            new TcpConnector(systemResolver, new TcpDialer(), tlsProvider, TimeProvider.System),
            resolver.Host,
            resolver.Port,
            TimeProvider.System,
            QueryTimeout);
        return new TlsaLookup(transport, new RrsigVerifier(TimeProvider.System), RootTrustAnchors.DelegationSigners);
    }
}
