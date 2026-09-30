using System.Formats.Asn1;
using System.Security.Cryptography;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Reads a <c>--key</c> file in every format either of curl's SSH backends reads
/// (ADR-0122, ADR-0230): PKCS #1 RSA, OpenSSL DSA and SEC 1 EC PEM, each plain or with
/// legacy PEM encryption; PKCS #8, plain or encrypted, for RSA, ECDSA, DSA and Ed25519;
/// and <c>openssh-key-v1</c>, plain or encrypted with bcrypt. The file's first PEM block decides.
/// </summary>
internal static class SshPrivateKeyReader
{
    // One decoder per PEM label; each takes the block and the --pass bytes.
    private static readonly Dictionary<string, Func<PemBlock, byte[], SshPrivateKey?>> Decoders = new(StringComparer.Ordinal)
    {
        ["RSA PRIVATE KEY"] = (block, passphrase) => RsaSshPrivateKey.FromPkcs1(LegacyPemDecryption.Decrypt(block, passphrase)),
        ["DSA PRIVATE KEY"] = (block, passphrase) => Asn1PrivateKeyDecoder.ReadDsa(LegacyPemDecryption.Decrypt(block, passphrase)),
        ["EC PRIVATE KEY"] = (block, passphrase) => Asn1PrivateKeyDecoder.ReadEcPrivateKey(LegacyPemDecryption.Decrypt(block, passphrase), curveOid: null),
        ["PRIVATE KEY"] = (block, _) => Asn1PrivateKeyDecoder.ReadPkcs8(block.Body),
        ["ENCRYPTED PRIVATE KEY"] = (block, passphrase) => Asn1PrivateKeyDecoder.ReadPkcs8(Pkcs8Decryption.Decrypt(block.Body, passphrase)),
        ["OPENSSH PRIVATE KEY"] = (block, passphrase) => OpenSshPrivateKeyDecoder.Read(block.Body, passphrase),
    };

    /// <summary>
    /// Reads the key in <paramref name="text" />.
    /// </summary>
    /// <param name="text">The file's text.</param>
    /// <param name="passphrase">The <c>--pass</c> bytes; empty when it was not given, as curl hands libssh2 an empty passphrase.</param>
    /// <returns>
    /// The key, or <see langword="null" /> when the file holds no key this reads, is
    /// malformed, or is encrypted and the passphrase does not open it: libssh2 then fails
    /// the <c>publickey</c> method and curl goes on to the next.
    /// </returns>
    internal static SshPrivateKey? Read(string text, byte[] passphrase)
    {
        try
        {
            return PemBlock.Find(text) is { } block && Decoders.TryGetValue(block.Label, out Func<PemBlock, byte[], SshPrivateKey?>? decode)
                ? decode(block, passphrase)
                : null;
        }
        catch (Exception exception) when (exception is CryptographicException or AsnContentException or InvalidDataException or FormatException or ArgumentException)
        {
            return null;
        }
    }
}
