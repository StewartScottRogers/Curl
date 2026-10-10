using System.Text;
using Curl.Cryptography;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Conformance.SshServer;

/// <summary>
/// The one account the conformance SSH server lets in (BL-1916): <see cref="User" /> by
/// <see cref="Password" />, or by the fixed <c>ssh-ed25519</c> client key whose files upstream's
/// cases name as <c>%LOGDIR/server/curl_client_key</c> and <c>curl_client_key.pub</c>, or by its
/// RSA twin <see cref="SshServerRsaClientKey" />. The Ed25519 key is RFC 8032 section 7.1's
/// second key, so the files are the same on every run and platform.
/// </summary>
public static class SshServerClientAccount
{
    /// <summary>The user name the server accepts, the case runner's <c>%USER</c>.</summary>
    public const string User = "curltest";

    /// <summary>The password the server accepts for <see cref="User" /> under the <c>password</c> method.</summary>
    public const string Password = "curltest-password";

    /// <summary>The client key's algorithm.</summary>
    internal const string KeyAlgorithm = "ssh-ed25519";

    private const string KeyComment = "curltest@conformance";

    private const int Base64LineLength = 70;

    private static readonly byte[] PrivateKey = Convert.FromHexString("4CCD089B28FF96DA9DB6C346EC114E0F5B8A319F35ABA624DA8CF6ED4FB8A6FB");

    private static readonly byte[] PublicKey = ComputePublicKey();

    /// <summary>Gets the client key's public key blob: the algorithm's name, then the 32-byte key, both as strings.</summary>
    internal static byte[] PublicKeyBlob { get; } = CreatePublicKeyBlob();

    /// <summary>
    /// Creates the private key file, unencrypted in OpenSSH's <c>openssh-key-v1</c> format, as
    /// <c>ssh-keygen</c> writes the file upstream's <c>sshserver.pl</c> makes.
    /// </summary>
    /// <returns>The file's bytes.</returns>
    public static byte[] CreatePrivateKeyFile()
    {
        SshWireWriter file = new();
        file.WriteBytes("openssh-key-v1\0"u8);
        file.WriteString("none"u8);
        file.WriteString("none"u8);
        file.WriteString([]);
        file.WriteUInt32(1);
        file.WriteString(PublicKeyBlob);
        file.WriteString(CreatePrivateSection());
        string base64 = Convert.ToBase64String(file.ToArray());
        StringBuilder text = new("-----BEGIN OPENSSH PRIVATE KEY-----\n");
        for (int start = 0; start < base64.Length; start += Base64LineLength)
        {
            text.Append(base64.AsSpan(start, Math.Min(Base64LineLength, base64.Length - start))).Append('\n');
        }

        return Encoding.ASCII.GetBytes(text.Append("-----END OPENSSH PRIVATE KEY-----\n").ToString());
    }

    /// <summary>Creates the public key file: one OpenSSH <c>authorized_keys</c> line.</summary>
    /// <returns>The file's bytes.</returns>
    public static byte[] CreatePublicKeyFile() =>
        Encoding.ASCII.GetBytes($"{KeyAlgorithm} {Convert.ToBase64String(PublicKeyBlob)} {KeyComment}\n");

    /// <summary>
    /// Gets whether a <c>publickey</c> request names one of this account's keys: the
    /// <c>ssh-ed25519</c> key, or the <see cref="SshServerRsaClientKey" /> under any of its
    /// signature algorithms, for curl's WinCNG build, which reads no Ed25519 key.
    /// </summary>
    /// <param name="algorithm">The algorithm the request names.</param>
    /// <param name="blob">The public key blob the request carries.</param>
    /// <returns>Whether the server accepts the key.</returns>
    internal static bool AcceptsKey(string algorithm, ReadOnlySpan<byte> blob) => algorithm == KeyAlgorithm
        ? blob.SequenceEqual(PublicKeyBlob)
        : SshServerRsaClientKey.IsSignatureAlgorithm(algorithm) && blob.SequenceEqual(SshServerRsaClientKey.PublicKeyBlob);

    /// <summary>Checks a <c>publickey</c> signature the client made with one of this account's keys.</summary>
    /// <param name="algorithm">The algorithm the request names, one <see cref="AcceptsKey" /> accepted.</param>
    /// <param name="signedData">The data the client signed (RFC 4252 section 7).</param>
    /// <param name="signatureBlob">The signature blob: the algorithm's name, then the signature, both as strings.</param>
    /// <returns>Whether the signature is the key's over the data under that algorithm.</returns>
    internal static bool Verifies(string algorithm, ReadOnlySpan<byte> signedData, ReadOnlyMemory<byte> signatureBlob)
    {
        SshWireReader reader = new(signatureBlob);
        if (reader.ReadName() != algorithm)
        {
            return false;
        }

        ReadOnlyMemory<byte> signature = reader.ReadString();
        return algorithm == KeyAlgorithm
            ? signature.Length == Ed25519.SignatureSize && Ed25519.Verify(PublicKey, signedData, signature.Span)
            : SshServerRsaClientKey.Verifies(algorithm, signedData, signature.Span);
    }

    /// <summary>Signs data with this account's Ed25519 key, as the client would.</summary>
    /// <param name="data">The data to sign.</param>
    /// <returns>The signature blob.</returns>
    internal static byte[] Sign(ReadOnlySpan<byte> data)
    {
        byte[] signature = new byte[Ed25519.SignatureSize];
        Ed25519.Sign(PrivateKey, data, signature);
        return Encode(signature);
    }

    // openssh-key-v1's private section: two equal check words, the key, its comment, then padding 1, 2, 3, ... to a multiple of 8.
    private static byte[] CreatePrivateSection()
    {
        SshWireWriter section = new();
        section.WriteUInt32(0x43555254);
        section.WriteUInt32(0x43555254);
        section.WriteString(Encoding.ASCII.GetBytes(KeyAlgorithm));
        section.WriteString(PublicKey);
        section.WriteString([.. PrivateKey, .. PublicKey]);
        section.WriteString(Encoding.ASCII.GetBytes(KeyComment));
        byte[] unpadded = section.ToArray();
        int padding = (8 - (unpadded.Length % 8)) % 8;
        return [.. unpadded, .. Enumerable.Range(1, padding).Select(value => (byte)value)];
    }

    private static byte[] ComputePublicKey()
    {
        byte[] publicKey = new byte[Ed25519.PublicKeySize];
        Ed25519.ComputePublicKey(PrivateKey, publicKey);
        return publicKey;
    }

    private static byte[] CreatePublicKeyBlob() => Encode(PublicKey);

    private static byte[] Encode(ReadOnlySpan<byte> key)
    {
        SshWireWriter writer = new();
        writer.WriteString(Encoding.ASCII.GetBytes(KeyAlgorithm));
        writer.WriteString(key);
        return writer.ToArray();
    }
}
