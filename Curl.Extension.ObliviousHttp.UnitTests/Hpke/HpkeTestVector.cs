namespace Curl.Extension.ObliviousHttp.Hpke;

/// <summary>
/// One RFC 9180 base-mode test vector: the suite, skEm, pkRm, skRm, enc, the ciphertexts of
/// sequence numbers 0 and 1, and the 32-byte export for an empty exporter context, hexadecimal.
/// </summary>
public sealed record HpkeTestVector(
    string Source,
    HpkeSuite Suite,
    string EphemeralPrivateKey,
    string RecipientPublicKey,
    string RecipientPrivateKey,
    string EncapsulatedKey,
    string Ciphertext0,
    string Ciphertext1,
    string ExportedValue)
{
    /// <summary>The vector's source, so each data row is named by it.</summary>
    public override string ToString() => Source;
}
