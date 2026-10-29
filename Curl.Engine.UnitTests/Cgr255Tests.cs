using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;

namespace Curl.Engine;

/// <summary>
/// CGR-255: on Windows a drive letter such as <c>c:/dir/file</c> set under <c>CURLU_DEFAULT_SCHEME</c> is the
/// host, not a scheme, as libcurl's <c>Curl_is_absolute_url</c> reads it there.
/// </summary>
[TestClass]
public sealed class Cgr255Tests
{
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Set_DriveLetterUnderDefaultScheme_IsTheHost()
    {
        using ManagedCurlUrlHandle url = new();

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Url, "c:/dir/file", CurlUrlFlags.DefaultScheme));

        Assert.AreEqual(CurlUrlCode.Ok, url.Get(CurlUrlPart.Url, CurlUrlFlags.None, out string? text));
        Assert.AreEqual("https://c/dir/file", text);
        Assert.AreEqual(CurlUrlCode.Ok, url.Get(CurlUrlPart.Host, CurlUrlFlags.None, out string? host));
        Assert.AreEqual("c", host);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Set_DriveLetterAndSlashUnderDefaultScheme_IsTheHost()
    {
        using ManagedCurlUrlHandle url = new();

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Url, "x:/", CurlUrlFlags.DefaultScheme));

        Assert.AreEqual(CurlUrlCode.Ok, url.Get(CurlUrlPart.Host, CurlUrlFlags.None, out string? host));
        Assert.AreEqual("x", host);
    }
}
