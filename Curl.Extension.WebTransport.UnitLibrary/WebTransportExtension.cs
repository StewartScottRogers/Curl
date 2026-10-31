using Curl.Extension.Abstractions.Description;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WebTransport;

/// <summary>The <c>webtransport://</c> extension's description and handler factory (ADR-1089).</summary>
public static class WebTransportExtension
{
    /// <summary>Gets what <c>curl --extensions</c> and <c>curl --ai-help extensions</c> say of it.</summary>
    public static ExtensionDescription Description { get; } = new(
        "webtransport",
        "Opens a WebTransport session over HTTP/3 (draft-ietf-webtrans-http3-16) at https://host[:port]/path (UDP port 443) and sends the -d data on a bidirectional stream, writing the server's answer on it; with ?wt=uni sends it on a unidirectional stream and writes the server's first unidirectional stream; with ?wt=datagram sends it in one HTTP/3 datagram and writes the server's first datagram; then closes the session.",
        ["webtransport"],
        [],
        new Uri("https://datatracker.ietf.org/doc/html/draft-ietf-webtrans-http3-16"));

    /// <summary>Creates the handler over curl's datagram connector and TLS.</summary>
    /// <param name="services">Curl's connection services.</param>
    /// <returns>The handler.</returns>
    public static IProtocolHandler CreateHandler(ProtocolHandlerServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new WebTransportProtocolHandler(services.DatagramConnector, services.TlsProvider);
    }
}
