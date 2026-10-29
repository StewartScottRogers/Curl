using System.Text;
using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;

namespace Curl.Engine;

/// <summary>
/// CGR-258: <c>CURLOPT_COOKIEJAR</c> is written at <c>curl_easy_cleanup</c>, not when each perform ends.
/// </summary>
public sealed partial class ManagedEasyTransferTests
{
    [TestMethod]
    public async Task PerformAsync_CookieJar_IsAbsentAfterPerformAndWrittenAtCleanup()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(SetsCookie)]);
        MemoryFileSystem files = new();
        ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://example.com/x" },
            Http = new EasyHttpOptions { CookieJar = "out.txt" },
        };
        easy.Host = HostOf(files);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);
        bool presentAfterPerform = files.Files.ContainsKey("out.txt");
        await easy.DisposeAsync();

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.IsFalse(presentAfterPerform);
        StringAssert.Contains(Encoding.ASCII.GetString(files.Files["out.txt"]), "\tgot\t2");
    }
}
