using Curl.Cryptography;
using CryptographyHpke = Curl.Cryptography.Hpke;

namespace Curl.Extension.ObliviousHttp.Hpke;

/// <summary>
/// The recipient's side of HPKE base mode (RFC 9180 sections 5.1, 5.2 and 5.3) over
/// <c>Curl.Cryptography</c>'s <see cref="CryptographyHpke" />: <c>SetupBaseR</c> gives a
/// context that opens the sender's messages in sequence and exports the same secrets.
/// </summary>
/// <remarks>
/// An unusable encapsulated key throws <see cref="HpkeSetupException" />; a message that does
/// not authenticate throws <see cref="HpkeOpenException" />.
/// </remarks>
public sealed class HpkeReceiver : IDisposable
{
    private readonly HpkeContext context;

    private HpkeReceiver(HpkeContext context) => this.context = context;

    /// <summary><c>SetupBaseR(enc, skR, info)</c>.</summary>
    /// <exception cref="HpkeSetupException"><paramref name="encapsulatedKey" /> cannot be used.</exception>
    /// <exception cref="ArgumentException">A suite value is not supported or <paramref name="recipientPrivateKey" /> is not 32 bytes.</exception>
    public static HpkeReceiver SetUp(HpkeSuite suite, ReadOnlySpan<byte> encapsulatedKey, ReadOnlySpan<byte> recipientPrivateKey, ReadOnlySpan<byte> info) =>
        CryptographyHpke.TrySetupBaseRecipient(suite.Kem, suite.Kdf, suite.Aead, encapsulatedKey, recipientPrivateKey, info, out HpkeContext? context)
            ? new HpkeReceiver(context)
            : throw new HpkeSetupException("HPKE setup failed: the encapsulated key cannot be used.");

    /// <summary><c>ContextR.Open(aad, ct)</c>: the plaintext of the next message in sequence.</summary>
    /// <exception cref="HpkeOpenException"><paramref name="ciphertext" /> does not authenticate; the sequence number is unchanged.</exception>
    /// <exception cref="ObjectDisposedException">The receiver has been disposed.</exception>
    public byte[] Open(ReadOnlySpan<byte> associatedData, ReadOnlySpan<byte> ciphertext)
    {
        byte[] plaintext = new byte[Math.Max(ciphertext.Length - HpkeContext.TagSize, 0)];
        return context.TryOpen(associatedData, ciphertext, plaintext) ? plaintext : throw new HpkeOpenException();
    }

    /// <summary><c>Context.Export(exporter_context, L)</c>: a secret of <paramref name="length" /> bytes bound to <paramref name="exporterContext" />.</summary>
    /// <exception cref="ArgumentException"><paramref name="length" /> is not between 1 and 8160.</exception>
    /// <exception cref="ObjectDisposedException">The receiver has been disposed.</exception>
    public byte[] Export(ReadOnlySpan<byte> exporterContext, int length) => HpkeExport.Of(context, exporterContext, length);

    /// <summary>Zeroes the context's key, base nonce and exporter secret.</summary>
    public void Dispose() => context.Dispose();
}
