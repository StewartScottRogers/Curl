using static Curl.Quic.QuicTest;

namespace Curl.Quic;

[TestClass]
public sealed class QuicTransportParametersTests
{
    private static readonly byte[] SourceConnectionId = [.. Enumerable.Range(0, 20).Select(value => (byte)value)];

    [TestMethod]
    public void Encode_CurlClientDefaults_MatchesCurlsBuild()
    {
        // ADR-0144 section 5: initial_source_connection_id, 05, 06, 07, 04, 08, 09, then version_information.
        byte[] encoded = (QuicTransportParameters.CurlClientDefaults with { InitialSourceConnectionId = SourceConnectionId }).Encode();

        Assert.AreEqual(
            "0f14000102030405060708090a0b0c0d0e0f10111213"
            + "050480008000" + "060480008000" + "0704be800000" + "0404be800000" + "080480040000" + "090480040000"
            + "11080000000100000001",
            HexOf(encoded));
    }

    [TestMethod]
    public void Decode_EveryParameter_RoundTrips()
    {
        QuicTransportParameters parameters = new()
        {
            OriginalDestinationConnectionId = [1, 2, 3],
            MaxIdleTimeout = 30000,
            StatelessResetToken = new byte[16],
            MaxUdpPayloadSize = 1452,
            InitialMaxData = 1,
            InitialMaxStreamDataBidiLocal = 2,
            InitialMaxStreamDataBidiRemote = 3,
            InitialMaxStreamDataUni = 4,
            InitialMaxStreamsBidi = 5,
            InitialMaxStreamsUni = 6,
            AckDelayExponent = 20,
            MaxAckDelay = 16383,
            DisableActiveMigration = true,
            PreferredAddress = [9, 9],
            ActiveConnectionIdLimit = 8,
            InitialSourceConnectionId = [4],
            RetrySourceConnectionId = [5, 6],
            VersionInformation = new QuicVersionInformation(1, [1, 0x6b3343cf]),
        };

        QuicTransportParameters decoded = QuicTransportParameters.Decode(parameters.Encode());

        Assert.AreEqual(HexOf(parameters.Encode()), HexOf(decoded.Encode()));
        Assert.IsTrue(decoded.DisableActiveMigration);
        Assert.AreEqual(1452UL, decoded.MaxUdpPayloadSize);
        CollectionAssert.AreEqual(new uint[] { 1, 0x6b3343cf }, decoded.VersionInformation!.AvailableVersions.ToArray());
        CollectionAssert.AreEqual(new byte[] { 9, 9 }, decoded.PreferredAddress);
    }

    [TestMethod]
    public void Decode_AbsentAndUnknownParameters_TakeDefaultsAndAreIgnored()
    {
        // A reserved parameter (31 * 1 + 27 = 58) with a two-byte value.
        QuicTransportParameters decoded = QuicTransportParameters.Decode(Hex("3a 02 ab cd"));

        Assert.AreEqual(QuicTransportParameters.DefaultMaxUdpPayloadSize, decoded.MaxUdpPayloadSize);
        Assert.AreEqual(QuicTransportParameters.DefaultAckDelayExponent, decoded.AckDelayExponent);
        Assert.AreEqual(QuicTransportParameters.DefaultMaxAckDelay, decoded.MaxAckDelay);
        Assert.AreEqual(QuicTransportParameters.DefaultActiveConnectionIdLimit, decoded.ActiveConnectionIdLimit);
        Assert.IsFalse(decoded.DisableActiveMigration);
        Assert.IsNull(decoded.OriginalDestinationConnectionId);
        Assert.IsNull(decoded.VersionInformation);
        Assert.IsEmpty(new QuicTransportParameters().Encode());
    }

    [TestMethod]
    [DataRow("01 01 05 01 01 06", DisplayName = "A parameter appears twice")]
    [DataRow("01 04 05", DisplayName = "A value is truncated")]
    [DataRow("01 02 05 00", DisplayName = "An integer has bytes after it")]
    [DataRow("03 02 44 af", DisplayName = "max_udp_payload_size below 1200")]
    [DataRow("0a 01 15", DisplayName = "ack_delay_exponent above 20")]
    [DataRow("0b 02 80 00 40 00", DisplayName = "max_ack_delay of 2^14")]
    [DataRow("0e 01 01", DisplayName = "active_connection_id_limit below 2")]
    [DataRow("08 08 d0 00 00 00 00 00 00 01", DisplayName = "initial_max_streams_bidi above 2^60")]
    [DataRow("02 0f 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00", DisplayName = "A 15-byte stateless reset token")]
    [DataRow("0f 15 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00", DisplayName = "A 21-byte connection ID")]
    [DataRow("0c 01 00", DisplayName = "disable_active_migration with a value")]
    [DataRow("11 06 00 00 00 01 00 00", DisplayName = "version_information of 6 bytes")]
    [DataRow("11 04 00 00 00 00", DisplayName = "version_information choosing version 0")]
    [DataRow("11 00", DisplayName = "version_information with no chosen version")]
    public void Decode_MalformedParameter_ThrowsTransportParameterError(string hex) =>
        Assert.AreEqual(QuicTransportErrorCode.TransportParameterError, ErrorOf(() => QuicTransportParameters.Decode(Hex(hex))));
}
