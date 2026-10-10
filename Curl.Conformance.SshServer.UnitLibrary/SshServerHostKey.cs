using System.Security.Cryptography;
using System.Text;
using Curl.Cryptography;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Conformance.SshServer;

/// <summary>
/// The server's fixed <c>ssh-ed25519</c> host key: RFC 8032 section 7.1's first Ed25519 key,
/// as the SSH client's tests use, so its blob and fingerprints are the same on every run and
/// platform and an upstream case's <c>--hostpubmd5</c> or <c>--hostpubsha256</c> can name them.
/// </summary>
internal static class SshServerHostKey
{
    /// <summary>The host-key algorithm's name.</summary>
    internal const string Algorithm = "ssh-ed25519";

    /// <summary>
    /// The MD5 of <see cref="Blob" /> as 32 lowercase hex digits, the form
    /// <c>--hostpubmd5</c> takes.
    /// </summary>
    internal const string Md5Fingerprint = "cf07be9d68ae65546da093c36fbd0d82";

    /// <summary>
    /// The SHA-256 of <see cref="Blob" /> in base64 without padding, the form
    /// <c>--hostpubsha256</c> takes.
    /// </summary>
    internal const string Sha256Fingerprint = "bbXpuKG6zhzdmnxq256TlqzFBzRl2f6OOg722cYNbU8";

    private static readonly byte[] PrivateKey = Convert.FromHexString("9D61B19DEFFD5A60BA844AF492EC2CC44449C5697B326919703BAC031CAE7F60");

    /// <summary>Gets the public key blob: the algorithm's name, then the 32-byte public key, both as strings.</summary>
    internal static byte[] Blob { get; } = CreateBlob();

    /// <summary>Computes the MD5 fingerprint of <see cref="Blob" />, as <see cref="Md5Fingerprint" /> pins it.</summary>
    /// <returns>32 lowercase hex digits.</returns>
    internal static string ComputeMd5Fingerprint() => Convert.ToHexStringLower(MD5.HashData(Blob));

    /// <summary>Computes the SHA-256 fingerprint of <see cref="Blob" />, as <see cref="Sha256Fingerprint" /> pins it.</summary>
    /// <returns>Base64 without its trailing <c>=</c>.</returns>
    internal static string ComputeSha256Fingerprint() => Convert.ToBase64String(SHA256.HashData(Blob)).TrimEnd('=');

    /// <summary>
    /// Signs an exchange hash: the <c>ssh-ed25519</c> signature blob (RFC 8709 section 6).
    /// </summary>
    /// <param name="exchangeHash">H.</param>
    /// <returns>The algorithm's name, then the 64-byte signature, both as strings.</returns>
    internal static byte[] Sign(ReadOnlySpan<byte> exchangeHash)
    {
        byte[] signature = new byte[Ed25519.SignatureSize];
        Ed25519.Sign(PrivateKey, exchangeHash, signature);
        return Encode(signature);
    }

    private static byte[] CreateBlob()
    {
        byte[] publicKey = new byte[Ed25519.PublicKeySize];
        Ed25519.ComputePublicKey(PrivateKey, publicKey);
        return Encode(publicKey);
    }

    private static byte[] Encode(ReadOnlySpan<byte> key)
    {
        SshWireWriter writer = new();
        writer.WriteString(Encoding.ASCII.GetBytes(Algorithm));
        writer.WriteString(key);
        return writer.ToArray();
    }
}
