using Curl.Testing;
using static Curl.Quic.QuicTest;

namespace Curl.Quic;

[TestClass]
public sealed class QuicTransportParametersTests
{
    private static readonly byte[] SourceConnectionId = [.. Enumerable.Range(0, 20).Select(value => (byte)value)];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Encode_CurlClientDefaults_MatchesCurlsBuild()
    {
        // ADR-0144 section 5: initial_source_connection_id, 05, 06, 07, 04, 08, 09, then version_information.
        const string expectedHex = "0f14000102030405060708090a0b0c0d0e0f10111213"
            + "050480008000" + "060480008000" + "0704be800000" + "0404be800000" + "080480040000" + "090480040000"
            + "11080000000100000001";
        Diagnostics.Arrange("initial_source_connection_id", HexOf(SourceConnectionId));

        byte[] encoded = (QuicTransportParameters.CurlClientDefaults with { InitialSourceConnectionId = SourceConnectionId }).Encode();

        Diagnostics.Act("encoded length", encoded.Length);
        Diagnostics.Bytes("encoded transport parameters", encoded);
        Diagnostics.Diff("encoded hex", expectedHex, HexOf(encoded));
        Assert.AreEqual(expectedHex, HexOf(encoded));
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

        Diagnostics.Arrange("every parameter set", "every transport parameter set");
        Diagnostics.Bytes("encoded input", parameters.Encode());

        QuicTransportParameters decoded = QuicTransportParameters.Decode(parameters.Encode());

        Diagnostics.Act("decoded disable_active_migration", decoded.DisableActiveMigration);
        Diagnostics.Act("decoded max_udp_payload_size", decoded.MaxUdpPayloadSize);
        Diagnostics.Bytes("re-encoded", decoded.Encode());
        Diagnostics.Diff("re-encoded hex", HexOf(parameters.Encode()), HexOf(decoded.Encode()));
        Assert.AreEqual(HexOf(parameters.Encode()), HexOf(decoded.Encode()));
        Diagnostics.Assert("disable_active_migration", true, decoded.DisableActiveMigration);
        Assert.IsTrue(decoded.DisableActiveMigration);
        Diagnostics.Assert("max_udp_payload_size", 1452UL, decoded.MaxUdpPayloadSize);
        Assert.AreEqual(1452UL, decoded.MaxUdpPayloadSize);
        Diagnostics.Assert(
            "available versions",
            "1, 6b3343cf",
            string.Join(", ", decoded.VersionInformation!.AvailableVersions.ToArray().Select(version => version.ToString("x", System.Globalization.CultureInfo.InvariantCulture))));
        CollectionAssert.AreEqual(new uint[] { 1, 0x6b3343cf }, decoded.VersionInformation!.AvailableVersions.ToArray());
        Diagnostics.Bytes("preferred address", decoded.PreferredAddress!);
        Diagnostics.Assert("preferred address", "0909", HexOf(decoded.PreferredAddress!));
        CollectionAssert.AreEqual(new byte[] { 9, 9 }, decoded.PreferredAddress);
    }

    [TestMethod]
    public void Decode_AbsentAndUnknownParameters_TakeDefaultsAndAreIgnored()
    {
        // A reserved parameter (31 * 1 + 27 = 58) with a two-byte value.
        Diagnostics.Arrange("transport parameters hex", "3a 02 ab cd");

        QuicTransportParameters decoded = QuicTransportParameters.Decode(Hex("3a 02 ab cd"));

        Diagnostics.Act("max_udp_payload_size", decoded.MaxUdpPayloadSize);
        Diagnostics.Act("ack_delay_exponent", decoded.AckDelayExponent);
        Diagnostics.Act("max_ack_delay", decoded.MaxAckDelay);
        Diagnostics.Act("active_connection_id_limit", decoded.ActiveConnectionIdLimit);
        Diagnostics.Act("disable_active_migration", decoded.DisableActiveMigration);
        Diagnostics.Act("original_destination_connection_id is null", decoded.OriginalDestinationConnectionId is null);
        Diagnostics.Act("version_information is null", decoded.VersionInformation is null);
        Diagnostics.Assert("max_udp_payload_size", QuicTransportParameters.DefaultMaxUdpPayloadSize, decoded.MaxUdpPayloadSize);
        Assert.AreEqual(QuicTransportParameters.DefaultMaxUdpPayloadSize, decoded.MaxUdpPayloadSize);
        Diagnostics.Assert("ack_delay_exponent", QuicTransportParameters.DefaultAckDelayExponent, decoded.AckDelayExponent);
        Assert.AreEqual(QuicTransportParameters.DefaultAckDelayExponent, decoded.AckDelayExponent);
        Diagnostics.Assert("max_ack_delay", QuicTransportParameters.DefaultMaxAckDelay, decoded.MaxAckDelay);
        Assert.AreEqual(QuicTransportParameters.DefaultMaxAckDelay, decoded.MaxAckDelay);
        Diagnostics.Assert("active_connection_id_limit", QuicTransportParameters.DefaultActiveConnectionIdLimit, decoded.ActiveConnectionIdLimit);
        Assert.AreEqual(QuicTransportParameters.DefaultActiveConnectionIdLimit, decoded.ActiveConnectionIdLimit);
        Diagnostics.Assert("disable_active_migration", false, decoded.DisableActiveMigration);
        Assert.IsFalse(decoded.DisableActiveMigration);
        Assert.IsNull(decoded.OriginalDestinationConnectionId);
        Assert.IsNull(decoded.VersionInformation);
        var emptyEncoding = new QuicTransportParameters().Encode();
        Diagnostics.Assert("default parameters encoded length", 0, emptyEncoding.Length);
        Assert.IsEmpty(emptyEncoding);
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
    public void Decode_MalformedParameter_ThrowsTransportParameterError(string hex)
    {
        Diagnostics.Arrange("transport parameters hex", hex);
        Diagnostics.Bytes("transport parameters", Hex(hex));

        var error = ErrorOf(() => QuicTransportParameters.Decode(Hex(hex)));

        Diagnostics.Act("transport error code", error);
        Diagnostics.Assert("transport error code", QuicTransportErrorCode.TransportParameterError, error);
        Assert.AreEqual(QuicTransportErrorCode.TransportParameterError, error);
    }
}
