using System.Buffers.Binary;
using System.Text;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Decodes OpenSSH's own key format, <c>openssh-key-v1</c> (<c>OPENSSH PRIVATE KEY</c>,
/// OpenSSH's <c>PROTOCOL.key</c>): the magic, the cipher and KDF names and options, one
/// public key, and the private section, whose two check integers must match and which
/// holds the key's type and fields.
/// </summary>
/// <remarks>
/// This reads the unencrypted form (cipher and KDF <c>none</c>) of RSA, DSA and ECDSA keys.
/// An encrypted private section (KDF <c>bcrypt</c>) and <c>ssh-ed25519</c> keys are
/// BL-681's: it decrypts the section where <see cref="Read" /> takes it, and adds a type to
/// <see cref="ReadKey" />.
/// </remarks>
internal static class OpenSshPrivateKeyDecoder
{
    // The curve named inside the key, not the suffix, decides which ECDSA key it is.
    private const string EcdsaKeyTypePrefix = "ecdsa-sha2-";

    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("openssh-key-v1\0");

    /// <summary>
    /// Decodes the PEM body of an <c>OPENSSH PRIVATE KEY</c> block.
    /// </summary>
    /// <param name="body">The decoded body.</param>
    /// <returns>
    /// The key, or <see langword="null" /> when the private section is encrypted or the key
    /// type is not RSA, DSA or ECDSA on a NIST curve.
    /// </returns>
    /// <exception cref="InvalidDataException">The structure is malformed, or it holds other than one key.</exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">The key's values are invalid.</exception>
    /// <exception cref="ArgumentException">A DSA key's values are outside what the signer accepts.</exception>
    internal static SshPrivateKey? Read(byte[] body)
    {
        if (!body.AsSpan().StartsWith(Magic))
        {
            throw new InvalidDataException("The key does not start with openssh-key-v1.");
        }

        SshWireReader reader = new(body.AsMemory(Magic.Length));
        string cipher = reader.ReadName();
        string kdf = reader.ReadName();
        reader.ReadString();
        if (reader.ReadUInt32() != 1)
        {
            throw new InvalidDataException("An openssh-key-v1 file holds exactly one key.");
        }

        reader.ReadString();
        ReadOnlyMemory<byte> privateSection = reader.ReadString();
        return cipher == "none" && kdf == "none" ? ReadKey(new SshWireReader(privateSection)) : null;
    }

    private static SshPrivateKey? ReadKey(SshWireReader section)
    {
        ReadOnlySpan<byte> checks = section.ReadBytes(8).Span;
        if (BinaryPrimitives.ReadUInt32BigEndian(checks) != BinaryPrimitives.ReadUInt32BigEndian(checks[4..]))
        {
            throw new InvalidDataException("The openssh-key-v1 check integers differ.");
        }

        string keyType = section.ReadName();
        return keyType switch
        {
            RsaSshPrivateKey.RsaKeyType => ReadRsa(section),
            DsaSshPrivateKey.DsaKeyType => ReadDsa(section),
            _ when keyType.StartsWith(EcdsaKeyTypePrefix, StringComparison.Ordinal) => ReadEcdsa(section),
            _ => null,
        };
    }

    // n, e, d, iqmp, p, q.
    private static RsaSshPrivateKey ReadRsa(SshWireReader section)
    {
        ReadOnlySpan<byte> modulus = section.ReadMpint().Span;
        ReadOnlySpan<byte> exponent = section.ReadMpint().Span;
        ReadOnlySpan<byte> privateExponent = section.ReadMpint().Span;
        ReadOnlySpan<byte> coefficient = section.ReadMpint().Span;
        ReadOnlySpan<byte> prime1 = section.ReadMpint().Span;
        return RsaSshPrivateKey.FromComponents(modulus, exponent, privateExponent, coefficient, prime1, section.ReadMpint().Span);
    }

    // p, q, g, y, x.
    private static DsaSshPrivateKey ReadDsa(SshWireReader section)
    {
        byte[] prime = section.ReadMpint().ToArray();
        byte[] subprime = section.ReadMpint().ToArray();
        byte[] generator = section.ReadMpint().ToArray();
        byte[] publicKey = section.ReadMpint().ToArray();
        return DsaSshPrivateKey.Create(prime, subprime, generator, publicKey, section.ReadMpint().ToArray());
    }

    // The curve's SSH name, Q, then d.
    private static EcdsaSshPrivateKey? ReadEcdsa(SshWireReader section)
    {
        string curve = section.ReadName();
        byte[] publicPoint = section.ReadString().ToArray();
        return EcdsaSshPrivateKey.FromCurveIdentifier(curve, section.ReadMpint().Span, publicPoint);
    }
}
