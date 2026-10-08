using System.Text;

namespace Curl.Tls;

/// <summary>
/// The <c>application_layer_protocol_negotiation</c> extension (RFC 7301 section 3.1): the
/// protocol names a client offers, or the one a server selects, as a list either way. The
/// list holds at least one name and every name is 1 to 255 bytes.
/// </summary>
public static class ApplicationLayerProtocolNegotiationExtension
{
    private const int MaximumProtocolNameLength = 255;

    /// <summary>Returns an ALPN extension listing <paramref name="protocols" />.</summary>
    /// <param name="protocols">The protocol names, such as <c>h2</c> and <c>http/1.1</c>, in preference order.</param>
    /// <returns>The extension.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="protocols" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="protocols" /> is empty, a name is empty or longer than 255 bytes in Latin-1,
    /// or the encoded names together exceed 65,535 bytes: RFC 7301 section 3.1 allows none of these.
    /// </exception>
    public static TlsExtension Encode(IReadOnlyList<string> protocols)
    {
        ArgumentNullException.ThrowIfNull(protocols);
        if (protocols.Count == 0)
        {
            throw new ArgumentException("An ALPN extension lists at least one protocol name.", nameof(protocols));
        }

        byte[][] names = [.. protocols.Select(protocol => Encoding.Latin1.GetBytes(protocol))];
        if (names.Any(name => name.Length is 0 or > MaximumProtocolNameLength))
        {
            throw new ArgumentException("An ALPN protocol name is 1 to 255 bytes.", nameof(protocols));
        }

        TlsWriter writer = new();
        writer.WriteVector(2, list =>
        {
            foreach (byte[] name in names)
            {
                list.WriteOpaque(1, name);
            }
        });
        return new TlsExtension(TlsExtensionType.ApplicationLayerProtocolNegotiation, writer.ToArray());
    }

    /// <summary>Decodes ALPN data.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>
    /// The protocol names, or the alert the bytes call for: <see cref="TlsAlertDescription.DecodeError" />
    /// for an empty list or an empty name as well as for truncated or trailing bytes.
    /// </returns>
    public static TlsDecodeResult<IReadOnlyList<string>> Decode(byte[] data)
    {
        TlsReader reader = new(data);
        TlsReader list = reader.ReadVector(2);
        List<string> protocols = [];
        while (list.HasMore)
        {
            protocols.Add(Encoding.Latin1.GetString(list.ReadOpaque(1)));
        }

        if (protocols.Count == 0 || protocols.Contains(string.Empty))
        {
            reader.Fail(TlsAlertDescription.DecodeError);
        }

        return reader.Finish<IReadOnlyList<string>>(protocols);
    }
}
