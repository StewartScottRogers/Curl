using System.Buffers.Binary;
using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// A host key the scripted server signs the exchange hash with: its blob <c>K_S</c> and the
/// signature blob it sends, built by hand from the BCL and <see cref="DsaSignature" />.
/// </summary>
/// <param name="Algorithm">The signature algorithm the key signs as.</param>
/// <param name="Blob">The host key blob.</param>
/// <param name="Sign">Signs H, returning the signature blob.</param>
internal sealed record TestHostKey(string Algorithm, byte[] Blob, Func<byte[], byte[]> Sign)
{
    /// <summary>p of RFC 6979 appendix A.2.1's 1024-bit DSA key.</summary>
    internal const string DsaPrime =
        "86F5CA03DCFEB225063FF830A0C769B9DD9D6153AD91D7CE27F787C43278B447E6533B86B18BED6E8A48B784A14C252C5BE0DBF60B86D6385BD2F12FB763ED88"
        + "73ABFD3F5BA2E0A8C0A59082EAC056935E529DAF7C610467899C77ADEDFC846C881870B7B19B2B58F9BE0521A17002E3BDD6B86685EE90B3D9A1B02B782B1779";

    /// <summary>q of the RFC 6979 key.</summary>
    internal const string DsaSubprime = "996F967F6C8E388D9E28D01E205FBA957A5698B1";

    /// <summary>g of the RFC 6979 key.</summary>
    internal const string DsaGenerator =
        "07B0F92546150B62514BB771E2A0C0CE387F03BDA6C56B505209FF25FD3C133D89BBCD97E904E09114D9A7DEFDEADFC9078EA544D2E401AEECC40BB9FBBF78FD"
        + "87995A10A1C27CB7789B594BA7EFB5C4326A9FE59A070E136DB77175464ADCA417BE5DCE2F40D10A46A3A3943F26AB7FD9C0398FF8C76EE0A56826A8A88F1DBD";

    /// <summary>x of the RFC 6979 key.</summary>
    internal const string DsaPrivateKey = "411602CB19A6CCC34494D79D98EF1E7ED5AF25F7";

    /// <summary>y of the RFC 6979 key.</summary>
    internal const string DsaPublicKey =
        "5DF5E01DED31D0297E274E1691C192FE5868FEF9E19A84776454B100CF16F65392195A38B90523E2542EE61871C0440CB87C322FC4B4D2EC5E1E7EC766E1BE8D"
        + "4CE935437DC11C3C8FD426338933EBFE739CB3465F4D3668C5E473508253B1E682F65CBDC4FAE93C2EA212390E54905A86E2223170B44EAA7DA5DD9FFCFB7F3B";

    /// <summary>The private key of RFC 8032 section 7.1's first Ed25519 test vector.</summary>
    internal const string Ed25519PrivateKey = "9D61B19DEFFD5A60BA844AF492EC2CC44449C5697B326919703BAC031CAE7F60";

    /// <summary>Gets a fixed P-256 host key.</summary>
    internal static ECParameters FixedNistP256 { get; } = new()
    {
        Curve = ECCurve.NamedCurves.nistP256,
        D = Convert.FromHexString("0D25EBCE4C1878F611322C47C2D0943E6551F60138B9C32996E086114783B00E"),
        Q = new ECPoint
        {
            X = Convert.FromHexString("98A2CCA52C7E6108C0B8EA504BB0BDAC139321A2BCB7EDAEC8C448759A727EFD"),
            Y = Convert.FromHexString("3DCDF302893BCD925229529F23ADE7A98A0CEAFEB9100C66F6B0151B227A78C4"),
        },
    };

    // The flags byte (user present) and counter every test security-key signature carries.
    private static readonly byte[] SecurityKeyFlagsAndCounter = [0x01, 0, 0, 0, 7];

    // Generated once and shared as parameters: a platform RSA object generates its key on
    // first use, so one object shared by parallel tests could end up with two keys.
    private static readonly Lazy<RSAParameters> SharedRsaKey = new(() =>
    {
        using RSA rsa = RSA.Create(2048);
        return rsa.ExportParameters(true);
    });

    /// <summary>An RSA host key signing as <paramref name="algorithm" />.</summary>
    internal static TestHostKey Rsa(string algorithm, HashAlgorithmName hash)
    {
        RSA rsa = RSA.Create(SharedRsaKey.Value);
        RSAParameters parameters = rsa.ExportParameters(false);
        byte[] blob = SshTestEncoding.Join(SshTestEncoding.Name("ssh-rsa"), SshTestEncoding.Mpint(parameters.Exponent!), SshTestEncoding.Mpint(parameters.Modulus!));
        return new TestHostKey(
            algorithm,
            blob,
            h => SshTestEncoding.Join(SshTestEncoding.Name(algorithm), SshTestEncoding.String(rsa.SignData(h, hash, RSASignaturePadding.Pkcs1))));
    }

    /// <summary>An ECDSA host key on the curve SSH calls <paramref name="curveIdentifier" />.</summary>
    internal static TestHostKey Ecdsa(string curveIdentifier, ECParameters? fixedKey = null)
    {
        (ECCurve curve, HashAlgorithmName hash) = curveIdentifier switch
        {
            "nistp256" => (ECCurve.NamedCurves.nistP256, HashAlgorithmName.SHA256),
            "nistp384" => (ECCurve.NamedCurves.nistP384, HashAlgorithmName.SHA384),
            _ => (ECCurve.NamedCurves.nistP521, HashAlgorithmName.SHA512),
        };
        ECDsa ecdsa = fixedKey is { } parameters ? ECDsa.Create(parameters) : ECDsa.Create(curve);
        ECPoint q = ecdsa.ExportParameters(false).Q;
        string name = "ecdsa-sha2-" + curveIdentifier;
        byte[] blob = SshTestEncoding.Join(SshTestEncoding.Name(name), SshTestEncoding.Name(curveIdentifier), SshTestEncoding.String([0x04, .. q.X!, .. q.Y!]));
        return new TestHostKey(name, blob, h =>
        {
            byte[] signature = ecdsa.SignData(h, hash);
            int half = signature.Length / 2;
            byte[] values = SshTestEncoding.Join(SshTestEncoding.Mpint(signature[..half]), SshTestEncoding.Mpint(signature[half..]));
            return SshTestEncoding.Join(SshTestEncoding.Name(name), SshTestEncoding.String(values));
        });
    }

    /// <summary>The RFC 6979 1024-bit DSA key as an <c>ssh-dss</c> host key; its signatures are deterministic.</summary>
    internal static TestHostKey Dsa()
    {
        byte[] p = Convert.FromHexString(DsaPrime);
        byte[] q = Convert.FromHexString(DsaSubprime);
        byte[] g = Convert.FromHexString(DsaGenerator);
        byte[] blob = SshTestEncoding.Join(
            SshTestEncoding.Name("ssh-dss"),
            SshTestEncoding.Mpint(p),
            SshTestEncoding.Mpint(q),
            SshTestEncoding.Mpint(g),
            SshTestEncoding.Mpint(Convert.FromHexString(DsaPublicKey)));
        return new TestHostKey("ssh-dss", blob, h =>
        {
            using DsaSignature dsa = new(p, q, g, Convert.FromHexString(DsaPrivateKey));
            byte[] signature = new byte[dsa.SignatureLength];
            dsa.SignHash(SHA1.HashData(h), HashAlgorithmName.SHA1, signature);
            return SshTestEncoding.Join(SshTestEncoding.Name("ssh-dss"), SshTestEncoding.String(signature));
        });
    }

    /// <summary>RFC 8032 section 7.1's first Ed25519 key as an <c>ssh-ed25519</c> host key; its signatures are deterministic.</summary>
    internal static TestHostKey Ed25519()
    {
        byte[] privateKey = Convert.FromHexString(Ed25519PrivateKey);
        byte[] publicKey = new byte[Cryptography.Ed25519.PublicKeySize];
        Cryptography.Ed25519.ComputePublicKey(privateKey, publicKey);
        byte[] blob = SshTestEncoding.Join(SshTestEncoding.Name("ssh-ed25519"), SshTestEncoding.String(publicKey));
        return new TestHostKey("ssh-ed25519", blob, h =>
        {
            byte[] signature = new byte[Cryptography.Ed25519.SignatureSize];
            Cryptography.Ed25519.Sign(privateKey, h, signature);
            return SshTestEncoding.Join(SshTestEncoding.Name("ssh-ed25519"), SshTestEncoding.String(signature));
        });
    }

    /// <summary>
    /// The FIDO application string OpenSSH gives a security key it makes (<c>PROTOCOL.u2f</c>).
    /// </summary>
    internal static byte[] SecurityKeyApplication { get; } = "ssh:"u8.ToArray();

    /// <summary>
    /// The fixed P-256 key as an <c>sk-ecdsa-sha2-nistp256@openssh.com</c> host key: it signs
    /// SHA256(application) || flags || counter || SHA256(H) and appends the flags and counter.
    /// </summary>
    internal static TestHostKey SecurityKeyEcdsa()
    {
        const string name = "sk-ecdsa-sha2-nistp256@openssh.com";
        ECDsa ecdsa = ECDsa.Create(FixedNistP256);
        byte[] point = [0x04, .. FixedNistP256.Q.X!, .. FixedNistP256.Q.Y!];
        byte[] blob = SshTestEncoding.Join(SshTestEncoding.Name(name), SshTestEncoding.Name("nistp256"), SshTestEncoding.String(point), SshTestEncoding.String(SecurityKeyApplication));
        return new TestHostKey(name, blob, h =>
        {
            byte[] signature = ecdsa.SignData(SecurityKeySignedData(h), HashAlgorithmName.SHA256);
            int half = signature.Length / 2;
            byte[] values = SshTestEncoding.Join(SshTestEncoding.Mpint(signature[..half]), SshTestEncoding.Mpint(signature[half..]));
            return SshTestEncoding.Join(SshTestEncoding.Name(name), SshTestEncoding.String(values), SecurityKeyFlagsAndCounter);
        });
    }

    /// <summary>
    /// RFC 8032's first Ed25519 key as an <c>sk-ssh-ed25519@openssh.com</c> host key: it signs
    /// SHA256(application) || flags || counter || SHA256(H) and appends the flags and counter.
    /// </summary>
    internal static TestHostKey SecurityKeyEd25519()
    {
        const string name = "sk-ssh-ed25519@openssh.com";
        byte[] privateKey = Convert.FromHexString(Ed25519PrivateKey);
        byte[] publicKey = new byte[Cryptography.Ed25519.PublicKeySize];
        Cryptography.Ed25519.ComputePublicKey(privateKey, publicKey);
        byte[] blob = SshTestEncoding.Join(SshTestEncoding.Name(name), SshTestEncoding.String(publicKey), SshTestEncoding.String(SecurityKeyApplication));
        return new TestHostKey(name, blob, h =>
        {
            byte[] signature = new byte[Cryptography.Ed25519.SignatureSize];
            Cryptography.Ed25519.Sign(privateKey, SecurityKeySignedData(h), signature);
            return SshTestEncoding.Join(SshTestEncoding.Name(name), SshTestEncoding.String(signature), SecurityKeyFlagsAndCounter);
        });
    }

    /// <summary>
    /// An OpenSSH host certificate (<c>PROTOCOL.certkeys</c>) of <paramref name="certified" />'s
    /// key: the certificate name, a nonce, the key's fields, then <paramref name="body" /> (by
    /// default <see cref="CertificateBody" /> for <c>localhost</c>, valid for ever and signed by
    /// an Ed25519 CA). It signs H as the certified key does.
    /// </summary>
    internal static TestHostKey Certificate(string certificateName, TestHostKey certified, byte[]? body = null)
    {
        int keyTypeLength = BinaryPrimitives.ReadInt32BigEndian(certified.Blob);
        byte[] keyFields = certified.Blob[(4 + keyTypeLength)..];
        byte[] signed = SshTestEncoding.Join(SshTestEncoding.Name(certificateName), SshTestEncoding.String(new byte[32]), keyFields);
        return new TestHostKey(certificateName, [.. signed, .. body ?? CertificateBody(signed, ["localhost"], validBefore: ulong.MaxValue)], certified.Sign);
    }

    /// <summary>
    /// A host certificate's fields after its key: serial 1, type 2 (host), key ID, the
    /// principals, valid from 0 to <paramref name="validBefore" />, no options or extensions,
    /// then an Ed25519 CA key and its signature over everything before it.
    /// </summary>
    internal static byte[] CertificateBody(byte[] certificateStart, string[] principals, ulong validBefore)
    {
        byte[] caPrivateKey = Convert.FromHexString(Ed25519PrivateKey);
        byte[] caPublicKey = new byte[Cryptography.Ed25519.PublicKeySize];
        Cryptography.Ed25519.ComputePublicKey(caPrivateKey, caPublicKey);
        byte[] validBeforeBytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(validBeforeBytes, validBefore);
        byte[] fields = SshTestEncoding.Join(
            [0, 0, 0, 0, 0, 0, 0, 1],
            SshTestEncoding.UInt32(2),
            SshTestEncoding.Name("host key"),
            SshTestEncoding.String(SshTestEncoding.Join([.. principals.Select(SshTestEncoding.Name)])),
            new byte[8],
            validBeforeBytes,
            SshTestEncoding.String([]),
            SshTestEncoding.String([]),
            SshTestEncoding.String([]),
            SshTestEncoding.String(SshTestEncoding.Join(SshTestEncoding.Name("ssh-ed25519"), SshTestEncoding.String(caPublicKey))));
        byte[] signature = new byte[Cryptography.Ed25519.SignatureSize];
        Cryptography.Ed25519.Sign(caPrivateKey, [.. certificateStart, .. fields], signature);
        return [.. fields, .. SshTestEncoding.String(SshTestEncoding.Join(SshTestEncoding.Name("ssh-ed25519"), SshTestEncoding.String(signature)))];
    }

    /// <summary>The host key for an algorithm name.</summary>
    internal static TestHostKey For(string algorithm) => algorithm switch
    {
        "ecdsa-sha2-nistp256-cert-v01@openssh.com" => Certificate(algorithm, Ecdsa("nistp256")),
        "ecdsa-sha2-nistp384-cert-v01@openssh.com" => Certificate(algorithm, Ecdsa("nistp384")),
        "ecdsa-sha2-nistp521-cert-v01@openssh.com" => Certificate(algorithm, Ecdsa("nistp521")),
        "rsa-sha2-512-cert-v01@openssh.com" => Certificate(algorithm, Rsa("rsa-sha2-512", HashAlgorithmName.SHA512)),
        "rsa-sha2-256-cert-v01@openssh.com" => Certificate(algorithm, Rsa("rsa-sha2-256", HashAlgorithmName.SHA256)),
        "ssh-rsa-cert-v01@openssh.com" => Certificate(algorithm, Rsa("ssh-rsa", HashAlgorithmName.SHA1)),
        "ssh-ed25519-cert-v01@openssh.com" => Certificate(algorithm, Ed25519()),
        "sk-ecdsa-sha2-nistp256@openssh.com" => SecurityKeyEcdsa(),
        "sk-ssh-ed25519@openssh.com" => SecurityKeyEd25519(),
        "sk-ecdsa-sha2-nistp256-cert-v01@openssh.com" => Certificate(algorithm, SecurityKeyEcdsa()),
        "sk-ssh-ed25519-cert-v01@openssh.com" => Certificate(algorithm, SecurityKeyEd25519()),
        "ecdsa-sha2-nistp256" => Ecdsa("nistp256"),
        "ecdsa-sha2-nistp384" => Ecdsa("nistp384"),
        "ecdsa-sha2-nistp521" => Ecdsa("nistp521"),
        "rsa-sha2-512" => Rsa(algorithm, HashAlgorithmName.SHA512),
        "rsa-sha2-256" => Rsa(algorithm, HashAlgorithmName.SHA256),
        "ssh-rsa" => Rsa(algorithm, HashAlgorithmName.SHA1),
        "ssh-ed25519" => Ed25519(),
        _ => Dsa(),
    };

    private static byte[] SecurityKeySignedData(byte[] h) =>
        [.. SHA256.HashData(SecurityKeyApplication), .. SecurityKeyFlagsAndCounter, .. SHA256.HashData(h)];
}
