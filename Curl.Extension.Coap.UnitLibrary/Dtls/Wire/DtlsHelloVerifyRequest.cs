using System.Buffers.Binary;
using Curl.Tls;

namespace Curl.Extension.Coap.Dtls.Wire;

/// <summary>
/// The body of a HelloVerifyRequest (RFC 6347 section 4.2.1): the server's version and the
/// stateless cookie the client echoes in its second ClientHello.
/// </summary>
/// <param name="ServerVersion">The server's version; DTLS 1.0's {254, 255} or DTLS 1.2's.</param>
/// <param name="Cookie">The cookie, 0 to <see cref="MaximumCookieLength" /> bytes.</param>
internal sealed record DtlsHelloVerifyRequest(ushort ServerVersion, byte[] Cookie)
{
    /// <summary>hello_verify_request's handshake type, 3, which <see cref="HandshakeType" /> has no name for.</summary>
    public const HandshakeType MessageType = (HandshakeType)3;

    /// <summary>The longest cookie DTLS 1.2 allows.</summary>
    public const int MaximumCookieLength = 255;

    /// <summary>Writes the body: the version, the cookie's one-byte length and the cookie.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The cookie is longer than <see cref="MaximumCookieLength" />.</exception>
    public byte[] Encode()
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Cookie.Length, MaximumCookieLength, nameof(Cookie));
        byte[] body = new byte[3 + Cookie.Length];
        BinaryPrimitives.WriteUInt16BigEndian(body, ServerVersion);
        body[2] = (byte)Cookie.Length;
        Cookie.CopyTo(body, 3);
        return body;
    }

    /// <summary>Reads a HelloVerifyRequest body.</summary>
    /// <exception cref="FormatException">The body is shorter than 3 bytes or its length does not match the cookie's.</exception>
    public static DtlsHelloVerifyRequest Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < 3 || body.Length != 3 + body[2])
        {
            throw new FormatException($"A {body.Length}-byte HelloVerifyRequest does not hold a version, a cookie length and that many cookie bytes.");
        }

        return new DtlsHelloVerifyRequest(BinaryPrimitives.ReadUInt16BigEndian(body), body[3..].ToArray());
    }
}
