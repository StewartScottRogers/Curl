using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// A user's private key, read from a <c>--key</c> file, which signs the <c>publickey</c>
/// authentication request (RFC 4252 section 7). One subclass per key type.
/// </summary>
internal abstract class SshPrivateKey
{
    /// <summary>
    /// Gets the key type, such as <c>ssh-rsa</c>, <c>ecdsa-sha2-nistp256</c> or
    /// <c>ssh-dss</c>: the first field of <see cref="PublicKeyBlob" />.
    /// </summary>
    internal abstract string KeyType { get; }

    /// <summary>
    /// Gets the public half as an SSH public key blob, as libssh2 derives it from the private
    /// key when no <c>--pubkey</c> is given.
    /// </summary>
    internal abstract byte[] PublicKeyBlob { get; }

    /// <summary>
    /// Gets the public half with its key type.
    /// </summary>
    internal SshPublicKey PublicKey => new(KeyType, PublicKeyBlob);

    /// <summary>
    /// Signs <paramref name="data" /> and wraps the signature in an SSH signature blob: the
    /// algorithm's name, then the signature in that algorithm's encoding.
    /// </summary>
    /// <param name="algorithm">
    /// The signature algorithm: <see cref="KeyType" />, or for an RSA key one of
    /// <c>rsa-sha2-512</c>, <c>rsa-sha2-256</c> and <c>ssh-rsa</c>.
    /// </param>
    /// <param name="data">The bytes to sign.</param>
    /// <returns>The signature blob.</returns>
    internal byte[] Sign(string algorithm, byte[] data)
    {
        SshWireWriter blob = new();
        blob.WriteString(System.Text.Encoding.ASCII.GetBytes(algorithm));
        blob.WriteString(SignRaw(algorithm, data));
        return blob.ToArray();
    }

    /// <summary>
    /// Signs <paramref name="data" /> with <paramref name="algorithm" />, returning the
    /// signature in that algorithm's SSH encoding without the blob's name.
    /// </summary>
    /// <param name="algorithm">The signature algorithm.</param>
    /// <param name="data">The bytes to sign.</param>
    /// <returns>The encoded signature.</returns>
    private protected abstract byte[] SignRaw(string algorithm, byte[] data);
}
