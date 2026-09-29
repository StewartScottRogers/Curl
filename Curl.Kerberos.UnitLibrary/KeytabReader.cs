namespace Curl.Kerberos;

/// <summary>
/// Reads MIT Kerberos's keytab file format, version 2 (MIT Kerberos documentation, "Keytab
/// file format"), the only version MIT has written since 1.0. Version 1, in the host's byte
/// order, is refused as <see cref="KerberosFileError.UnknownVersion" /> (ADR-0158).
/// </summary>
public static class KeytabReader
{
    private const byte FileFormatMarker = 0x05;

    private const byte SupportedVersion = 0x02;

    private const int SizeFieldLength = 4;

    /// <summary>Reads a keytab from its file bytes.</summary>
    /// <param name="bytes">The whole file.</param>
    /// <returns>The keytab's entries.</returns>
    /// <remarks>
    /// As MIT's reader does, an entry size of zero, or fewer than four bytes left for one,
    /// ends the keytab, and a negative size is a hole of that many bytes, skipped.
    /// </remarks>
    /// <exception cref="KerberosFileException">
    /// The file is not version 2 (<see cref="KerberosFileError.UnknownVersion" />) or an
    /// entry runs past the end (<see cref="KerberosFileError.Truncated" />).
    /// </exception>
    public static Keytab Read(ReadOnlySpan<byte> bytes)
    {
        BigEndianFileCursor cursor = new(bytes);
        RequireVersion(ref cursor);
        List<KeytabEntry> entries = [];
        while (cursor.RemainingLength >= SizeFieldLength)
        {
            long size = cursor.ReadInt32();
            if (size == 0)
            {
                break;
            }

            ReadOnlySpan<byte> record = cursor.Take(Math.Abs(size));
            if (size > 0)
            {
                entries.Add(ReadEntry(record));
            }
        }

        return new Keytab(entries);
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

    private static KeytabEntry ReadEntry(ReadOnlySpan<byte> record)
    {
        BigEndianFileCursor cursor = new(record);
        int componentCount = cursor.ReadUInt16();
        string realm = cursor.ReadString16();
        List<string> components = [];
        for (int index = 0; index < componentCount; index++)
        {
            components.Add(cursor.ReadString16());
        }

        int nameType = cursor.ReadInt32();
        DateTimeOffset timestamp = DateTimeOffset.FromUnixTimeSeconds(cursor.ReadUInt32());
        uint keyVersionNumber = cursor.ReadByte();
        int encryptionType = cursor.ReadUInt16();
        KerberosKey key = new(encryptionType, cursor.ReadBytes16());
        uint extendedKeyVersionNumber = cursor.RemainingLength >= SizeFieldLength ? cursor.ReadUInt32() : 0;
        if (extendedKeyVersionNumber != 0)
        {
            keyVersionNumber = extendedKeyVersionNumber;
        }

        return new KeytabEntry(new KerberosPrincipal(nameType, realm, components), timestamp, keyVersionNumber, key);
    }
}
