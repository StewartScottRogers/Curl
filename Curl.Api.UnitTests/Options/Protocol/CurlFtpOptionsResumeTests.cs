using Curl.Api.Messages;
using Curl.Api.Options;
using Curl.Api.Options.Protocols;

namespace Curl.Api.UnitTests.Options.Protocol;

[TestClass]
public sealed class CurlFtpOptionsResumeTests
{
    private static async Task<EasyOptions> PerformedOptions(CurlClientOptions clientOptions, CurlRequest request)
    {
        ScriptedEasyTransfer easy = new();
        await using CurlClient client = new(new ScriptedCurlEngine(easy), clientOptions, ownsEngine: true);
        await client.SendAsync(request, CancellationToken.None);
        return easy.PerformedOptions!;
    }

    [TestMethod]
    public async Task Request_ResumeFromAndListOnly_ReachTheTransferOptions()
    {
        CurlRequest request = new(HttpMethod.Get, "ftp://example/dir/")
        {
            Ftp = new CurlFtpOptions { ResumeFrom = 5, ListOnly = true },
        };

        EasyOptions options = await PerformedOptions(new CurlClientOptions(), request);

        Assert.AreEqual(5, options.Transfer.ResumeFrom);
        Assert.IsTrue(options.Transfer.DirListOnly);
    }

    [TestMethod]
    public async Task Client_ResumeFromMinusOne_ReachesTheTransferOptions()
    {
        CurlClientOptions clientOptions = new() { Ftp = new CurlFtpOptions { ResumeFrom = -1 } };

        EasyOptions options = await PerformedOptions(clientOptions, new CurlRequest(HttpMethod.Get, "ftp://example/f"));

        Assert.AreEqual(-1, options.Transfer.ResumeFrom);
        Assert.IsFalse(options.Transfer.DirListOnly);
    }

    [TestMethod]
    public async Task NoFtpRecord_LeavesTheTransferOptionsAlone()
    {
        EasyOptions options = await PerformedOptions(new CurlClientOptions(), new CurlRequest(HttpMethod.Get, "http://example/"));

        Assert.AreEqual(0, options.Transfer.ResumeFrom);
        Assert.IsFalse(options.Transfer.DirListOnly);
    }

    [TestMethod]
    public async Task Client_ResumeFrom_ReachesAnHttpRequestToo()
    {
        CurlClientOptions clientOptions = new() { Ftp = new CurlFtpOptions { ResumeFrom = 500 } };

        EasyOptions options = await PerformedOptions(clientOptions, new CurlRequest(HttpMethod.Get, "http://example/"));

        Assert.AreEqual(500, options.Transfer.ResumeFrom);
    }

    [TestMethod]
    public async Task RequestFtpRecord_ReplacesTheClientsWhole_ResettingResumeFrom()
    {
        CurlClientOptions clientOptions = new() { Ftp = new CurlFtpOptions { ResumeFrom = 500 } };
        CurlRequest request = new(HttpMethod.Get, "ftp://example/f")
        {
            Ftp = new CurlFtpOptions { FileMethod = FtpFileMethod.NoCwd },
        };

        EasyOptions options = await PerformedOptions(clientOptions, request);

        Assert.AreEqual(0, options.Transfer.ResumeFrom);
    }

    [TestMethod]
    public void ResumeFrom_BelowMinusOne_Throws() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = new CurlFtpOptions { ResumeFrom = -2 });
}
