using System.Security.Cryptography;
using System.Text;
using Curl.Cryptography;
using Curl.Protocol.Ssh.KeyExchange;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// The server's half of one key exchange, computed from the RFCs' definitions by hand: the
/// messages the server sends after its <c>KEXINIT</c>, the messages the client must send,
/// and the shared secret and exchange hash both sides must reach.
/// </summary>
/// <param name="ServerPayloads">What the server sends, in order, before its <c>NEWKEYS</c>.</param>
/// <param name="ClientPayloads">What the client must send, in order, before its <c>NEWKEYS</c>.</param>
/// <param name="EncodedSharedSecret">K as H hashes it: an <c>mpint</c>, or a <c>string</c> for the hybrid methods.</param>
/// <param name="ExchangeHash">H.</param>
/// <param name="Hash">The method's hash.</param>
internal sealed record TestKeyExchangeServer(
    List<byte[]> ServerPayloads,
    List<byte[]> ClientPayloads,
    byte[] EncodedSharedSecret,
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
        byte[] serverKexInit,
        SshGroupExchangeSizes? groupExchangeSizes = null,
        FiniteFieldDiffieHellmanGroup? exchangedGroup = null,
        string serverIdentification = ServerIdentification)
    {
        byte[] common = Join(Name(ClientIdentification), Name(serverIdentification), String(clientKexInit), String(serverKexInit), String(hostKey.Blob));
        return method switch
        {
            "mlkem768x25519-sha256" => Hybrid(MlKemShares(MlKemParameterSet.MlKem768), X25519Shares(), HashAlgorithmName.SHA256, hostKey, common),
            "mlkem768nistp256-sha256" => Hybrid(MlKemShares(MlKemParameterSet.MlKem768), EcdhShares(ECCurve.NamedCurves.nistP256, keys), HashAlgorithmName.SHA256, hostKey, common),
            "mlkem1024nistp384-sha384" => Hybrid(MlKemShares(MlKemParameterSet.MlKem1024), EcdhShares(ECCurve.NamedCurves.nistP384, keys), HashAlgorithmName.SHA384, hostKey, common),
            "sntrup761x25519-sha512" or "sntrup761x25519-sha512@openssh.com" => Hybrid(Sntrup761Shares(), X25519Shares(), HashAlgorithmName.SHA512, hostKey, common),
            "curve25519-sha256" or "curve25519-sha256@libssh.org" => Curve25519(hostKey, common),
            "ecdh-sha2-nistp256" => Ecdh(ECCurve.NamedCurves.nistP256, HashAlgorithmName.SHA256, hostKey, keys, common),
            "ecdh-sha2-nistp384" => Ecdh(ECCurve.NamedCurves.nistP384, HashAlgorithmName.SHA384, hostKey, keys, common),
            "ecdh-sha2-nistp521" => Ecdh(ECCurve.NamedCurves.nistP521, HashAlgorithmName.SHA512, hostKey, keys, common),
            "diffie-hellman-group1-sha1" => FiniteField(FiniteFieldDiffieHellmanGroup.Group2, HashAlgorithmName.SHA1, hostKey, common),
            "diffie-hellman-group14-sha1" => FiniteField(FiniteFieldDiffieHellmanGroup.Group14, HashAlgorithmName.SHA1, hostKey, common),
            "diffie-hellman-group14-sha256" => FiniteField(FiniteFieldDiffieHellmanGroup.Group14, HashAlgorithmName.SHA256, hostKey, common),
            "diffie-hellman-group16-sha512" => FiniteField(FiniteFieldDiffieHellmanGroup.Group16, HashAlgorithmName.SHA512, hostKey, common),
            "diffie-hellman-group18-sha512" => FiniteField(FiniteFieldDiffieHellmanGroup.Group18, HashAlgorithmName.SHA512, hostKey, common),
            "diffie-hellman-group-exchange-sha1" => GroupExchange(HashAlgorithmName.SHA1, groupExchangeSizes, exchangedGroup, hostKey, common),
            _ => GroupExchange(HashAlgorithmName.SHA256, groupExchangeSizes, exchangedGroup, hostKey, common),
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
            Mpint(k),
            h,
            hash);
    }

    // RFC 8731 section 3: Q_C and Q_S as strings, and the 32 X25519 bytes as K read
    // big-endian, straight into an mpint.
    private static TestKeyExchangeServer Curve25519(TestHostKey hostKey, byte[] common)
    {
        byte[] clientPublic = new byte[X25519.KeySize];
        byte[] serverPublic = new byte[X25519.KeySize];
        byte[] k = new byte[X25519.KeySize];
        X25519.ComputePublicKey(TestEphemeralKeys.ClientX25519, clientPublic);
        X25519.ComputePublicKey(TestEphemeralKeys.ServerX25519, serverPublic);
        X25519.TryComputeSharedSecret(TestEphemeralKeys.ServerX25519, clientPublic, k);
        byte[] h = SHA256.HashData(Join(common, String(clientPublic), String(serverPublic), Mpint(k)));
        return new TestKeyExchangeServer(
            [EcdhReply(hostKey.Blob, serverPublic, hostKey.Sign(h))],
            [[30, .. String(clientPublic)]],
            Mpint(k),
            h,
            HashAlgorithmName.SHA256);
    }

    // draft-ietf-sshm-mlkem-hybrid-kex and draft-josefsson-ntruprime-ssh: the KEM's share
    // before the classical one each way, K = HASH(KEM secret || classical secret) as a
    // string, not an mpint.
    private static TestKeyExchangeServer Hybrid(Shares keyEncapsulation, Shares keyAgreement, HashAlgorithmName hash, TestHostKey hostKey, byte[] common)
    {
        byte[] clientShare = Join(keyEncapsulation.Client, keyAgreement.Client);
        byte[] serverShare = Join(keyEncapsulation.Server, keyAgreement.Server);
        byte[] k = CryptographicOperations.HashData(hash, Join(keyEncapsulation.Secret, keyAgreement.Secret));
        byte[] h = CryptographicOperations.HashData(hash, Join(common, String(clientShare), String(serverShare), String(k)));
        return new TestKeyExchangeServer(
            [EcdhReply(hostKey.Blob, serverShare, hostKey.Sign(h))],
            [[30, .. String(clientShare)]],
            String(k),
            h,
            hash);
    }

    private static Shares MlKemShares(MlKemParameterSet parameterSet)
    {
        using MlKem clientKey = MlKem.GenerateKey(parameterSet, TestEphemeralKeys.ClientMlKemSeedD, TestEphemeralKeys.ClientMlKemSeedZ);
        byte[] encapsulationKey = new byte[MlKem.GetEncapsulationKeySize(parameterSet)];
        byte[] ciphertext = new byte[MlKem.GetCiphertextSize(parameterSet)];
        byte[] secret = new byte[MlKem.SharedSecretSize];
        clientKey.ExportEncapsulationKey(encapsulationKey);
        MlKem.TryEncapsulate(parameterSet, encapsulationKey, TestEphemeralKeys.ServerMlKemMessage, ciphertext, secret);
        return new Shares(encapsulationKey, ciphertext, secret);
    }

    private static Shares Sntrup761Shares()
    {
        byte[] ciphertext = new byte[Sntrup761.CiphertextSize];
        byte[] secret = new byte[Sntrup761.SharedSecretSize];
        Sntrup761.Encapsulate(TestEphemeralKeys.ClientSntrup761.PublicKey, TestEphemeralKeys.ServerSntrup761Random, ciphertext, secret);
        return new Shares(TestEphemeralKeys.ClientSntrup761.PublicKey, ciphertext, secret);
    }

    private static Shares X25519Shares()
    {
        byte[] clientPublic = new byte[X25519.KeySize];
        byte[] serverPublic = new byte[X25519.KeySize];
        byte[] secret = new byte[X25519.KeySize];
        X25519.ComputePublicKey(TestEphemeralKeys.ClientX25519, clientPublic);
        X25519.ComputePublicKey(TestEphemeralKeys.ServerX25519, serverPublic);
        X25519.TryComputeSharedSecret(TestEphemeralKeys.ServerX25519, clientPublic, secret);
        return new Shares(clientPublic, serverPublic, secret);
    }

    // The classical secret is the x-coordinate of the product, fixed-length, not an mpint.
    private static Shares EcdhShares(ECCurve curve, TestEphemeralKeys keys)
    {
        ECParameters clientKey = keys.Client(curve);
        ECParameters serverKey = keys.Server(curve);
        using ECDiffieHellman server = ECDiffieHellman.Create(serverKey);
        using ECDiffieHellman clientPublic = ECDiffieHellman.Create(new ECParameters { Curve = curve, Q = clientKey.Q });
        return new Shares(
            [0x04, .. clientKey.Q.X!, .. clientKey.Q.Y!],
            [0x04, .. serverKey.Q.X!, .. serverKey.Q.Y!],
            server.DeriveRawSecretAgreement(clientPublic.PublicKey));
    }

    private static TestKeyExchangeServer FiniteField(FiniteFieldDiffieHellmanGroup group, HashAlgorithmName hash, TestHostKey hostKey, byte[] common)
    {
        (byte[] e, byte[] f, byte[] k) = Round(group);
        byte[] h = CryptographicOperations.HashData(hash, Join(common, Mpint(e), Mpint(f), Mpint(k)));
        return new TestKeyExchangeServer(
            [FiniteFieldReply(31, hostKey.Blob, f, hostKey.Sign(h))],
            [[30, .. Mpint(e)]],
            Mpint(k),
            h,
            hash);
    }

    private static TestKeyExchangeServer GroupExchange(
        HashAlgorithmName hash,
        SshGroupExchangeSizes? requested,
        FiniteFieldDiffieHellmanGroup? exchangedGroup,
        TestHostKey hostKey,
        byte[] common)
    {
        FiniteFieldDiffieHellmanGroup group = exchangedGroup ?? FiniteFieldDiffieHellmanGroup.Group14;
        SshGroupExchangeSizes sizesAsked = requested ?? SshGroupExchangeSizes.OpenSslReference;
        (byte[] e, byte[] f, byte[] k) = Round(group);
        byte[] sizes = Join(UInt32(sizesAsked.MinimumBits), UInt32(sizesAsked.PreferredBits), UInt32(sizesAsked.MaximumBits));
        byte[] p = group.Prime.ToArray();
        byte[] g = group.Generator.ToArray();
        byte[] h = CryptographicOperations.HashData(hash, Join(common, sizes, Mpint(p), Mpint(g), Mpint(e), Mpint(f), Mpint(k)));
        return new TestKeyExchangeServer(
            [[31, .. Mpint(p), .. Mpint(g)], FiniteFieldReply(33, hostKey.Blob, f, hostKey.Sign(h))],
            [[34, .. sizes], [32, .. Mpint(e)]],
            Mpint(k),
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
    /// The library's derivation of this exchange's keys, for building the server's packet
    /// protection; <see cref="DeriveKey" /> checks the derivation itself independently.
    /// </summary>
    internal SshKeyDerivation Keys(byte[] sessionIdentifier) => new(Hash, EncodedSharedSecret, ExchangeHash, sessionIdentifier);

    /// <summary>
    /// Derives one key as RFC 4253 section 7.2 defines it.
    /// </summary>
    internal byte[] DeriveKey(char letter, int length, byte[] sessionIdentifier)
    {
        byte[] prefix = Join(EncodedSharedSecret, ExchangeHash);
        byte[] key = CryptographicOperations.HashData(Hash, Join(prefix, Encoding.ASCII.GetBytes([letter]), sessionIdentifier));
        while (key.Length < length)
        {
            key = [.. key, .. CryptographicOperations.HashData(Hash, Join(prefix, key))];
        }

        return key[..length];
    }

    /// <summary>One component of a hybrid exchange: what each side sends and the secret it gives.</summary>
    private sealed record Shares(byte[] Client, byte[] Server, byte[] Secret);
}
