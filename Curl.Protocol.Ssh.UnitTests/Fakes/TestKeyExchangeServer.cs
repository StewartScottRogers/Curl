using System.Security.Cryptography;
using System.Text;
using Curl.Cryptography;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// The server's half of one key exchange, computed from the RFCs' definitions by hand: the
/// messages the server sends after its <c>KEXINIT</c>, the messages the client must send,
/// and the shared secret and exchange hash both sides must reach.
/// </summary>
/// <param name="ServerPayloads">What the server sends, in order, before its <c>NEWKEYS</c>.</param>
/// <param name="ClientPayloads">What the client must send, in order, before its <c>NEWKEYS</c>.</param>
/// <param name="SharedSecret">K, unsigned big-endian.</param>
/// <param name="ExchangeHash">H.</param>
/// <param name="Hash">The method's hash.</param>
internal sealed record TestKeyExchangeServer(
    List<byte[]> ServerPayloads,
    List<byte[]> ClientPayloads,
    byte[] SharedSecret,
    byte[] ExchangeHash,
    HashAlgorithmName Hash)
{
    /// <summary>The server's identification string.</summary>
    internal const string ServerIdentification = "SSH-2.0-OpenSSH_9.7";

    /// <summary>The client's identification string.</summary>
    internal const string ClientIdentification = "SSH-2.0-libssh2_1.11.1";

    /// <summary>
    /// Answers the client's exchange with <paramref name="method" />.
    /// </summary>
    internal static TestKeyExchangeServer Answer(
        string method,
        TestHostKey hostKey,
        TestEphemeralKeys keys,
        byte[] clientKexInit,
        byte[] serverKexInit)
    {
        byte[] common = Join(Name(ClientIdentification), Name(ServerIdentification), String(clientKexInit), String(serverKexInit), String(hostKey.Blob));
        return method switch
        {
            "ecdh-sha2-nistp256" => Ecdh(ECCurve.NamedCurves.nistP256, HashAlgorithmName.SHA256, hostKey, keys, common),
            "ecdh-sha2-nistp384" => Ecdh(ECCurve.NamedCurves.nistP384, HashAlgorithmName.SHA384, hostKey, keys, common),
            "ecdh-sha2-nistp521" => Ecdh(ECCurve.NamedCurves.nistP521, HashAlgorithmName.SHA512, hostKey, keys, common),
            "diffie-hellman-group1-sha1" => FiniteField(FiniteFieldDiffieHellmanGroup.Group2, HashAlgorithmName.SHA1, hostKey, common),
            "diffie-hellman-group14-sha1" => FiniteField(FiniteFieldDiffieHellmanGroup.Group14, HashAlgorithmName.SHA1, hostKey, common),
            "diffie-hellman-group14-sha256" => FiniteField(FiniteFieldDiffieHellmanGroup.Group14, HashAlgorithmName.SHA256, hostKey, common),
            "diffie-hellman-group16-sha512" => FiniteField(FiniteFieldDiffieHellmanGroup.Group16, HashAlgorithmName.SHA512, hostKey, common),
            "diffie-hellman-group18-sha512" => FiniteField(FiniteFieldDiffieHellmanGroup.Group18, HashAlgorithmName.SHA512, hostKey, common),
            "diffie-hellman-group-exchange-sha1" => GroupExchange(HashAlgorithmName.SHA1, hostKey, common),
            _ => GroupExchange(HashAlgorithmName.SHA256, hostKey, common),
        };
    }

    /// <summary>
    /// The server's reply for a finite-field method with an arbitrary f and signature, for
    /// the failure tests.
    /// </summary>
    internal static byte[] FiniteFieldReply(byte messageNumber, byte[] hostKeyBlob, byte[] f, byte[] signatureBlob) =>
        [messageNumber, .. String(hostKeyBlob), .. Mpint(f), .. String(signatureBlob)];

    /// <summary>
    /// The server's reply for an ECDH method with an arbitrary point and signature, for the
    /// failure tests.
    /// </summary>
    internal static byte[] EcdhReply(byte[] hostKeyBlob, byte[] point, byte[] signatureBlob) =>
        [31, .. String(hostKeyBlob), .. String(point), .. String(signatureBlob)];

    private static TestKeyExchangeServer Ecdh(ECCurve curve, HashAlgorithmName hash, TestHostKey hostKey, TestEphemeralKeys keys, byte[] common)
    {
        ECParameters clientKey = keys.Client(curve);
        using ECDiffieHellman serverKey = ECDiffieHellman.Create(keys.Server(curve));
        using ECDiffieHellman clientPublic = ECDiffieHellman.Create(new ECParameters { Curve = curve, Q = clientKey.Q });
        byte[] clientPoint = [0x04, .. clientKey.Q.X!, .. clientKey.Q.Y!];
        ECPoint serverQ = keys.Server(curve).Q;
        byte[] serverPoint = [0x04, .. serverQ.X!, .. serverQ.Y!];
        byte[] k = serverKey.DeriveRawSecretAgreement(clientPublic.PublicKey);
        byte[] h = CryptographicOperations.HashData(hash, Join(common, String(clientPoint), String(serverPoint), Mpint(k)));
        return new TestKeyExchangeServer(
            [EcdhReply(hostKey.Blob, serverPoint, hostKey.Sign(h))],
            [[30, .. String(clientPoint)]],
            k,
            h,
            hash);
    }

    private static TestKeyExchangeServer FiniteField(FiniteFieldDiffieHellmanGroup group, HashAlgorithmName hash, TestHostKey hostKey, byte[] common)
    {
        (byte[] e, byte[] f, byte[] k) = Round(group);
        byte[] h = CryptographicOperations.HashData(hash, Join(common, Mpint(e), Mpint(f), Mpint(k)));
        return new TestKeyExchangeServer(
            [FiniteFieldReply(31, hostKey.Blob, f, hostKey.Sign(h))],
            [[30, .. Mpint(e)]],
            k,
            h,
            hash);
    }

    private static TestKeyExchangeServer GroupExchange(HashAlgorithmName hash, TestHostKey hostKey, byte[] common)
    {
        FiniteFieldDiffieHellmanGroup group = FiniteFieldDiffieHellmanGroup.Group14;
        (byte[] e, byte[] f, byte[] k) = Round(group);
        byte[] sizes = Join(UInt32(2048), UInt32(4096), UInt32(4096));
        byte[] p = group.Prime.ToArray();
        byte[] g = group.Generator.ToArray();
        byte[] h = CryptographicOperations.HashData(hash, Join(common, sizes, Mpint(p), Mpint(g), Mpint(e), Mpint(f), Mpint(k)));
        return new TestKeyExchangeServer(
            [[31, .. Mpint(p), .. Mpint(g)], FiniteFieldReply(33, hostKey.Blob, f, hostKey.Sign(h))],
            [[34, .. sizes], [32, .. Mpint(e)]],
            k,
            h,
            hash);
    }

    private static (byte[] E, byte[] F, byte[] K) Round(FiniteFieldDiffieHellmanGroup group)
    {
        using FiniteFieldDiffieHellman clientKey = new(group, TestEphemeralKeys.ClientExponent);
        using FiniteFieldDiffieHellman serverKey = new(group, TestEphemeralKeys.ServerExponent);
        byte[] e = new byte[group.PrimeLength];
        byte[] f = new byte[group.PrimeLength];
        byte[] k = new byte[group.PrimeLength];
        clientKey.ComputePublicValue(e);
        serverKey.ComputePublicValue(f);
        serverKey.TryComputeSharedSecret(e, k);
        return (e, f, k);
    }

    /// <summary>
    /// Derives one key as RFC 4253 section 7.2 defines it.
    /// </summary>
    internal byte[] DeriveKey(char letter, int length, byte[] sessionIdentifier)
    {
        byte[] prefix = Join(Mpint(SharedSecret), ExchangeHash);
        byte[] key = CryptographicOperations.HashData(Hash, Join(prefix, Encoding.ASCII.GetBytes([letter]), sessionIdentifier));
        while (key.Length < length)
        {
            key = [.. key, .. CryptographicOperations.HashData(Hash, Join(prefix, key))];
        }

        return key[..length];
    }
}
