namespace Curl.Kerberos;

/// <summary>
/// Reads MIT Kerberos's credential cache file format, version 4 (MIT Kerberos
/// documentation, "Credential cache file format"), which every MIT release since 1.2 writes.
/// Versions 1 to 3 are refused as <see cref="KerberosFileError.UnknownVersion" /> (ADR-0158).
/// </summary>
public static class CredentialCacheReader
{
    private const byte FileFormatMarker = 0x05;

    private const byte SupportedVersion = 0x04;

    private const ushort DeltaTimeTag = 1;

    /// <summary>Reads a credential cache from its file bytes.</summary>
    /// <param name="bytes">The whole file.</param>
    /// <returns>The cache's default principal and credentials.</returns>
    /// <exception cref="KerberosFileException">
    /// The file is not version 4 (<see cref="KerberosFileError.UnknownVersion" />) or ends
    /// inside a field (<see cref="KerberosFileError.Truncated" />).
    /// </exception>
    public static CredentialCache Read(ReadOnlySpan<byte> bytes)
    {
        BigEndianFileCursor cursor = new(bytes);
        RequireVersion(ref cursor);
        TimeSpan? kdcTimeOffset = ReadHeader(ref cursor);
        KerberosPrincipal defaultPrincipal = ReadPrincipal(ref cursor);
        List<CachedCredential> credentials = [];
        while (cursor.RemainingLength > 0)
        {
            credentials.Add(ReadCredential(ref cursor));
        }

        return new CredentialCache(kdcTimeOffset, defaultPrincipal, credentials);
    }

    private static void RequireVersion(ref BigEndianFileCursor cursor)
    {
        byte marker = cursor.ReadByte();
        byte version = cursor.ReadByte();
        if (marker != FileFormatMarker || version != SupportedVersion)
        {
            throw new KerberosFileException(KerberosFileError.UnknownVersion);
        }
    }

    private static TimeSpan? ReadHeader(ref BigEndianFileCursor cursor)
    {
        BigEndianFileCursor header = new(cursor.Take(cursor.ReadUInt16()));
        TimeSpan? kdcTimeOffset = null;
        while (header.RemainingLength > 0)
        {
            ushort tag = header.ReadUInt16();
            BigEndianFileCursor value = new(header.Take(header.ReadUInt16()));
            if (tag == DeltaTimeTag)
            {
                int seconds = value.ReadInt32();
                int microseconds = value.ReadInt32();
                kdcTimeOffset = TimeSpan.FromSeconds(seconds) + TimeSpan.FromMicroseconds(microseconds);
            }
        }

        return kdcTimeOffset;
    }

    private static KerberosPrincipal ReadPrincipal(ref BigEndianFileCursor cursor)
    {
        int nameType = cursor.ReadInt32();
        uint componentCount = cursor.ReadUInt32();
        string realm = cursor.ReadString32();
        List<string> components = [];
        for (uint index = 0; index < componentCount; index++)
        {
            components.Add(cursor.ReadString32());
        }

        return new KerberosPrincipal(nameType, realm, components);
    }

    private static CachedCredential ReadCredential(ref BigEndianFileCursor cursor)
    {
        KerberosPrincipal client = ReadPrincipal(ref cursor);
        KerberosPrincipal server = ReadPrincipal(ref cursor);
        int encryptionType = cursor.ReadUInt16();
        KerberosKey sessionKey = new(encryptionType, cursor.ReadBytes32());
        return new CachedCredential
        {
            Client = client,
            Server = server,
            SessionKey = sessionKey,
            AuthenticationTime = ReadTime(ref cursor),
            StartTime = ReadTime(ref cursor),
            EndTime = ReadTime(ref cursor),
            RenewUntil = ReadTime(ref cursor),
            IsEncryptedInSessionKey = cursor.ReadByte() != 0,
            Flags = (KerberosTicketFlags)cursor.ReadUInt32(),
            Addresses = ReadAddresses(ref cursor),
            AuthorizationData = ReadAuthorizationData(ref cursor),
            Ticket = cursor.ReadBytes32(),
            SecondTicket = cursor.ReadBytes32(),
        };
    }

    private static DateTimeOffset ReadTime(ref BigEndianFileCursor cursor) =>
        DateTimeOffset.FromUnixTimeSeconds(cursor.ReadUInt32());

    private static List<KerberosAddress> ReadAddresses(ref BigEndianFileCursor cursor)
    {
        uint count = cursor.ReadUInt32();
        List<KerberosAddress> addresses = [];
        for (uint index = 0; index < count; index++)
        {
            int addressType = cursor.ReadUInt16();
            addresses.Add(new KerberosAddress(addressType, cursor.ReadBytes32()));
        }

        return addresses;
    }

    private static List<KerberosAuthorizationData> ReadAuthorizationData(ref BigEndianFileCursor cursor)
    {
        uint count = cursor.ReadUInt32();
        List<KerberosAuthorizationData> elements = [];
        for (uint index = 0; index < count; index++)
        {
            int dataType = cursor.ReadUInt16();
            elements.Add(new KerberosAuthorizationData(dataType, cursor.ReadBytes32()));
        }

        return elements;
    }
}
