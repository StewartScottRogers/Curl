namespace Curl.Protobuf.Wire;

/// <summary>Pins <see cref="ProtobufZigZag" /> to the table in the Protocol Buffers encoding specification.</summary>
[TestClass]
public sealed class ProtobufZigZagTests
{
    [TestMethod]
    [DataRow(0, 0U)]
    [DataRow(-1, 1U)]
    [DataRow(1, 2U)]
    [DataRow(-2, 3U)]
    [DataRow(0x7FFFFFFF, 0xFFFFFFFEU)]
    [DataRow(int.MinValue, 0xFFFFFFFFU)]
    public void Encode32_SpecificationTable_RoundTrips(int value, uint encoded)
    {
        var actual = ProtobufZigZag.Encode32(value);

        Assert.AreEqual(encoded, actual);
        Assert.AreEqual(value, ProtobufZigZag.Decode32(actual));
    }

    [TestMethod]
    [DataRow(0L, 0UL)]
    [DataRow(-1L, 1UL)]
    [DataRow(1L, 2UL)]
    [DataRow(-2L, 3UL)]
    [DataRow(long.MaxValue, 0xFFFFFFFFFFFFFFFEUL)]
    [DataRow(long.MinValue, 0xFFFFFFFFFFFFFFFFUL)]
    public void Encode64_SpecificationTable_RoundTrips(long value, ulong encoded)
    {
        var actual = ProtobufZigZag.Encode64(value);

        Assert.AreEqual(encoded, actual);
        Assert.AreEqual(value, ProtobufZigZag.Decode64(actual));
    }
}
