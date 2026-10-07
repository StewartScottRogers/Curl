using System.Text;
using Curl.Testing;

namespace Curl.Conformance;

/// <summary>Pins how <see cref="UpstreamTestPartBodies"/> reads a part's body for serving and for comparing.</summary>
[TestClass]
public sealed class UpstreamTestPartBodiesTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Text_AbsentPart_IsEmpty()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("part", "null");

        string text = UpstreamTestPartBodies.Text(null);
        var lines = UpstreamTestPartBodies.Lines(null);

        diagnostics.Act("text", text);
        diagnostics.Act("line count", lines.Length);
        diagnostics.Assert("text", string.Empty, text);
        diagnostics.Assert("line count", 0, lines.Length);
        Assert.AreEqual(string.Empty, UpstreamTestPartBodies.Text(null));
        Assert.IsEmpty(UpstreamTestPartBodies.Lines(null));
    }

    [TestMethod]
    public void Lines_TrimsAndDropsBlankLines()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const string testFile = "<client>\n<server>\n http \r\n\nfile\n</server>\n</client>\n";
        diagnostics.Arrange("test file", testFile);
        UpstreamTestCase testCase = ParsedTestCase.From(testFile);

        var lines = UpstreamTestPartBodies.Lines(testCase.Find("client", "server"));

        diagnostics.Act("lines", string.Join(" | ", lines));
        diagnostics.Assert("lines", "http | file", string.Join(" | ", lines));
        CollectionAssert.AreEqual(new[] { "http", "file" }, UpstreamTestPartBodies.Lines(testCase.Find("client", "server")));
    }

    [TestMethod]
    [DataRow("", "a\nB: c\n")]
    [DataRow(" crlf=\"yes\"", "a\r\nB: c\r\n")]
    [DataRow(" crlf=\"headers\"", "a\nB: c\r\n")]
    [DataRow(" crlf=\"1\"", "a\nB: c\n")]
    public void Served_ForcesTheLineEndingsPreproForces(string attributes, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("attributes", attributes);
        UpstreamTestSection part = Part($"<data{attributes}>\na\nB: c\n</data>\n");

        string actual = Text(UpstreamTestPartBodies.Served(part));

        diagnostics.Act("served body", actual);
        diagnostics.Diff("served body", expected, actual);
        Assert.AreEqual(expected, Text(UpstreamTestPartBodies.Served(part)));
    }

    [TestMethod]
    [DataRow("", "a\nB: c\n")]
    [DataRow(" crlf=\"yes\"", "a\r\nB: c\r\n")]
    [DataRow(" crlf=\"1\"", "a\r\nB: c\r\n")]
    [DataRow(" crlf=\"headers\"", "a\nB: c\r\n")]
    public void WithCrlf_ForcesTheLineEndingsTheComparisonForces(string attributes, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("attributes", attributes);
        UpstreamTestSection part = Part($"<data{attributes}>\na\nB: c\n</data>\n");

        string actual = Text(UpstreamTestPartBodies.WithCrlf(part.Content.ToArray(), part));

        diagnostics.Act("compared body", actual);
        diagnostics.Diff("compared body", expected, actual);
        Assert.AreEqual(expected, Text(UpstreamTestPartBodies.WithCrlf(part.Content.ToArray(), part)));
    }

    [TestMethod]
    [DataRow("", "a\n")]
    [DataRow(" nonewline=\"yes\"", "a")]
    public void WithoutFinalNewline_CutsOnlyUnderNonewline(string attributes, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("attributes", attributes);
        UpstreamTestSection part = Part($"<data{attributes}>\na\n</data>\n");

        string actual = Text(UpstreamTestPartBodies.WithoutFinalNewline(part.Content.ToArray(), part));

        diagnostics.Act("body", actual);
        diagnostics.Diff("body", expected, actual);
        Assert.AreEqual(expected, Text(UpstreamTestPartBodies.WithoutFinalNewline(part.Content.ToArray(), part)));
    }

    private static UpstreamTestSection Part(string text) =>
        ParsedTestCase.From($"<reply>\n{text}</reply>\n").Find("reply", "data")!;

    private static string Text(byte[] bytes) => Encoding.Latin1.GetString(bytes);
}
