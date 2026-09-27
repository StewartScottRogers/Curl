using System.Text;

namespace Curl.Conformance;

/// <summary>Pins how <see cref="UpstreamFirstDifference"/> names where an output first differs.</summary>
[TestClass]
public sealed class UpstreamFirstDifferenceTests
{
    [TestMethod]
    public void Describe_EqualBytes_ReturnsNull()
    {
        Assert.IsNull(UpstreamFirstDifference.Describe("x", Bytes("same\n"), Bytes("same\n")));
    }

    [TestMethod]
    public void Describe_NamesTheOffsetLineAndBothLines()
    {
        string? difference = UpstreamFirstDifference.Describe("<verify><stdout>", Bytes("one\ntwo\r\nthree"), Bytes("one\ntwX\r\n"));

        Assert.AreEqual("<verify><stdout> differs at byte 6 (line 2): expected \"two\\r\\n\", got \"twX\\r\\n\"", difference);
    }

    [TestMethod]
    public void Describe_ShortOutput_SaysItEnded()
    {
        string? difference = UpstreamFirstDifference.Describe("p", Bytes("abc"), Bytes("ab"));

        Assert.AreEqual("p differs at byte 2 (line 1): expected \"abc\", got the end", difference);
    }

    [TestMethod]
    public void Describe_EscapesCharactersThatAreNotPrintable()
    {
        string? difference = UpstreamFirstDifference.Describe("p", Bytes("\t\"\\\u0001é"), []);

        Assert.AreEqual("p differs at byte 0 (line 1): expected \"\\t\\\"\\\\\\x01\\xE9\", got the end", difference);
    }

    [TestMethod]
    public void Describe_LongLine_IsCut()
    {
        string? difference = UpstreamFirstDifference.Describe("p", Bytes(new string('a', 300)), Bytes("b"));

        Assert.AreEqual($"p differs at byte 0 (line 1): expected \"{new string('a', 200)}\"..., got \"b\"", difference);
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);
}
