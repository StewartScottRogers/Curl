using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Api.Options.Proxy;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

// A read-callback upload held to CURLOPT_INFILESIZE, and CURLOPT_HEADEROPT's CONNECT headers (BL-2645, upstream tests 1525 to 1527).
public sealed partial class ManagedEasyTransferTests
{
    [TestMethod]
    public async Task PerformAsync_PutWithInFileSizeAndAReadCallbackThatNeverEnds_SendsInFileSizeBytesWithThatContentLengthAndFinishes()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        int calls = 0;
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/up" },
            Transfer = new EasyTransferOptions { Upload = true, InFileSize = 13 },
            Callbacks = new()
            {
                Read = buffer =>
                {
                    calls++;
                    "Hello Cloud!\n"u8.CopyTo(buffer);
                    return 13;
                },
            },
        };
        using CancellationTokenSource stall = new(TimeSpan.FromSeconds(10));

        CurlCode code = await easy.PerformAsync(stall.Token);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(1, calls);
        string written = Encoding.ASCII.GetString(connector.Written);
        StringAssert.StartsWith(written, "PUT /up HTTP/1.1\r\n");
        StringAssert.EndsWith(written, "Content-Length: 13\r\n\r\nHello Cloud!\n");
    }

    // Upstream test 1576's shape (BL-2659): a custom method, Expect removed, and a callback answering 45 bytes on every call.
    [TestMethod]
    public async Task PerformAsync_CustomRequestUploadWithInFileSizeAndACallbackThatNeverEnds_StopsAtTheUploadSize()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        byte[] chunk = Encoding.ASCII.GetBytes(new string('x', 45));
        int calls = 0;
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/1576" },
            Http = new EasyHttpOptions { HttpHeaders = ["Expect:"] },
            Transfer = new EasyTransferOptions { Upload = true, InFileSize = 45, CustomRequest = "CURL" },
            Callbacks = new()
            {
                Read = buffer =>
                {
                    calls++;
                    chunk.CopyTo(buffer);
                    return chunk.Length;
                },
            },
        };
        using CancellationTokenSource stall = new(TimeSpan.FromSeconds(10));

        CurlCode code = await easy.PerformAsync(stall.Token);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(1, calls);
        string written = Encoding.ASCII.GetString(connector.Written);
        StringAssert.StartsWith(written, "CURL /1576 HTTP/1.1\r\n");
        Assert.DoesNotContain("Expect:", written);
        StringAssert.EndsWith(written, "Content-Length: 45\r\n\r\n" + new string('x', 45));
    }

    [TestMethod]
    public async Task PerformAsync_PutWithZeroInFileSize_SendsAnEmptyBodyWithoutAskingTheReadCallback()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/up" },
            Transfer = new EasyTransferOptions { Upload = true, InFileSize = 0 },
            Callbacks = new() { Read = _ => throw new AssertFailedException("the read callback was asked") },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        StringAssert.EndsWith(Encoding.ASCII.GetString(connector.Written), "Content-Length: 0\r\n\r\n");
    }

    [TestMethod]
    [DataRow("http://h/x", 13L, 13L)]
    [DataRow("HTTPS://h/x", 0L, 0L)]
    [DataRow("http://h/x", -1L, null)]
    [DataRow("ftp://h/x", 13L, null)]
    [DataRow("ws://h/x", 13L, null)]
    public void DeclaredUploadLengthOf_SchemeAndInFileSize_HoldsOnlyAnHttpUploadOfKnownSize(string url, long inFileSize, long? expected) =>
        Assert.AreEqual(
            expected,
            ManagedEasyTransfer.DeclaredUploadLengthOf(new EasyOptions { Transfer = new EasyTransferOptions { InFileSize = inFileSize } }, CurlUrl.Parse(url)));

    [TestMethod]
    [DataRow(HeaderOption.Separate, 1)]
    [DataRow(HeaderOption.Unified, 0)]
    public void TransferSettingsOf_HeaderOption_ForwardsTheProxyListOnlyWhenSeparate(HeaderOption option, int expected)
    {
        EasyOptions options = new() { Proxy = new EasyProxyOptions { ProxyHeaders = ["P: 1"], HeaderOption = option } };

        EngineTransferSettings settings = ManagedEasyTransfer.TransferSettingsOf(options);

        Assert.HasCount(expected, settings.ProxyHeaders);
    }
}
