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
/// This reads RSA, DSA, ECDSA and Ed25519 keys, unencrypted (cipher and KDF <c>none</c>)
/// or encrypted with KDF <c>bcrypt</c> and a cipher
/// <see cref="OpenSshPrivateSectionDecryption" /> reads. A wrong passphrase leaves check
/// integers that differ.
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
    /// <param name="passphrase">The <c>--pass</c> bytes, which open an encrypted private section.</param>
    /// <returns>
    /// The key, or <see langword="null" /> when the cipher and KDF are neither both
    /// <c>none</c> nor <c>bcrypt</c> with a cipher this reads, or the key type is not RSA,
    /// DSA, Ed25519 or ECDSA on a NIST curve.
    /// </returns>
    /// <exception cref="InvalidDataException">The structure is malformed, it holds other than one key, or the check integers differ, as a wrong passphrase leaves them.</exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">The key's values are invalid, or a GCM tag fails.</exception>
    /// <exception cref="ArgumentException">A DSA key's values are outside what the signer accepts, or the passphrase is empty for an encrypted key.</exception>
    internal static SshPrivateKey? Read(byte[] body, byte[] passphrase)
    {
        if (!body.AsSpan().StartsWith(Magic))
        {
            throw new InvalidDataException("The key does not start with openssh-key-v1.");
        }

        SshWireReader reader = new(body.AsMemory(Magic.Length));
        string cipher = reader.ReadName();
        string kdf = reader.ReadName();
        ReadOnlyMemory<byte> kdfOptions = reader.ReadString();
        if (reader.ReadUInt32() != 1)
        {
            throw new InvalidDataException("An openssh-key-v1 file holds exactly one key.");
        }

        reader.ReadString();
        ReadOnlyMemory<byte> privateSection = reader.ReadString();
        byte[]? section = OpenSection(cipher, kdf, kdfOptions, privateSection, reader, passphrase);
        return section is null ? null : ReadKey(new SshWireReader(section));
    }

    // The private section as it is when unencrypted, decrypted when bcrypt names a cipher,
    // and null for any other pairing, which libssh2 does not read.
    private static byte[]? OpenSection(string cipher, string kdf, ReadOnlyMemory<byte> kdfOptions, ReadOnlyMemory<byte> privateSection, SshWireReader afterSection, byte[] passphrase) =>
        (cipher, kdf) switch
        {
            ("none", "none") => privateSection.ToArray(),
            (not "none", "bcrypt") => OpenSshPrivateSectionDecryption.Decrypt(cipher, kdfOptions, privateSection, afterSection, passphrase),
            _ => null,
        };

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
            Ed25519SshPrivateKey.Ed25519KeyType => ReadEd25519(section),
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

    // The public key, then the seed followed by the public key again.
    private static Ed25519SshPrivateKey ReadEd25519(SshWireReader section)
    {
        ReadOnlySpan<byte> publicKey = section.ReadString().Span;
        return Ed25519SshPrivateKey.FromOpenSshFields(publicKey, section.ReadString().Span);
    }

    // The curve's SSH name, Q, then d.
    private static EcdsaSshPrivateKey? ReadEcdsa(SshWireReader section)
    {
        string curve = section.ReadName();
        byte[] publicPoint = section.ReadString().ToArray();
        return EcdsaSshPrivateKey.FromCurveIdentifier(curve, section.ReadMpint().Span, publicPoint);
    }
}
