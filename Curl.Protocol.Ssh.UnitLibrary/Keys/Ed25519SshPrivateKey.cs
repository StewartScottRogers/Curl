using System.Security.Cryptography;
using System.Text;
using Curl.Cryptography;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// An Ed25519 user key: <c>ssh-ed25519</c> blobs and signatures (RFC 8709 sections 4 and 6),
/// signed with the hand-built <see cref="Ed25519" />.
/// </summary>
internal sealed class Ed25519SshPrivateKey : SshPrivateKey
{
    /// <summary>The key type and signature algorithm, <c>ssh-ed25519</c>.</summary>
    internal const string Ed25519KeyType = "ssh-ed25519";

    private readonly byte[] seed;

    private Ed25519SshPrivateKey(byte[] seed, byte[] publicKey)
    {
        this.seed = seed;
        SshWireWriter blob = new();
        blob.WriteString(Encoding.ASCII.GetBytes(Ed25519KeyType));
        blob.WriteString(publicKey);
        PublicKeyBlob = blob.ToArray();
    }

    /// <inheritdoc />
    internal override string KeyType => Ed25519KeyType;

    /// <inheritdoc />
    internal override byte[] PublicKeyBlob { get; }

    /// <summary>
    /// Builds the key from its 32-byte seed, computing the public key from it.
    /// </summary>
    /// <param name="seed">The private key: RFC 8032's 32-byte seed.</param>
    /// <returns>The key.</returns>
    /// <exception cref="CryptographicException">The seed is not 32 bytes.</exception>
    internal static Ed25519SshPrivateKey FromSeed(ReadOnlySpan<byte> seed)
    {
        if (seed.Length != Ed25519.PrivateKeySize)
        {
            throw new CryptographicException("The Ed25519 private key is not 32 bytes.");
        }

        byte[] publicKey = new byte[Ed25519.PublicKeySize];
        Ed25519.ComputePublicKey(seed, publicKey);
        return new Ed25519SshPrivateKey(seed.ToArray(), publicKey);
    }

    /// <summary>
    /// Builds the key from <c>openssh-key-v1</c>'s two fields: the public key, and the
    /// private key as the seed followed by the public key again.
    /// </summary>
    /// <param name="publicKey">The 32-byte public key.</param>
    /// <param name="privateKey">The 64-byte seed and public key.</param>
    /// <returns>The key.</returns>
    /// <exception cref="CryptographicException">A length is wrong, or the seed does not give the public key.</exception>
    internal static Ed25519SshPrivateKey FromOpenSshFields(ReadOnlySpan<byte> publicKey, ReadOnlySpan<byte> privateKey)
    {
        if (privateKey.Length != Ed25519.PrivateKeySize + Ed25519.PublicKeySize)
        {
            throw new CryptographicException("The Ed25519 private key is not 64 bytes.");
        }

        Ed25519SshPrivateKey key = FromSeed(privateKey[..Ed25519.PrivateKeySize]);
        if (!key.PublicKeyBlob.AsSpan().EndsWith(publicKey) || !publicKey.SequenceEqual(privateKey[Ed25519.PrivateKeySize..]))
        {
            throw new CryptographicException("The Ed25519 public key does not match its private key.");
        }

        return key;
    }

    /// <inheritdoc />
    private protected override byte[] SignRaw(string algorithm, byte[] data)
    {
        byte[] signature = new byte[Ed25519.SignatureSize];
        Ed25519.Sign(seed, data, signature);
        return signature;
    }
}
