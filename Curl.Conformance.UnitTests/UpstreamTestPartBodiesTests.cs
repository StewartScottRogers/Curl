using System.Text;

namespace Curl.Conformance;

/// <summary>Pins how <see cref="UpstreamTestPartBodies"/> reads a part's body for serving and for comparing.</summary>
[TestClass]
public sealed class UpstreamTestPartBodiesTests
{
    [TestMethod]
    public void Text_AbsentPart_IsEmpty()
    {
        Assert.AreEqual(string.Empty, UpstreamTestPartBodies.Text(null));
        Assert.IsEmpty(UpstreamTestPartBodies.Lines(null));
    }

    [TestMethod]
    public void Lines_TrimsAndDropsBlankLines()
    {
        UpstreamTestCase testCase = ParsedTestCase.From("<client>\n<server>\n http \r\n\nfile\n</server>\n</client>\n");

        CollectionAssert.AreEqual(new[] { "http", "file" }, UpstreamTestPartBodies.Lines(testCase.Find("client", "server")));
    }

    [TestMethod]
    [DataRow("", "a\nB: c\n")]
    [DataRow(" crlf=\"yes\"", "a\r\nB: c\r\n")]
    [DataRow(" crlf=\"headers\"", "a\nB: c\r\n")]
    [DataRow(" crlf=\"1\"", "a\nB: c\n")]
    public void Served_ForcesTheLineEndingsPreproForces(string attributes, string expected)
    {
        UpstreamTestSection part = Part($"<data{attributes}>\na\nB: c\n</data>\n");

        Assert.AreEqual(expected, Text(UpstreamTestPartBodies.Served(part)));
    }

    [TestMethod]
    [DataRow("", "a\nB: c\n")]
    [DataRow(" crlf=\"yes\"", "a\r\nB: c\r\n")]
    [DataRow(" crlf=\"1\"", "a\r\nB: c\r\n")]
    [DataRow(" crlf=\"headers\"", "a\nB: c\r\n")]
    public void WithCrlf_ForcesTheLineEndingsTheComparisonForces(string attributes, string expected)
    {
        UpstreamTestSection part = Part($"<data{attributes}>\na\nB: c\n</data>\n");

        Assert.AreEqual(expected, Text(UpstreamTestPartBodies.WithCrlf(part.Content.ToArray(), part)));
    }

    [TestMethod]
    [DataRow("", "a\n")]
    [DataRow(" nonewline=\"yes\"", "a")]
    public void WithoutFinalNewline_CutsOnlyUnderNonewline(string attributes, string expected)
    {
        UpstreamTestSection part = Part($"<data{attributes}>\na\n</data>\n");

        Assert.AreEqual(expected, Text(UpstreamTestPartBodies.WithoutFinalNewline(part.Content.ToArray(), part)));
    }

    private static UpstreamTestSection Part(string text) =>
        ParsedTestCase.From($"<reply>\n{text}</reply>\n").Find("reply", "data")!;

    private static string Text(byte[] bytes) => Encoding.Latin1.GetString(bytes);
}
