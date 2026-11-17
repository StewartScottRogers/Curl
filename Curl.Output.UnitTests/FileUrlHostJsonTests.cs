using Curl.Api.Info;
using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// BL-5340: <c>%{json}</c> reports <c>url.host</c> and <c>urle.host</c> as <c>null</c> when the URL has
/// no host (a <c>file:///</c> URL), as curl does, while a plain <c>%{url.host}</c> still writes nothing.
/// </summary>
[TestClass]
public sealed class FileUrlHostJsonTests
{
    [TestMethod]
    [DataRow("file:///etc/hostname")]
    [DataRow("file://localhost/etc/hostname")]
    [DataRow("file:///no/such/file")]
    public void TryGetVariableText_JsonOfFileUrl_ReportsHostAsNull(string url)
    {
        string json = Get(url, "json");

        StringAssert.Contains(json, "\"url.host\":null");
        StringAssert.Contains(json, "\"urle.host\":null");
    }

    [TestMethod]
    [DataRow("file:///etc/hostname")]
    [DataRow("file://localhost/etc/hostname")]
    [DataRow("file:///no/such/file")]
    public void TryGetVariableText_PlainHostOfFileUrl_IsEmpty(string url)
    {
        Assert.AreEqual(string.Empty, Get(url, "url.host"));
        Assert.AreEqual(string.Empty, Get(url, "urle.host"));
    }

    [TestMethod]
    public void TryGetVariableText_JsonOfHttpUrl_StillReportsTheHost()
    {
        string json = Get("http://example.com/", "json");

        StringAssert.Contains(json, "\"url.host\":\"example.com\"");
        StringAssert.Contains(json, "\"urle.host\":\"example.com\"");
    }

    private static string Get(string url, string name)
    {
        TransferWriteOutVariables variables = new(new TransferInfo { EffectiveUrl = url }, TransferResult.Success(0), url, 0);
        Assert.IsTrue(variables.TryGetVariableText(name, out string? text), name);
        return text ?? string.Empty;
    }
}
