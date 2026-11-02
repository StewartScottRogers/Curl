using System.Security.Cryptography;
using Curl.Extension.Abstractions.Description;
using Curl.Extended.Protocol.Coap.Observe;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Coap.Client;

/// <summary>The <c>coap://</c> extension's description and handler factory (ADR-1047).</summary>
public static class CoapExtension
{
    /// <summary>Gets what <c>curl --extended-help</c> and <c>curl --ai-help extensions</c> say of it.</summary>
    public static ExtensionDescription Description { get; } = new(
        "coap",
        "Makes CoAP requests over UDP (RFC 7252), and over DTLS 1.2 for coaps:// (RFC 6347): a confirmable GET, or POST, PUT or DELETE with -X, the -d data as the payload, retransmitted until acknowledged, and writes the response payload; -i shows the response code; --coap-observe follows the resource's notifications (RFC 7641).",
        ["coap", "coaps"],
        [CoapObserveTransferHook.Option],
        new Uri("https://www.rfc-editor.org/rfc/rfc7252"));

    /// <summary>Creates the handler over curl's datagram connector and TLS provider, with system randomness for Message IDs, tokens and timeouts.</summary>
    /// <param name="services">Curl's connection services.</param>
    /// <returns>The handler.</returns>
    public static IProtocolHandler CreateHandler(ProtocolHandlerServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new CoapProtocolHandler(services.DatagramConnector, RandomNumberGenerator.GetBytes, services.TlsProvider);
    }
}
