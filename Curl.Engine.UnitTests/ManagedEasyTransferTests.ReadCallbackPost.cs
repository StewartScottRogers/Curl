using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;

namespace Curl.Engine;

// CURLOPT_POST with no CURLOPT_POSTFIELDS, its body read through CURLOPT_READFUNCTION (BL-2586, upstream test508).
public sealed partial class ManagedEasyTransferTests
{
    [TestMethod]
    public async Task PerformAsync_PostWithPostFieldSizeAndAReadCallback_PostsTheCallbacksBytesWithThatContentLength()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();

        CurlCode code = await PerformReadCallbackPostAsync(easy, new EasyHttpOptions { Post = true, PostFieldSize = 5 }, new EasyTransferOptions());

        Assert.AreEqual(CurlCode.Ok, code);
        string written = Encoding.ASCII.GetString(connector.Written);
        StringAssert.StartsWith(written, "POST /p HTTP/1.1\r\n");
        StringAssert.Contains(written, "Content-Length: 5\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nhello");
    }

    [TestMethod]
    public async Task PerformAsync_PostWithNoPostFieldSizeAndAReadCallback_PostsTheCallbacksBytesChunked()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n"), Encoding.ASCII.GetBytes(Response)]);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();

        CurlCode code = await PerformReadCallbackPostAsync(easy, new EasyHttpOptions { Post = true }, new EasyTransferOptions());

        Assert.AreEqual(CurlCode.Ok, code);
        string written = Encoding.ASCII.GetString(connector.Written);
        StringAssert.StartsWith(written, "POST /p HTTP/1.1\r\n");
        StringAssert.Contains(written, "Transfer-Encoding: chunked\r\n");
        Assert.AreEqual("hello", Dechunked(written[(written.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..]));
    }

    [TestMethod]
    public async Task PerformAsync_PostWithAPostFieldSizeOfZeroAndAReadCallback_PostsWithContentLengthZeroNotChunked()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();

        CurlCode code = await PerformReadCallbackPostAsync(easy, new EasyHttpOptions { Post = true, PostFieldSize = 0 }, new EasyTransferOptions());

        Assert.AreEqual(CurlCode.Ok, code);
        string written = Encoding.ASCII.GetString(connector.Written);
        StringAssert.Contains(written, "Content-Length: 0\r\n");
        Assert.IsFalse(written.Contains("Transfer-Encoding", StringComparison.OrdinalIgnoreCase), written);
    }

    [TestMethod]
    public async Task PerformAsync_PostUnderUploadWithAReadCallback_UploadsWithPut()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();

        CurlCode code = await PerformReadCallbackPostAsync(easy, new EasyHttpOptions { Post = true, PostFieldSize = 5 }, new EasyTransferOptions { Upload = true, InFileSize = 5 });

        Assert.AreEqual(CurlCode.Ok, code);
        StringAssert.StartsWith(Encoding.ASCII.GetString(connector.Written), "PUT /p HTTP/1.1\r\n");
    }

    [TestMethod]
    public async Task PerformAsync_PostWithPostFieldsAndAReadCallback_PostsThePostFields()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();

        CurlCode code = await PerformReadCallbackPostAsync(easy, new EasyHttpOptions { Post = true, PostFields = Encoding.ASCII.GetBytes("a=1") }, new EasyTransferOptions());

        Assert.AreEqual(CurlCode.Ok, code);
        StringAssert.EndsWith(Encoding.ASCII.GetString(connector.Written), "\r\n\r\na=1");
    }

    [TestMethod]
    public async Task PerformAsync_PostWithNoReadCallbackAndNoPostFields_PostsAnEmptyChunkedBody()
    {
        // libcurl's default read callback reads an empty stdin: a chunked POST that asks for 100-continue (BL-3319).
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n"), Encoding.ASCII.GetBytes(Response)]);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/p" }, Http = new EasyHttpOptions { Post = true } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        string written = Encoding.ASCII.GetString(connector.Written);
        Assert.AreEqual(CurlCode.Ok, code);
        StringAssert.StartsWith(written, "POST /p HTTP/1.1\r\n");
        StringAssert.Contains(written, "Transfer-Encoding: chunked\r\n");
        StringAssert.Contains(written, "Content-Type: application/x-www-form-urlencoded\r\n");
        StringAssert.Contains(written, "Expect: 100-continue\r\n");
        StringAssert.EndsWith(written, "\r\n\r\n0\r\n\r\n");
    }

    [TestMethod]
    public async Task PerformAsync_PostWithNoPostFieldsAPostFieldSizeOfZeroAndNoReadCallback_PostsAnEmptyBodyWithContentLengthZero()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/p" }, Http = new EasyHttpOptions { Post = true, PostFieldSize = 0 } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        string written = Encoding.ASCII.GetString(connector.Written);
        StringAssert.StartsWith(written, "POST /p HTTP/1.1\r\n");
        StringAssert.EndsWith(written, "Content-Length: 0\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\n");
    }

    [TestMethod]
    public async Task PerformAsync_PostWithAChunkedHeaderAndAWrongPostFieldSize_SendsEachCallbackAnswerAsAChunk()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();
        string[] answers = ["one", "two", "three", "four"];
        int index = 0;
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/p" },
            Http = new EasyHttpOptions { Post = true, PostFieldSize = 4, HttpHeaders = ["Expect:", "Transfer-Encoding: chunked"] },
            Callbacks = new EasyCallbackOptions
            {
                Read = buffer => index == answers.Length ? 0 : Encoding.ASCII.GetBytes(answers[index++], buffer),
            },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        string written = Encoding.ASCII.GetString(connector.Written);
        Assert.IsFalse(written.Contains("Content-Length", StringComparison.OrdinalIgnoreCase), written);
        StringAssert.EndsWith(written, "\r\n\r\n3\r\none\r\n3\r\ntwo\r\n5\r\nthree\r\n4\r\nfour\r\n0\r\n\r\n");
    }

    [TestMethod]
    [DataRow("Transfer-Encoding: chunked", true)]
    [DataRow(" transfer-encoding :gzip, Chunked", true)]
    [DataRow("Transfer-Encoding: gzip", false)]
    [DataRow("X-Transfer-Encoding: chunked", false)]
    [DataRow("Transfer-Encoding chunked", false)]
    public void AsksForChunked_OneHeaderLine_TellsWhetherItAsksForChunked(string line, bool expected) =>
        Assert.AreEqual(expected, ManagedEasyTransfer.AsksForChunked([line]));

    private static async Task<CurlCode> PerformReadCallbackPostAsync(ManagedEasyTransfer easy, EasyHttpOptions http, EasyTransferOptions transfer)
    {
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/p" },
            Http = http,
            Transfer = transfer,
            Callbacks = new EasyCallbackOptions { Read = ReaderOf("hello") },
        };

        return await easy.PerformAsync(CancellationToken.None);
    }
}
