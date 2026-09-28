namespace Curl.Protocol.Ws;

/// <summary>Pins how one <c>-H</c> entry is read, as curl reads it.</summary>
[TestClass]
public sealed class WsCustomHeaderTests
{
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
        Assert.AreEqual(sentLine, WsCustomHeader.Parse(entry).SentLine);
    }

    [TestMethod]
    [DataRow("Connection:  keep-alive ", "keep-alive")]
    [DataRow("Connection:", null)]
    [DataRow("Connection;", null)]
    public void Value_Entry_IsTheTrimmedValueOfANameValueEntry(string entry, string? value)
    {
        Assert.AreEqual(value, WsCustomHeader.Parse(entry).Value);
    }

    [TestMethod]
    [DataRow("connection: a", "Connection", true)]
    [DataRow("Connection;", "Connection", true)]
    [DataRow("Connections: a", "Connection", false)]
    [DataRow("X: a", "Connection", false)]
    public void Names_Entry_MatchesTheNameInAnyCase(string entry, string name, bool names)
    {
        Assert.AreEqual(names, WsCustomHeader.Parse(entry).Names(name));
    }
}
