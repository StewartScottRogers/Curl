namespace Curl.Tls;

/// <summary>
/// The <c>pre_shared_key</c> extension (RFC 8446 section 4.2.11): the identities and
/// binders a ClientHello offers, and the index a ServerHello selects. In a ClientHello it
/// must be the last extension.
/// </summary>
public static class PreSharedKeyExtension
{
    /// <summary>Returns a ClientHello's <c>pre_shared_key</c> carrying <paramref name="offer" />.</summary>
    /// <param name="offer">The identities and binders.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension EncodeOffered(OfferedPsks offer)
    {
        ArgumentNullException.ThrowIfNull(offer);
        TlsWriter writer = new();
        writer.WriteVector(2, identities =>
        {
            foreach (PskIdentity identity in offer.Identities)
            {
                identities.WriteOpaque(2, identity.Identity);
                identities.WriteUInt32(identity.ObfuscatedTicketAge);
            }
        });
        writer.WriteVector(2, binders =>
        {
            foreach (byte[] binder in offer.Binders)
            {
                binders.WriteOpaque(1, binder);
            }
        });
        return new TlsExtension(TlsExtensionType.PreSharedKey, writer.ToArray());
    }

    /// <summary>Decodes a ClientHello's <c>pre_shared_key</c> data.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The identities and binders, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<OfferedPsks> DecodeOffered(byte[] data)
    {
        TlsReader reader = new(data);
        TlsReader identityList = reader.ReadVector(2);
        List<PskIdentity> identities = [];
        while (identityList.HasMore)
        {
            byte[] identity = identityList.ReadOpaque(2);
            identities.Add(new PskIdentity(identity, identityList.ReadUInt32()));
        }

        TlsReader binderList = reader.ReadVector(2);
        List<byte[]> binders = [];
        while (binderList.HasMore)
        {
            binders.Add(binderList.ReadOpaque(1));
        }

        return reader.Finish(new OfferedPsks(identities, binders));
    }

    /// <summary>Returns a ServerHello's <c>pre_shared_key</c> selecting identity <paramref name="selectedIdentity" />.</summary>
    /// <param name="selectedIdentity">The zero-based index of the identity the server accepted.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension EncodeSelected(ushort selectedIdentity)
    {
        TlsWriter writer = new();
        writer.WriteUInt16(selectedIdentity);
        return new TlsExtension(TlsExtensionType.PreSharedKey, writer.ToArray());
    }

    /// <summary>Decodes a ServerHello's <c>pre_shared_key</c> data.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The selected identity's index, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<ushort> DecodeSelected(byte[] data)
    {
        TlsReader reader = new(data);
        return reader.Finish(reader.ReadUInt16());
    }
}
