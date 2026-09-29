namespace Curl.Kerberos;

/// <summary>
/// Writes credentials in MIT Kerberos's credential cache file format, version 4, exactly as
/// <see cref="CredentialCacheReader" /> reads them, so a credential appended to a cache file
/// reads back as the same credential (MIT <c>cc_file.c</c>'s store, ADR-0208).
/// </summary>
public static class CredentialCacheWriter
{
    /// <summary>Marshals one credential as a version 4 cache holds it after its header and default principal.</summary>
    /// <param name="credential">The credential.</param>
    /// <returns>The bytes; they hold the session key, so the caller zeroes them.</returns>
    public static byte[] WriteCredential(CachedCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        BigEndianFileWriter measure = BigEndianFileWriter.Measuring();
        WriteCredential(ref measure, credential);
        byte[] bytes = new byte[measure.Position];
        BigEndianFileWriter writer = new(bytes);
        WriteCredential(ref writer, credential);
        return bytes;
    }

    private static void WriteCredential(ref BigEndianFileWriter writer, CachedCredential credential)
    {
        WritePrincipal(ref writer, credential.Client);
        WritePrincipal(ref writer, credential.Server);
        writer.WriteUInt16((ushort)credential.SessionKey.EncryptionType);
        writer.WriteBytes32(credential.SessionKey.Value);
        WriteTime(ref writer, credential.AuthenticationTime);
        WriteTime(ref writer, credential.StartTime);
        WriteTime(ref writer, credential.EndTime);
        WriteTime(ref writer, credential.RenewUntil);
        writer.WriteByte(credential.IsEncryptedInSessionKey ? (byte)1 : (byte)0);
        writer.WriteUInt32((uint)credential.Flags);
        writer.WriteUInt32((uint)credential.Addresses.Count);
        foreach (KerberosAddress address in credential.Addresses)
        {
            writer.WriteUInt16((ushort)address.AddressType);
            writer.WriteBytes32(address.Address);
        }

        writer.WriteUInt32((uint)credential.AuthorizationData.Count);
        foreach (KerberosAuthorizationData element in credential.AuthorizationData)
        {
            writer.WriteUInt16((ushort)element.DataType);
            writer.WriteBytes32(element.Data);
        }

        writer.WriteBytes32(credential.Ticket);
        writer.WriteBytes32(credential.SecondTicket);
    }

    private static void WritePrincipal(ref BigEndianFileWriter writer, KerberosPrincipal principal)
    {
        writer.WriteInt32(principal.NameType);
        writer.WriteUInt32((uint)principal.Components.Count);
        writer.WriteString32(principal.Realm);
        foreach (string component in principal.Components)
        {
            writer.WriteString32(component);
        }
    }

    private static void WriteTime(ref BigEndianFileWriter writer, DateTimeOffset time) =>
        writer.WriteUInt32((uint)time.ToUnixTimeSeconds());
}
