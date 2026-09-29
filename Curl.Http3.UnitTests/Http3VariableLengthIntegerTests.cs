using static Curl.Http3.Qpack;

namespace Curl.Http3;

/// <summary>
/// Pins <see cref="Http3VariableLengthInteger" /> against RFC 9000 appendix A.1's examples.
/// </summary>
[TestClass]
public sealed class Http3VariableLengthIntegerTests
{
    [TestMethod]
    [DataRow(37L, "25")]
    [DataRow(15293L, "7bbd")]
    [DataRow(494878333L, "9d7f3e7d")]
    [DataRow(151288809941952652L, "c2197c5eff14e88c")]
    [DataRow(4611686018427387903L, "ffffffffffffffff")]
    public void Write_RfcExamples_GiveTheShortestEncoding(long value, string hex)
    {
        List<byte> output = [0xaa];

        Http3VariableLengthInteger.Write(output, value);

        CollectionAssert.AreEqual(FromHex("aa" + hex), output);
    }

    [TestMethod]
    [DataRow(-1L)]
    [DataRow(4611686018427387904L)]
    public void Write_ValueOutsideTheRange_IsRejected(long value) =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Http3VariableLengthInteger.Write([], value));

    [TestMethod]
    [DataRow("25", 37L)]
    [DataRow("4025", 37L)]
    [DataRow("7bbd", 15293L)]
    [DataRow("9d7f3e7d", 494878333L)]
    [DataRow("c2197c5eff14e88c", 151288809941952652L)]
    public void TryRead_RfcExamples_GiveTheirValues(string hex, long expected)
    {
        var input = FromHex("00" + hex + "00");
        var position = 1;

        Assert.IsTrue(Http3VariableLengthInteger.TryRead(input, ref position, out var value));

        Assert.AreEqual(expected, value);
        Assert.AreEqual(input.Length - 1, position);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("40")]
    [DataRow("9d7f3e")]
    public void TryRead_InputEndingInsideTheValue_ReadsNothing(string hex)
    {
        var position = 0;

        Assert.IsFalse(Http3VariableLengthInteger.TryRead(FromHex(hex), ref position, out _));

        Assert.AreEqual(0, position);
    }
}
