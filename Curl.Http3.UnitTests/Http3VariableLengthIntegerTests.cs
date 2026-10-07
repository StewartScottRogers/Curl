using Curl.Testing;
using static Curl.Http3.Qpack;

namespace Curl.Http3;

/// <summary>
/// Pins <see cref="Http3VariableLengthInteger" /> against RFC 9000 appendix A.1's examples.
/// </summary>
[TestClass]
public sealed class Http3VariableLengthIntegerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(37L, "25")]
    [DataRow(15293L, "7bbd")]
    [DataRow(494878333L, "9d7f3e7d")]
    [DataRow(151288809941952652L, "c2197c5eff14e88c")]
    [DataRow(4611686018427387903L, "ffffffffffffffff")]
    public void Write_RfcExamples_GiveTheShortestEncoding(long value, string hex)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("value", value);
        diagnostics.Arrange("expected encoding", hex);
        List<byte> output = [0xaa];

        Http3VariableLengthInteger.Write(output, value);

        diagnostics.Bytes("output after the 0xaa marker", output.ToArray());
        diagnostics.Act("output length", output.Count);
        diagnostics.Diff("output", FromHex("aa" + hex), output.ToArray());
        CollectionAssert.AreEqual(FromHex("aa" + hex), output);
    }

    [TestMethod]
    [DataRow(-1L)]
    [DataRow(4611686018427387904L)]
    public void Write_ValueOutsideTheRange_IsRejected(long value)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("value", value);

        var failure = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Http3VariableLengthInteger.Write([], value));

        diagnostics.Act("exception", failure.GetType().Name);
        diagnostics.Assert("exception", nameof(ArgumentOutOfRangeException), failure.GetType().Name);
    }

    [TestMethod]
    [DataRow("25", 37L)]
    [DataRow("4025", 37L)]
    [DataRow("7bbd", 15293L)]
    [DataRow("9d7f3e7d", 494878333L)]
    [DataRow("c2197c5eff14e88c", 151288809941952652L)]
    public void TryRead_RfcExamples_GiveTheirValues(string hex, long expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var input = FromHex("00" + hex + "00");
        diagnostics.Arrange("value at position 1", hex);
        diagnostics.Bytes("input", input);
        var position = 1;

        var read = Http3VariableLengthInteger.TryRead(input, ref position, out var value);

        diagnostics.Act("read", read);
        diagnostics.Act("value", value);
        diagnostics.Act("position", position);
        Assert.IsTrue(read);

        diagnostics.Assert("value", expected, value);
        Assert.AreEqual(expected, value);
        diagnostics.Assert("position", input.Length - 1, position);
        Assert.AreEqual(input.Length - 1, position);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("40")]
    [DataRow("9d7f3e")]
    public void TryRead_InputEndingInsideTheValue_ReadsNothing(string hex)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("input", hex);
        var position = 0;

        var read = Http3VariableLengthInteger.TryRead(FromHex(hex), ref position, out _);

        diagnostics.Act("read", read);
        diagnostics.Act("position", position);
        Assert.IsFalse(read);

        diagnostics.Assert("position", 0, position);
        Assert.AreEqual(0, position);
    }
}
