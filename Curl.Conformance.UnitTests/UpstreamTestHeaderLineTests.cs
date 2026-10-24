namespace Curl.Conformance;

[TestClass]
public sealed class UpstreamTestHeaderLineTests
{
    // A status line cut short after its version must be judged, not read past its end (AF-0398).
    [TestMethod]
    [DataRow("HTTP/1.1")]
    [DataRow("HTTP/1.1 ")]
    [DataRow("HTTP/2")]
    [DataRow("HTTP/2 ")]
    [DataRow("HTTP/3")]
    public void LooksLikeHeader_StatusLineCutShortAfterVersion_IsNotAHeaderLine(string line) =>
        Assert.IsFalse(UpstreamTestHeaderLine.LooksLikeHeader(line));

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\n")]
    [DataRow("HTTP/2 200")]
    [DataRow("HTTP/1.0 9")]
    public void LooksLikeHeader_CompleteStatusLine_IsAHeaderLine(string line) =>
        Assert.IsTrue(UpstreamTestHeaderLine.LooksLikeHeader(line));
}
