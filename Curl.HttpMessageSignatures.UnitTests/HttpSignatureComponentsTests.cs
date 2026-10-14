namespace Curl.HttpMessageSignatures;

/// <summary>Checks the <c>--httpsig-headers</c> parser against curl 8.22.0's <c>parse_components</c>.</summary>
[TestClass]
public sealed class HttpSignatureComponentsTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Parse_NoText_ReturnsMethodAuthorityPath(string? text)
    {
        var components = HttpSignatureComponents.Parse(text, urlHasQuery: false);

        CollectionAssert.AreEqual(new[] { "@method", "@authority", "@path" }, components.ToArray());
    }

    [TestMethod]
    public void Parse_NoTextAndAQuery_AddsQuery()
    {
        var components = HttpSignatureComponents.Parse(null, urlHasQuery: true);

        CollectionAssert.AreEqual(new[] { "@method", "@authority", "@path", "@query" }, components.ToArray());
    }

    [TestMethod]
    public void Parse_MixedCaseNamesAndBlanks_ReturnsLowerCaseIdentifiersInOrder()
    {
        var components = HttpSignatureComponents.Parse(" \tQuery  Content-Type: PATH\tmethod authority X-É: ", urlHasQuery: false);

        CollectionAssert.AreEqual(new[] { "@query", "content-type", "@path", "@method", "@authority", "x-É" }, components.ToArray());
    }

    [TestMethod]
    public void Parse_SixteenComponents_ReturnsAllSixteen()
    {
        var text = string.Join(' ', Enumerable.Range(0, 16).Select(index => $"h{index}:")) + " ";

        var components = HttpSignatureComponents.Parse(text, urlHasQuery: false);

        Assert.HasCount(16, components);
    }

    [TestMethod]
    [DataRow("method \"x\"", HttpSignatureException.BadArgumentMessage)]
    [DataRow("a\\b:", HttpSignatureException.BadArgumentMessage)]
    [DataRow("method Status", "httpsig: unknown component 'status'")]
    [DataRow("@method", "httpsig: unknown component '@method'")]
    [DataRow("method :", "httpsig: empty header component name")]
    [DataRow(" \t ", "httpsig: no signature components specified")]
    [DataRow("path Date: PATH", "httpsig: duplicate signature component '@path'")]
    [DataRow("date: DATE:", "httpsig: duplicate signature component 'date'")]
    public void Parse_BadText_ThrowsCurlsMessage(string text, string message)
    {
        var exception = Assert.ThrowsExactly<HttpSignatureException>(() => HttpSignatureComponents.Parse(text, urlHasQuery: false));

        Assert.AreEqual(message, exception.Message);
    }

    [TestMethod]
    [DataRow(" h16:")]
    [DataRow("  ")]
    public void Parse_TextLeftAfterSixteenComponents_ThrowsTooMany(string rest)
    {
        var text = string.Join(' ', Enumerable.Range(0, 16).Select(index => $"h{index}:")) + rest;

        var exception = Assert.ThrowsExactly<HttpSignatureException>(() => HttpSignatureComponents.Parse(text, urlHasQuery: false));

        Assert.AreEqual("httpsig: too many signature components (max 16)", exception.Message);
    }
}
