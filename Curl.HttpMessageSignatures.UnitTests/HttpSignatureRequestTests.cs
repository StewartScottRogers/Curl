namespace Curl.HttpMessageSignatures;

/// <summary>Checks how a request's derived values and <c>-H</c> header values are taken for signing.</summary>
[TestClass]
public sealed class HttpSignatureRequestTests
{
    [TestMethod]
    [DataRow("", "/")]
    [DataRow("/a/b", "/a/b")]
    public void SignedPath_Path_IsSlashWhenEmpty(string path, string expected)
    {
        var request = new HttpSignatureRequest("GET", "h", path, null, []);

        Assert.AreEqual(expected, request.SignedPath);
    }

    [TestMethod]
    [DataRow(null, "?")]
    [DataRow("", "?")]
    [DataRow("a=1&b", "?a=1&b")]
    public void SignedQuery_Query_StartsWithQuestionMark(string? query, string expected)
    {
        var request = new HttpSignatureRequest("GET", "h", "/", query, []);

        Assert.AreEqual(expected, request.SignedQuery);
    }

    [TestMethod]
    [DataRow("signature: x", true)]
    [DataRow("SIGNATURE;", true)]
    [DataRow("Signature-Input: x", false)]
    [DataRow("Signature", false)]
    [DataRow("Sig", false)]
    [DataRow("X-Signature: x", false)]
    public void HasHeader_Line_MatchesNameThenColonOrSemicolon(string line, bool expected)
    {
        var request = new HttpSignatureRequest("GET", "h", "/", null, [line]);

        Assert.AreEqual(expected, request.HasHeader("Signature"));
    }

    [TestMethod]
    public void HeaderValue_RepeatedHeader_JoinsTrimmedValuesWithCommaSpace()
    {
        var request = new HttpSignatureRequest("GET", "h", "/", null, ["Accept: \t text/html \t", "X-Other: 1", "accept;", "ACCEPT:a: b", "Accept:"]);

        var value = request.HeaderValue("accept");

        Assert.AreEqual("text/html, a: b, ", value);
    }

    [TestMethod]
    public void HeaderValue_OnlyAnEmptyHeaderLine_ThrowsNotFound()
    {
        var request = new HttpSignatureRequest("GET", "h", "/", null, ["Accept;"]);

        var exception = Assert.ThrowsExactly<HttpSignatureException>(() => request.HeaderValue("accept"));

        Assert.AreEqual("httpsig: header 'accept' not found in request", exception.Message);
    }
}
