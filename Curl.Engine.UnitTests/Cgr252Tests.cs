using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Network;

namespace Curl.Engine;

// CGR-252 (BL-3781): a header callback that aborts on the status line leaves getinfo reporting nothing
// of the response, as libcurl 8.22.0 does: HEADER_SIZE 0, CONTENT_TYPE NULL, CONTENT_LENGTH_DOWNLOAD -1.
public sealed partial class ManagedEasyTransferTests
{
    [TestMethod]
    public async Task PerformAsync_HeaderCallbackAbortsOnTheStatusLine_ReportsNoParsedHeaders()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Callbacks = new() { Header = _ => 1 },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.WriteError, code);
        Assert.AreEqual(0, easy.Info.HeaderSize);
        Assert.IsNull(easy.Info.ContentType);
        Assert.AreEqual(-1, easy.Info.ContentLengthDownload);
    }
}
