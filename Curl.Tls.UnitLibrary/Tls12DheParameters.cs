namespace Curl.Tls;

/// <summary>
/// A DHE ServerKeyExchange's <c>ServerDHParams</c> (RFC 5246 section 7.4.3): the prime,
/// the generator and the server's public value, each big-endian.
/// </summary>
/// <param name="Prime">The prime p.</param>
/// <param name="Generator">The generator g.</param>
/// <param name="PublicValue">The server's public value g^X mod p.</param>
public sealed record Tls12DheParameters(byte[] Prime, byte[] Generator, byte[] PublicValue) : Tls12ServerKeyExchangeParameters
{
    /// <inheritdoc />
    internal override void Write(TlsWriter writer)
    {
        writer.WriteOpaque(2, Prime);
        writer.WriteOpaque(2, Generator);
        writer.WriteOpaque(2, PublicValue);
    }

    /// <summary>Reads the parameters.</summary>
    internal static Tls12DheParameters Read(TlsReader reader)
    {
        byte[] prime = reader.ReadOpaque(2);
        byte[] generator = reader.ReadOpaque(2);
        return new Tls12DheParameters(prime, generator, reader.ReadOpaque(2));
    }
}
