namespace Curl.Tls;

/// <summary>
/// An SRP ServerKeyExchange's <c>ServerSRPParams</c> (RFC 5054 section 2.8.2): the prime,
/// the generator, the salt and the server's public value, each big-endian.
/// </summary>
/// <param name="Prime">The prime N.</param>
/// <param name="Generator">The generator g.</param>
/// <param name="Salt">The salt s, behind an 8-bit length.</param>
/// <param name="PublicValue">The server's public value B.</param>
public sealed record Tls12SrpParameters(byte[] Prime, byte[] Generator, byte[] Salt, byte[] PublicValue) : Tls12ServerKeyExchangeParameters
{
    /// <inheritdoc />
    internal override void Write(TlsWriter writer)
    {
        writer.WriteOpaque(2, Prime);
        writer.WriteOpaque(2, Generator);
        writer.WriteOpaque(1, Salt);
        writer.WriteOpaque(2, PublicValue);
    }

    /// <summary>Reads the parameters.</summary>
    internal static Tls12SrpParameters Read(TlsReader reader)
    {
        byte[] prime = reader.ReadOpaque(2);
        byte[] generator = reader.ReadOpaque(2);
        byte[] salt = reader.ReadOpaque(1);
        return new Tls12SrpParameters(prime, generator, salt, reader.ReadOpaque(2));
    }
}
