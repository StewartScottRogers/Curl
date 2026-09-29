namespace Curl.Cryptography;

/// <summary>
/// One HPKE base-mode test vector: the suite, skEm, pkRm, skRm, enc, shared_secret, key,
/// base_nonce, exporter_secret, the ciphertexts of sequence numbers 0 and 1, and the
/// 32-byte export for an empty exporter context, all hexadecimal.
/// </summary>
public sealed record HpkeBaseModeVector(
    string Source,
    HpkeKem Kem,
    HpkeAead Aead,
    string EphemeralPrivateKey,
    string RecipientPublicKey,
    string RecipientPrivateKey,
    string EncapsulatedKey,
    string SharedSecret,
    string Key,
    string BaseNonce,
    string ExporterSecret,
    string Ciphertext0,
    string Ciphertext1,
    string ExportedValue)
{
    /// <summary>The vector's source, so each data row is named by it.</summary>
    public override string ToString() => Source;
}
