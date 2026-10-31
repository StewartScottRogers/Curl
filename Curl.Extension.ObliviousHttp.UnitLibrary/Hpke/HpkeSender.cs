using Curl.Cryptography;
using CryptographyHpke = Curl.Cryptography.Hpke;

namespace Curl.Extension.ObliviousHttp.Hpke;

/// <summary>
/// The sender's side of HPKE base mode (RFC 9180 sections 5.1, 5.2 and 5.3) over
/// <c>Curl.Cryptography</c>'s <see cref="CryptographyHpke" />: <c>SetupBaseS</c> gives the
/// encapsulated key to send and a context that seals messages in sequence and exports secrets.
/// </summary>
/// <remarks>An unusable recipient public key throws <see cref="HpkeSetupException" />.</remarks>
public sealed class HpkeSender : IDisposable
{
    private readonly HpkeContext context;
    private readonly byte[] encapsulatedKey;

    private HpkeSender(HpkeContext context, byte[] encapsulatedKey)
    {
        this.context = context;
        this.encapsulatedKey = encapsulatedKey;
    }

    /// <summary>enc, the encapsulated key the recipient sets up its context with.</summary>
    public ReadOnlySpan<byte> EncapsulatedKey => encapsulatedKey;

    /// <summary><c>SetupBaseS(pkR, info)</c> with a fresh ephemeral key.</summary>
    /// <exception cref="HpkeSetupException"><paramref name="recipientPublicKey" /> cannot be used.</exception>
    /// <exception cref="ArgumentException">A suite value is not supported.</exception>
    public static HpkeSender SetUp(HpkeSuite suite, ReadOnlySpan<byte> recipientPublicKey, ReadOnlySpan<byte> info)
    {
        byte[] encapsulatedKey = new byte[CryptographyHpke.GetEncapsulatedKeySize(suite.Kem)];
        return CryptographyHpke.TrySetupBaseSender(suite.Kem, suite.Kdf, suite.Aead, recipientPublicKey, info, encapsulatedKey, out HpkeContext? context)
            ? new HpkeSender(context, encapsulatedKey)
            : throw UnusableRecipientPublicKey();
    }

    /// <summary>
    /// <c>SetupBaseS(pkR, info)</c> with <paramref name="ephemeralPrivateKey" /> as skE, so a
    /// published test vector can be reproduced.
    /// </summary>
    /// <exception cref="HpkeSetupException"><paramref name="recipientPublicKey" /> cannot be used.</exception>
    /// <exception cref="ArgumentException">A suite value is not supported or <paramref name="ephemeralPrivateKey" /> is not 32 bytes.</exception>
    public static HpkeSender SetUp(HpkeSuite suite, ReadOnlySpan<byte> recipientPublicKey, ReadOnlySpan<byte> ephemeralPrivateKey, ReadOnlySpan<byte> info)
    {
        byte[] encapsulatedKey = new byte[CryptographyHpke.GetEncapsulatedKeySize(suite.Kem)];
        return CryptographyHpke.TrySetupBaseSender(suite.Kem, suite.Kdf, suite.Aead, recipientPublicKey, ephemeralPrivateKey, info, encapsulatedKey, out HpkeContext? context)
            ? new HpkeSender(context, encapsulatedKey)
            : throw UnusableRecipientPublicKey();
    }

    /// <summary><c>ContextS.Seal(aad, pt)</c>: the ciphertext and tag of the next message in sequence.</summary>
    /// <exception cref="ObjectDisposedException">The sender has been disposed.</exception>
    public byte[] Seal(ReadOnlySpan<byte> associatedData, ReadOnlySpan<byte> plaintext)
    {
        byte[] ciphertext = new byte[plaintext.Length + HpkeContext.TagSize];
        context.Seal(associatedData, plaintext, ciphertext);
        return ciphertext;
    }

    /// <summary><c>Context.Export(exporter_context, L)</c>: a secret of <paramref name="length" /> bytes bound to <paramref name="exporterContext" />.</summary>
    /// <exception cref="ArgumentException"><paramref name="length" /> is not between 1 and 8160.</exception>
    /// <exception cref="ObjectDisposedException">The sender has been disposed.</exception>
    public byte[] Export(ReadOnlySpan<byte> exporterContext, int length) => HpkeExport.Of(context, exporterContext, length);

    /// <summary>Zeroes the context's key, base nonce and exporter secret.</summary>
    public void Dispose() => context.Dispose();

    private static HpkeSetupException UnusableRecipientPublicKey() =>
        new("HPKE setup failed: the recipient public key cannot be used.");
}
