using Curl.Api.Codes;
using Libcurl.Info;
using Curl.Engine;
using Libcurl.Easy;
using Libcurl.Options;

namespace Libcurl.UnitTests;

/// <summary>
/// CGR-247: <c>CURLINFO_XFER_ID</c> and <c>CURLINFO_CONN_ID</c> of a new easy handle's first transfer start at 0, as
/// libcurl 8.22.0's do because each <c>curl_easy_perform</c> runs over a multi handle of its own, however many
/// transfers other handles of the library have run.
/// </summary>
[TestClass]
public sealed class Cgr247Tests
{
    [TestMethod]
    public async Task GetInfo_XferIdOfEachNewHandlesFirstTransfer_IsZero()
    {
        string path = Path.Combine(Path.GetTempPath(), $"cgr247-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, "hello");
        try
        {
            await using ManagedCurlEngine engine = new();
            long[] first = await PerformNewHandleAsync(engine, path);
            long[] second = await PerformNewHandleAsync(engine, path);

            CollectionAssert.AreEqual(new long[] { 0 }, first);
            CollectionAssert.AreEqual(new long[] { 0 }, second);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task<long[]> PerformNewHandleAsync(ManagedCurlEngine engine, string path)
    {
        using CurlEasy easy = new(engine.CreateEasy(), EasyOptionDispatch.Default);
        Assert.AreEqual(CurlCode.Ok, easy.SetOption(CurlOption.Url, new Uri(path).AbsoluteUri));
        Assert.AreEqual(CurlCode.Ok, await easy.PerformAsync());
        Assert.AreEqual(CurlCode.Ok, easy.GetInfo(CurlInfo.XferId, out object? xferId));
        return [(long)xferId!];
    }
}
