using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// CGR-127: <c>CURLINFO_CONTENT_LENGTH_UPLOAD_T</c> after an <c>http</c> request is the last request's body size, as
/// libcurl 8.22.0 answers: 0 for a plain <c>GET</c>, 0 after a post a 301 turned into a <c>GET</c>, and -1 for a post
/// sent chunked under a <c>Transfer-Encoding: chunked</c> header.
/// </summary>
[TestClass]
public sealed class Cgr127Tests
{
    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";

    [TestMethod]
    public async Task PerformAsync_PlainGet_ContentLengthUploadIsZero()
    {
        long size = await ContentLengthUploadAfterAsync(new EasyHttpOptions(), Ok);

        Assert.AreEqual(0L, size);
    }

    [TestMethod]
    public async Task PerformAsync_PostRedirectedTo301Get_ContentLengthUploadIsZero()
    {
        long size = await ContentLengthUploadAfterAsync(
            new EasyHttpOptions { PostFields = Encoding.ASCII.GetBytes("q=1"), FollowLocation = FollowLocation.All },
            "HTTP/1.1 301 Moved\r\nLocation: /n\r\nContent-Length: 0\r\n\r\n",
            Ok);

        Assert.AreEqual(0L, size);
    }

    [TestMethod]
    public async Task PerformAsync_PostWithChunkedTransferEncodingHeader_ContentLengthUploadIsUnknown()
    {
        long size = await ContentLengthUploadAfterAsync(
            new EasyHttpOptions { PostFields = Encoding.ASCII.GetBytes("abc"), HttpHeaders = ["Transfer-Encoding: chunked"] },
            Ok);

        Assert.AreEqual(-1L, size);
    }

    [TestMethod]
    public async Task PerformAsync_PostWithContentLength_ContentLengthUploadIsTheBodySize()
    {
        long size = await ContentLengthUploadAfterAsync(new EasyHttpOptions { PostFields = Encoding.ASCII.GetBytes("abc") }, Ok);

        Assert.AreEqual(3L, size);
    }

    [TestMethod]
    [DataRow("http://h/", false, false, false, false, 0, null, 0L)]
    [DataRow("HTTPS://h/", false, false, false, false, 0, null, 0L)]
    [DataRow("ftp://h/", false, false, false, false, 0, null, -1L)]
    [DataRow("http://h/", false, false, false, false, -1, null, -1L)]
    [DataRow("http://h/", true, false, false, false, 0, null, 3L)]
    [DataRow("http://h/", true, true, false, false, 0, null, 0L)]
    [DataRow("http://h/", true, false, true, false, 0, null, 0L)]
    [DataRow("http://h/", true, false, false, true, 0, null, 3L)]
    [DataRow("http://h/", true, false, false, false, 1, "GET", 0L)]
    [DataRow("http://h/", true, false, false, false, 1, "HEAD", 0L)]
    [DataRow("http://h/", true, false, false, false, 1, "POST", 3L)]
    [DataRow("http://h/", true, false, false, false, 0, "GET", 3L)]
    public void ContentLengthUploadOf_HttpRequestSent_AnswersTheLastRequestsBodySize(string url, bool post, bool noBody, bool httpGet, bool postToo, int redirects, string? method, long expected)
    {
        EasyOptions options = new()
        {
            Transfer = new() { NoBody = noBody },
            Http = new EasyHttpOptions { PostFields = post ? Encoding.ASCII.GetBytes("abc") : (ReadOnlyMemory<byte>?)null, HttpGet = httpGet, Post = postToo },
        };
        TransferReport report = new() { RequestSize = redirects < 0 ? 0 : 40, RedirectCount = Math.Max(redirects, 0), Method = method };

        Assert.AreEqual(expected, ManagedEasyTransfer.ContentLengthUploadOf(options, report, url));
    }

    [TestMethod]
    public void ContentLengthUploadOf_UploadFormOrNoReport_KeepsTheDeclaredSize()
    {
        TransferReport sent = new() { RequestSize = 40 };
        EasyMimeBody form = new();

        Assert.AreEqual(7L, ManagedEasyTransfer.ContentLengthUploadOf(new EasyOptions { Transfer = new() { Upload = true, InFileSize = 7 } }, sent, "http://h/"));
        Assert.AreEqual(-1L, ManagedEasyTransfer.ContentLengthUploadOf(new EasyOptions { Http = new EasyHttpOptions { MimePost = form } }, sent, "http://h/"));
        Assert.AreEqual(-1L, ManagedEasyTransfer.ContentLengthUploadOf(new EasyOptions { Http = new EasyHttpOptions { HttpPost = form } }, sent, "http://h/"));
        Assert.AreEqual(-1L, ManagedEasyTransfer.ContentLengthUploadOf(new EasyOptions(), null, "http://h/"));
    }

    private static async Task<long> ContentLengthUploadAfterAsync(EasyHttpOptions http, params string[] responses)
    {
        ScriptedConnector connector = new([.. responses.Select(Encoding.ASCII.GetBytes)]);
        await using IEasyTransfer easy = EngineOver(connector).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Http = http,
            Callbacks = new EasyCallbackOptions { Write = bytes => bytes.Length },
        };

        Assert.AreEqual(CurlCode.Ok, await easy.PerformAsync(CancellationToken.None));
        return easy.Info.ContentLengthUpload;
    }

    private static ManagedCurlEngine EngineOver(ScriptedConnector connector) =>
        new(
            TimeProvider.System,
            (_, _, cookies) => new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
                connector,
                new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "Could not connect to server"),
                new PassThroughTlsProvider(),
                new LoopbackDnsResolver(),
                cookies)),
            readEnvironmentVariable: _ => null);
}
