namespace Curl.Quic;

/// <summary>
/// The <c>version_information</c> transport parameter (RFC 9368 section 3): the version
/// the sender chose for the connection and the versions it supports.
/// </summary>
/// <param name="ChosenVersion">The version in use, never 0.</param>
/// <param name="AvailableVersions">The versions the sender supports, in its order of preference.</param>
public sealed record QuicVersionInformation(uint ChosenVersion, IReadOnlyList<uint> AvailableVersions)
{
    /// <summary>Gets the version information curl's build sends: version 1 chosen, version 1 available.</summary>
    public static QuicVersionInformation Version1Only { get; } = new(QuicPacketCodec.Version1, [QuicPacketCodec.Version1]);

    /// <summary>Returns the parameter's value bytes.</summary>
    /// <returns>The chosen version, then each available version, four bytes each.</returns>
    public byte[] Encode()
    {
        var writer = new QuicWriter();
        writer.WriteUInt(ChosenVersion, 4);
        foreach (var version in AvailableVersions)
        {
            writer.WriteUInt(version, 4);
        }

        return writer.ToArray();
    }

    /// <summary>Reads the parameter's value bytes.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The version information.</returns>
    /// <exception cref="QuicTransportException">The value is not a whole number of versions, has no chosen version, or chooses version 0 (<see cref="QuicTransportErrorCode.TransportParameterError" />).</exception>
    public static QuicVersionInformation Decode(ReadOnlyMemory<byte> value)
    {
        var reader = new QuicReader(value, QuicTransportErrorCode.TransportParameterError);
        if (value.Length % 4 != 0)
        {
            throw reader.Fail($"version_information is {value.Length} bytes, not a whole number of versions.");
        }

        var chosen = reader.ReadUInt(4);
        if (chosen == 0)
        {
            throw reader.Fail("version_information chooses version 0.");
        }

        var available = new List<uint>();
        while (reader.Remaining > 0)
        {
            available.Add(reader.ReadUInt(4));
        }

        return new QuicVersionInformation(chosen, available);
    }
}
