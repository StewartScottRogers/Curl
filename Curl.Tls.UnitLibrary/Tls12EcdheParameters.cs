namespace Curl.Tls;

/// <summary>
/// An ECDHE ServerKeyExchange's <c>ServerECDHParams</c> (RFC 8422 section 5.4): the
/// <c>named_curve</c> curve type, the named group and the server's public point. A curve
/// type other than <c>named_curve</c> (explicit curves, deprecated) decodes as
/// <see cref="TlsAlertDescription.IllegalParameter" />.
/// </summary>
/// <param name="NamedGroup">The named group, such as <see cref="TlsNamedGroup.X25519" />.</param>
/// <param name="PublicKey">The server's ephemeral public value, as the group encodes it.</param>
public sealed record Tls12EcdheParameters(ushort NamedGroup, byte[] PublicKey) : Tls12ServerKeyExchangeParameters
{
    private const byte NamedCurveType = 3;

    /// <inheritdoc />
    internal override void Write(TlsWriter writer)
    {
        writer.WriteUInt8(NamedCurveType);
        writer.WriteUInt16(NamedGroup);
        writer.WriteOpaque(1, PublicKey);
    }

    /// <summary>Reads the parameters.</summary>
    internal static Tls12EcdheParameters Read(TlsReader reader)
    {
        if (reader.ReadUInt8() != NamedCurveType)
        {
            reader.Fail(TlsAlertDescription.IllegalParameter);
        }

        ushort group = reader.ReadUInt16();
        return new Tls12EcdheParameters(group, reader.ReadOpaque(1));
    }
}
