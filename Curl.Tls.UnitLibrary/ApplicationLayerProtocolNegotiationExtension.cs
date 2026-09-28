using System.Text;

namespace Curl.Tls;

/// <summary>
/// The <c>application_layer_protocol_negotiation</c> extension (RFC 7301 section 3.1): the
/// protocol names a client offers, or the one a server selects, as a list either way.
/// </summary>
public static class ApplicationLayerProtocolNegotiationExtension
{
    /// <summary>Returns an ALPN extension listing <paramref name="protocols" />.</summary>
    /// <param name="protocols">The protocol names, such as <c>h2</c> and <c>http/1.1</c>, in preference order.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension Encode(IReadOnlyList<string> protocols)
    {
        ArgumentNullException.ThrowIfNull(protocols);
        TlsWriter writer = new();
        writer.WriteVector(2, list =>
        {
            foreach (string protocol in protocols)
            {
                list.WriteOpaque(1, Encoding.Latin1.GetBytes(protocol));
            }
        });
        return new TlsExtension(TlsExtensionType.ApplicationLayerProtocolNegotiation, writer.ToArray());
    }

    /// <summary>Decodes ALPN data.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The protocol names, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<IReadOnlyList<string>> Decode(byte[] data)
    {
        TlsReader reader = new(data);
        TlsReader list = reader.ReadVector(2);
        List<string> protocols = [];
        while (list.HasMore)
        {
            protocols.Add(Encoding.Latin1.GetString(list.ReadOpaque(1)));
        }

        return reader.Finish<IReadOnlyList<string>>(protocols);
    }
}
