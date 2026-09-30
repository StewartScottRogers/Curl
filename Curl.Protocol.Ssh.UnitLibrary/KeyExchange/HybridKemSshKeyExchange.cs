using System.Security.Cryptography;
using Curl.Cryptography;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.KeyExchange;

/// <summary>
/// The hybrid post-quantum methods: a key encapsulation beside a classical key agreement,
/// over the <c>SSH_MSG_KEX_ECDH_*</c> message numbers (draft-ietf-sshm-mlkem-hybrid-kex,
/// draft-josefsson-ntruprime-ssh, OpenSSH's <c>kexmlkem768x25519.c</c> and
/// <c>kexsntrup761x25519.c</c>). The client sends its encapsulation key followed by its
/// classical public key as one <c>string</c>; the server answers with its ciphertext
/// followed by its classical public key; K is HASH(KEM secret || classical secret), and
/// both H and the key derivation take K as a <c>string</c>, not an <c>mpint</c> (ADR-0265).
/// </summary>
/// <param name="createKeyEncapsulation">Creates the post-quantum share.</param>
/// <param name="createKeyAgreement">Creates the classical share.</param>
/// <param name="hashAlgorithm">The method's hash, for K, H and the keys.</param>
/// <param name="keySource">Where the client's ephemeral keys come from.</param>
internal sealed class HybridKemSshKeyExchange(
    Func<ISshEphemeralKeySource, ISshKeyShare> createKeyEncapsulation,
    Func<ISshEphemeralKeySource, ISshKeyShare> createKeyAgreement,
    HashAlgorithmName hashAlgorithm,
    ISshEphemeralKeySource keySource) : ISshKeyExchange
{
    /// <summary>Creates <c>mlkem768x25519-sha256</c>.</summary>
    /// <param name="keySource">Where the client's ephemeral keys come from.</param>
    /// <returns>The method.</returns>
    internal static HybridKemSshKeyExchange MlKem768X25519(ISshEphemeralKeySource keySource) =>
        new(keys => new MlKemSshKeyShare(MlKemParameterSet.MlKem768, keys), keys => new X25519SshKeyShare(keys), HashAlgorithmName.SHA256, keySource);

    /// <summary>Creates <c>mlkem768nistp256-sha256</c>.</summary>
    /// <param name="keySource">Where the client's ephemeral keys come from.</param>
    /// <returns>The method.</returns>
    internal static HybridKemSshKeyExchange MlKem768NistP256(ISshEphemeralKeySource keySource) =>
        new(keys => new MlKemSshKeyShare(MlKemParameterSet.MlKem768, keys), keys => new NistCurveSshKeyShare(SshNistCurve.NistP256, keys), HashAlgorithmName.SHA256, keySource);

    /// <summary>Creates <c>mlkem1024nistp384-sha384</c>.</summary>
    /// <param name="keySource">Where the client's ephemeral keys come from.</param>
    /// <returns>The method.</returns>
    internal static HybridKemSshKeyExchange MlKem1024NistP384(ISshEphemeralKeySource keySource) =>
        new(keys => new MlKemSshKeyShare(MlKemParameterSet.MlKem1024, keys), keys => new NistCurveSshKeyShare(SshNistCurve.NistP384, keys), HashAlgorithmName.SHA384, keySource);

    /// <summary>Creates <c>sntrup761x25519-sha512</c> and its older name <c>sntrup761x25519-sha512@openssh.com</c>.</summary>
    /// <param name="keySource">Where the client's ephemeral keys come from.</param>
    /// <returns>The method.</returns>
    internal static HybridKemSshKeyExchange Sntrup761X25519(ISshEphemeralKeySource keySource) =>
        new(keys => new Sntrup761SshKeyShare(keys), keys => new X25519SshKeyShare(keys), HashAlgorithmName.SHA512, keySource);

    /// <inheritdoc />
    public async ValueTask<SshKeyExchangeOutcome> ExchangeAsync(
        SshKeyExchangeMessages messages,
        SshNegotiatedHandshake handshake,
        CancellationToken cancellationToken)
    {
        using ISshKeyShare keyEncapsulation = createKeyEncapsulation(keySource);
        using ISshKeyShare keyAgreement = createKeyAgreement(keySource);
        byte[] clientShare = [.. keyEncapsulation.ClientShare, .. keyAgreement.ClientShare];
        SshWireWriter init = new();
        init.WriteByte(SshMessageNumber.KeyExchangeDiffieHellmanInit);
        init.WriteString(clientShare);
        await messages.SendAsync(init.ToArray(), cancellationToken).ConfigureAwait(false);

        SshWireReader reply = await messages.ReadAsync(SshMessageNumber.KeyExchangeDiffieHellmanReply, cancellationToken).ConfigureAwait(false);
        byte[] hostKey = reply.ReadString().ToArray();
        byte[] serverShare = reply.ReadString().ToArray();
        byte[] signature = reply.ReadString().ToArray();
        int expectedLength = keyEncapsulation.ServerShareLength + keyAgreement.ServerShareLength;
        if (serverShare.Length != expectedLength)
        {
            throw new InvalidDataException($"The SSH server's hybrid key share is {serverShare.Length} bytes, not {expectedLength}.");
        }

        byte[] sharedSecret = CombineSecrets(
            keyEncapsulation.ComputeSharedSecret(serverShare.AsSpan(0, keyEncapsulation.ServerShareLength)),
            keyAgreement.ComputeSharedSecret(serverShare.AsSpan(keyEncapsulation.ServerShareLength)));
        byte[] encodedSharedSecret = SshExchangeHashInput.EncodeString(sharedSecret);
        CryptographicOperations.ZeroMemory(sharedSecret);

        SshExchangeHashInput hashInput = new(handshake, hostKey);
        hashInput.Fields.WriteString(clientShare);
        hashInput.Fields.WriteString(serverShare);
        return new SshKeyExchangeOutcome(hostKey, signature, encodedSharedSecret, hashInput.ComputeHash(encodedSharedSecret, hashAlgorithm), hashAlgorithm);
    }

    private byte[] CombineSecrets(byte[] keyEncapsulationSecret, byte[] keyAgreementSecret)
    {
        try
        {
            return CryptographicOperations.HashData(hashAlgorithm, [.. keyEncapsulationSecret, .. keyAgreementSecret]);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyEncapsulationSecret);
            CryptographicOperations.ZeroMemory(keyAgreementSecret);
        }
    }
}
