using Curl.Testing;

namespace Curl.Protocol.Ws;

/// <summary>Pins how one <c>-H</c> entry is read, as curl reads it.</summary>
[TestClass]
public sealed class WsCustomHeaderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("X-A: 1", "X-A: 1")]
    [DataRow("X-A:", null)]
    [DataRow("X-A: \t", null)]
    [DataRow(": 1", null)]
    [DataRow("X-A;", "X-A:")]
    [DataRow("X-A;b", null)]
    [DataRow(";", null)]
    [DataRow("X-A", null)]
    public void Parse_Entry_SendsTheLineCurlSends(string entry, string? sentLine)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("entry", entry);

        string? actual = WsCustomHeader.Parse(entry).SentLine;

        diagnostics.Act("sent line", actual ?? "(null)");
        diagnostics.Assert("sent line", sentLine ?? "(null)", actual ?? "(null)");
        Assert.AreEqual(sentLine, actual);
    }

    [TestMethod]
    [DataRow("Connection:  keep-alive ", "keep-alive")]
    [DataRow("Connection:", null)]
    [DataRow("Connection;", null)]
    public void Value_Entry_IsTheTrimmedValueOfANameValueEntry(string entry, string? value)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("entry", entry);

        string? actual = WsCustomHeader.Parse(entry).Value;

        diagnostics.Act("value", actual ?? "(null)");
        diagnostics.Assert("value", value ?? "(null)", actual ?? "(null)");
        Assert.AreEqual(value, actual);
    }

    [TestMethod]
    [DataRow("connection: a", "Connection", true)]
    [DataRow("Connection;", "Connection", true)]
    [DataRow("Connections: a", "Connection", false)]
    [DataRow("X: a", "Connection", false)]
    public void Names_Entry_MatchesTheNameInAnyCase(string entry, string name, bool names)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("entry", entry);
        diagnostics.Arrange("name", name);

        bool actual = WsCustomHeader.Parse(entry).Names(name);

        diagnostics.Act("names", actual);
        diagnostics.Assert("names", names, actual);
        Assert.AreEqual(names, actual);
    }
}
