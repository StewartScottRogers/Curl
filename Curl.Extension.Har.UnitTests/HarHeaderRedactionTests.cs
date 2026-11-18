using System.Text;
using System.Text.Json;

namespace Curl.Extension.Har.UnitTests;

[TestClass]
public sealed class HarHeaderRedactionTests
{
    private static JsonElement EntryOf(string referer, params (string Name, string Value)[] responseFields)
    {
        TransferReport report = new()
        {
            ResponseCode = 302,
            HttpVersion = new Version(1, 1),
            EffectiveUrl = "http://u:URLSECRET@example.com/r",
            RedirectUrl = "http://REDACTED:REDACTED@example.com/new",
            ResponseHeaders = [.. responseFields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value))],
        };
        HarExchange exchange = new(
            DateTimeOffset.UnixEpoch,
            "GET",
            HarTransferHook.Redacted(report.EffectiveUrl),
            [new("Referer", referer), new("X-Other", "http://a:b@h/")],
            null,
            TransferResult.Success(0) with { Report = report },
            [],
            1000);
        using var document = JsonDocument.Parse(HarDocument.Write([exchange]));
        return document.RootElement.GetProperty("log").GetProperty("entries")[0].Clone();
    }

    private static string ValueOf(JsonElement headers, string name) =>
        headers.EnumerateArray().First(header => header.GetProperty("name").GetString() == name).GetProperty("value").GetString()!;

    [TestMethod]
    public void Write_LocationAndContentLocationWithUserInfo_AreWrittenRedacted()
    {
        var headers = EntryOf("http://example.com/", ("Location", "http://u:LOCSECRET8@example.com/plain"), ("content-location", "http://cu:CLSECRET9@example.com/x"))
            .GetProperty("response").GetProperty("headers");

        Assert.AreEqual("http://REDACTED:REDACTED@example.com/plain", ValueOf(headers, "Location"));
        Assert.AreEqual("http://REDACTED:REDACTED@example.com/x", ValueOf(headers, "content-location"));
    }

    [TestMethod]
    public void Write_RefererWithUserInfo_IsWrittenRedacted()
    {
        var headers = EntryOf("http://r:REFSECRET@example.com/p").GetProperty("request").GetProperty("headers");

        Assert.AreEqual("http://REDACTED:REDACTED@example.com/p", ValueOf(headers, "Referer"));
    }

    [TestMethod]
    public void Write_HeadersWithoutUserInfoAndOtherNames_AreWrittenUnchanged_AndUrlsStayRedacted()
    {
        var entry = EntryOf("http://example.com/", ("Location", "http://example.com/plain"), ("X-Link", "http://a:b@h/"));

        Assert.AreEqual("http://example.com/plain", ValueOf(entry.GetProperty("response").GetProperty("headers"), "Location"));
        Assert.AreEqual("http://a:b@h/", ValueOf(entry.GetProperty("response").GetProperty("headers"), "X-Link"));
        Assert.AreEqual("http://a:b@h/", ValueOf(entry.GetProperty("request").GetProperty("headers"), "X-Other"));
        Assert.AreEqual("http://u:URLSECRET@example.com/r".Replace("u:URLSECRET", "REDACTED:REDACTED"), entry.GetProperty("request").GetProperty("url").GetString());
        Assert.AreEqual("http://REDACTED:REDACTED@example.com/new", entry.GetProperty("response").GetProperty("redirectURL").GetString());
    }
}
