namespace Curl.Quic;

/// <summary>
/// The QUIC transport parameters one endpoint declares in its <c>quic_transport_parameters</c>
/// TLS extension (RFC 9000 section 18, RFC 9368 for <c>version_information</c>). Each
/// property holds the parameter's value, or its RFC 9000 default when it was not sent;
/// <see cref="Encode" /> writes only the parameters that differ from their defaults, so the
/// client's parameters come out exactly as curl's ngtcp2 build sends them (ADR-0144
/// section 5). Parameters of an unknown ID, reserved ones included, are ignored when read.
/// </summary>
public sealed record QuicTransportParameters
{
    /// <summary>The default and largest <c>max_udp_payload_size</c>.</summary>
    public const ulong DefaultMaxUdpPayloadSize = 65527;

    /// <summary>The smallest <c>max_udp_payload_size</c> a peer may declare.</summary>
    public const ulong MinimumMaxUdpPayloadSize = 1200;

    /// <summary>The default <c>ack_delay_exponent</c>.</summary>
    public const ulong DefaultAckDelayExponent = 3;

    /// <summary>The default <c>max_ack_delay</c>, in milliseconds.</summary>
    public const ulong DefaultMaxAckDelay = 25;

    /// <summary>The default and smallest <c>active_connection_id_limit</c>.</summary>
    public const ulong DefaultActiveConnectionIdLimit = 2;

    private const ulong LargestAckDelayExponent = 20;

    private const ulong LargestMaxAckDelay = (1UL << 14) - 1;

    /// <summary>
    /// Gets the parameters curl's ngtcp2 build sends, as measured and recorded in ADR-0144
    /// section 5, without the <c>initial_source_connection_id</c> each connection adds.
    /// </summary>
    public static QuicTransportParameters CurlClientDefaults { get; } = new()
    {
        InitialMaxStreamDataBidiLocal = 32768,
        InitialMaxStreamDataBidiRemote = 32768,
        InitialMaxStreamDataUni = 1048576000,
        InitialMaxData = 1048576000,
        InitialMaxStreamsBidi = 262144,
        InitialMaxStreamsUni = 262144,
        VersionInformation = QuicVersionInformation.Version1Only,
    };

    /// <summary>Gets <c>original_destination_connection_id</c> (0x00), which only a server sends.</summary>
    public byte[]? OriginalDestinationConnectionId { get; init; }

    /// <summary>Gets <c>max_idle_timeout</c> (0x01) in milliseconds; 0 means none.</summary>
    public ulong MaxIdleTimeout { get; init; }

    /// <summary>Gets <c>stateless_reset_token</c> (0x02), 16 bytes, which only a server sends.</summary>
    public byte[]? StatelessResetToken { get; init; }

    /// <summary>Gets <c>max_udp_payload_size</c> (0x03).</summary>
    public ulong MaxUdpPayloadSize { get; init; } = DefaultMaxUdpPayloadSize;

    /// <summary>Gets <c>initial_max_data</c> (0x04).</summary>
    public ulong InitialMaxData { get; init; }

    /// <summary>Gets <c>initial_max_stream_data_bidi_local</c> (0x05).</summary>
    public ulong InitialMaxStreamDataBidiLocal { get; init; }

    /// <summary>Gets <c>initial_max_stream_data_bidi_remote</c> (0x06).</summary>
    public ulong InitialMaxStreamDataBidiRemote { get; init; }

    /// <summary>Gets <c>initial_max_stream_data_uni</c> (0x07).</summary>
    public ulong InitialMaxStreamDataUni { get; init; }

    /// <summary>Gets <c>initial_max_streams_bidi</c> (0x08).</summary>
    public ulong InitialMaxStreamsBidi { get; init; }

    /// <summary>Gets <c>initial_max_streams_uni</c> (0x09).</summary>
    public ulong InitialMaxStreamsUni { get; init; }

    /// <summary>Gets <c>ack_delay_exponent</c> (0x0a).</summary>
    public ulong AckDelayExponent { get; init; } = DefaultAckDelayExponent;

    /// <summary>Gets <c>max_ack_delay</c> (0x0b) in milliseconds.</summary>
    public ulong MaxAckDelay { get; init; } = DefaultMaxAckDelay;

    /// <summary>Gets a value indicating whether <c>disable_active_migration</c> (0x0c) was sent.</summary>
    public bool DisableActiveMigration { get; init; }

    /// <summary>Gets the value of <c>preferred_address</c> (0x0d) unread, which only a server sends.</summary>
    public byte[]? PreferredAddress { get; init; }

    /// <summary>Gets <c>active_connection_id_limit</c> (0x0e).</summary>
    public ulong ActiveConnectionIdLimit { get; init; } = DefaultActiveConnectionIdLimit;

    /// <summary>Gets <c>initial_source_connection_id</c> (0x0f).</summary>
    public byte[]? InitialSourceConnectionId { get; init; }

    /// <summary>Gets <c>retry_source_connection_id</c> (0x10), which only a server that sent a Retry sends.</summary>
    public byte[]? RetrySourceConnectionId { get; init; }

    /// <summary>Gets <c>version_information</c> (0x11).</summary>
    public QuicVersionInformation? VersionInformation { get; init; }

    /// <summary>
    /// Writes the parameters that differ from their defaults, in curl's order: the
    /// connection IDs, the stream and connection limits (bidirectional stream data before
    /// unidirectional, then <c>initial_max_data</c>, then the stream counts), the remaining
    /// integers, then <c>version_information</c> last.
    /// </summary>
    /// <returns>The extension data.</returns>
    public byte[] Encode()
    {
        var writer = new QuicWriter();
        WriteBytes(writer, 0x00, OriginalDestinationConnectionId);
        WriteBytes(writer, 0x0f, InitialSourceConnectionId);
        WriteBytes(writer, 0x10, RetrySourceConnectionId);
        WriteBytes(writer, 0x02, StatelessResetToken);
        WriteInteger(writer, 0x01, MaxIdleTimeout, 0);
        WriteInteger(writer, 0x03, MaxUdpPayloadSize, DefaultMaxUdpPayloadSize);
        WriteInteger(writer, 0x05, InitialMaxStreamDataBidiLocal, 0);
        WriteInteger(writer, 0x06, InitialMaxStreamDataBidiRemote, 0);
        WriteInteger(writer, 0x07, InitialMaxStreamDataUni, 0);
        WriteInteger(writer, 0x04, InitialMaxData, 0);
        WriteInteger(writer, 0x08, InitialMaxStreamsBidi, 0);
        WriteInteger(writer, 0x09, InitialMaxStreamsUni, 0);
        WriteInteger(writer, 0x0a, AckDelayExponent, DefaultAckDelayExponent);
        WriteInteger(writer, 0x0b, MaxAckDelay, DefaultMaxAckDelay);
        WriteBytes(writer, 0x0c, DisableActiveMigration ? [] : null);
        WriteInteger(writer, 0x0e, ActiveConnectionIdLimit, DefaultActiveConnectionIdLimit);
        WriteBytes(writer, 0x0d, PreferredAddress);
        WriteBytes(writer, 0x11, VersionInformation?.Encode());
        return writer.ToArray();
    }

    /// <summary>Reads the parameters a peer sent.</summary>
    /// <param name="encoded">The <c>quic_transport_parameters</c> extension data.</param>
    /// <returns>The parameters, with defaults for those not sent.</returns>
    /// <exception cref="QuicTransportException">
    /// A parameter is truncated or appears twice, an integer parameter has bytes after its
    /// value or a value out of its range, a connection ID is longer than 20 bytes, the
    /// reset token is not 16 bytes, or <c>disable_active_migration</c> has a value
    /// (<see cref="QuicTransportErrorCode.TransportParameterError" />).
    /// </exception>
    public static QuicTransportParameters Decode(ReadOnlyMemory<byte> encoded)
    {
        var values = ReadValues(encoded);
        return new QuicTransportParameters
        {
            OriginalDestinationConnectionId = ConnectionId(values, 0x00),
            MaxIdleTimeout = Integer(values, 0x01, 0),
            StatelessResetToken = Bytes(values, 0x02, QuicNewConnectionIdFrame.StatelessResetTokenLength, QuicNewConnectionIdFrame.StatelessResetTokenLength),
            MaxUdpPayloadSize = Integer(values, 0x03, DefaultMaxUdpPayloadSize, MinimumMaxUdpPayloadSize),
            InitialMaxData = Integer(values, 0x04, 0),
            InitialMaxStreamDataBidiLocal = Integer(values, 0x05, 0),
            InitialMaxStreamDataBidiRemote = Integer(values, 0x06, 0),
            InitialMaxStreamDataUni = Integer(values, 0x07, 0),
            InitialMaxStreamsBidi = Integer(values, 0x08, 0, maximum: QuicFrameCodec.MaximumStreamCount),
            InitialMaxStreamsUni = Integer(values, 0x09, 0, maximum: QuicFrameCodec.MaximumStreamCount),
            AckDelayExponent = Integer(values, 0x0a, DefaultAckDelayExponent, maximum: LargestAckDelayExponent),
            MaxAckDelay = Integer(values, 0x0b, DefaultMaxAckDelay, maximum: LargestMaxAckDelay),
            DisableActiveMigration = Bytes(values, 0x0c, 0, 0) is not null,
            PreferredAddress = Bytes(values, 0x0d, 0, int.MaxValue),
            ActiveConnectionIdLimit = Integer(values, 0x0e, DefaultActiveConnectionIdLimit, DefaultActiveConnectionIdLimit),
            InitialSourceConnectionId = ConnectionId(values, 0x0f),
            RetrySourceConnectionId = ConnectionId(values, 0x10),
            VersionInformation = values.TryGetValue(0x11, out var version) ? QuicVersionInformation.Decode(version) : null,
        };
    }

    private static Dictionary<ulong, ReadOnlyMemory<byte>> ReadValues(ReadOnlyMemory<byte> encoded)
    {
        var reader = new QuicReader(encoded, QuicTransportErrorCode.TransportParameterError);
        var values = new Dictionary<ulong, ReadOnlyMemory<byte>>();
        while (reader.Remaining > 0)
        {
            var id = reader.ReadVariableLengthInteger();
            if (!values.TryAdd(id, reader.ReadLengthPrefixedBytes()))
            {
                throw reader.Fail($"Transport parameter 0x{id:x} appears twice.");
            }
        }

        return values;
    }

    private static ulong Integer(Dictionary<ulong, ReadOnlyMemory<byte>> values, ulong id, ulong defaultValue, ulong minimum = 0, ulong maximum = QuicVariableLengthInteger.MaximumValue)
    {
        if (!values.TryGetValue(id, out var value))
        {
            return defaultValue;
        }

        var reader = new QuicReader(value, QuicTransportErrorCode.TransportParameterError);
        var integer = reader.ReadVariableLengthInteger();
        if (reader.Remaining != 0 || integer < minimum || integer > maximum)
        {
            throw reader.Fail($"Transport parameter 0x{id:x} is not one integer from {minimum} to {maximum}.");
        }

        return integer;
    }

    private static byte[]? ConnectionId(Dictionary<ulong, ReadOnlyMemory<byte>> values, ulong id) =>
        Bytes(values, id, 0, QuicFrameCodec.MaximumConnectionIdLength);

    private static byte[]? Bytes(Dictionary<ulong, ReadOnlyMemory<byte>> values, ulong id, int minimumLength, int maximumLength)
    {
        if (!values.TryGetValue(id, out var value))
        {
            return null;
        }

        return value.Length >= minimumLength && value.Length <= maximumLength
            ? value.ToArray()
            : throw new QuicTransportException(QuicTransportErrorCode.TransportParameterError, $"Transport parameter 0x{id:x} is {value.Length} bytes, not {minimumLength} to {maximumLength}.");
    }

    private static void WriteInteger(QuicWriter writer, ulong id, ulong value, ulong defaultValue)
    {
        if (value != defaultValue)
        {
            var bytes = new byte[QuicVariableLengthInteger.GetEncodedLength(value)];
            QuicVariableLengthInteger.Write(value, bytes);
            WriteBytes(writer, id, bytes);
        }
    }

    private static void WriteBytes(QuicWriter writer, ulong id, byte[]? value)
    {
        if (value is not null)
        {
            writer.WriteVariableLengthInteger(id);
            writer.WriteLengthPrefixedBytes(value);
        }
    }
}
