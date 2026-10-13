using System.Text;
using Curl.Testing;

namespace Curl.Conformance;

/// <summary>Pins how <see cref="UpstreamFirstDifference"/> names where an output first differs.</summary>
[TestClass]
public sealed class UpstreamFirstDifferenceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Describe_EqualBytes_ReturnsNull()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("expected and actual", "same\\n");

        string? difference = UpstreamFirstDifference.Describe("x", Bytes("same\n"), Bytes("same\n"));

        diagnostics.Act("difference", difference);
        diagnostics.Assert("difference", null, difference);
        Assert.IsNull(difference);
    }

    [TestMethod]
    public void Describe_NamesTheOffsetLineAndBothLinesThenTheLineDiff()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("expected", "one\\ntwo\\r\\nthree");
        diagnostics.Arrange("actual", "one\\ntwX\\r\\n");

        string? difference = UpstreamFirstDifference.Describe("<verify><stdout>", Bytes("one\ntwo\r\nthree"), Bytes("one\ntwX\r\n"));

        const string expectedDifference = "<verify><stdout> differs at byte 6 (line 2): expected \"two\\r\\n\", got \"twX\\r\\n\"\n"
            + "  1: \"one\\n\"\n"
            + "- 2: \"two\\r\\n\"\n"
            + "- 3: \"three\"\n"
            + "+ 2: \"twX\\r\\n\"\n";
        diagnostics.Act("difference", difference);
        diagnostics.Diff("difference", expectedDifference, difference ?? string.Empty);
        Assert.AreEqual(expectedDifference, difference);
    }

    [TestMethod]
    public void Describe_ShortOutput_SaysItEnded()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("expected", "abc");
        diagnostics.Arrange("actual", "ab");

        string? difference = UpstreamFirstDifference.Describe("p", Bytes("abc"), Bytes("ab"));

        diagnostics.Act("difference", FirstLine(difference));
        diagnostics.Diff("difference", "p differs at byte 2 (line 1): expected \"abc\", got the end", FirstLine(difference) ?? string.Empty);
        Assert.AreEqual("p differs at byte 2 (line 1): expected \"abc\", got the end", FirstLine(difference));
    }

    [TestMethod]
    public void Describe_EscapesCharactersThatAreNotPrintable()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] expectedBytes = Bytes("\t\"\\\u0001é");
        diagnostics.Bytes("expected", expectedBytes);
        diagnostics.Arrange("actual", "(empty)");

        string? difference = UpstreamFirstDifference.Describe("p", expectedBytes, []);

        diagnostics.Act("difference", FirstLine(difference));
        diagnostics.Diff("difference", "p differs at byte 0 (line 1): expected \"\\t\\\"\\\\\\x01\\xE9\", got the end", FirstLine(difference) ?? string.Empty);
        Assert.AreEqual("p differs at byte 0 (line 1): expected \"\\t\\\"\\\\\\x01\\xE9\", got the end", FirstLine(difference));
    }

    [TestMethod]
    public void Describe_LongLine_IsCut()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("expected", "300 times 'a'");
        diagnostics.Arrange("actual", "b");

        string? difference = UpstreamFirstDifference.Describe("p", Bytes(new string('a', 300)), Bytes("b"));

        string expectedDifference = $"p differs at byte 0 (line 1): expected \"{new string('a', 200)}\"..., got \"b\"";
        diagnostics.Act("difference", FirstLine(difference));
        diagnostics.Diff("difference", expectedDifference, FirstLine(difference) ?? string.Empty);
        Assert.AreEqual(expectedDifference, FirstLine(difference));
    }

    // The sentence naming the first difference; the line diff after it is pinned in UpstreamLineDifferenceTests.
    private static string? FirstLine(string? difference) => difference?.Split('\n')[0];

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);
}
