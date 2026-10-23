using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.Options;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;

namespace Curl.Engine;

// The four MIME edge cases of Stewart's libcurl conformance batch (report 30), each a whole CURLOPT_MIMEPOST transfer
// whose request bytes and code are pinned to what libcurl 8.22.0 sends and returns for the same parts, as recorded in
// Documentation/Product/LibcurlMimeEdgeCaseRecordings.md (BL-3210 cases 1 to 4, BL-3208). The recorder's boundaries are
// random 46-character strings; these tests fix them at that length so every length matches the recording.
public sealed partial class ManagedEasyTransferTests
{
    private const string UpstreamBoundary = "------------------------AAAAAAAAAAAAAAAAAAAAAA";

    private const string UpstreamInnerBoundary = "------------------------BBBBBBBBBBBBBBBBBBBBBB";

    [TestMethod]
    public async Task PerformAsync_UpstreamCase1MissingFilePart_SendsThePostWithThatPartEmptyAndEndsOk()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        ManagedCurlEngine engine = CreateEngine(connector);
        IMimeBuilder mime = engine.CreateMime();
        IMimePartBuilder missing = mime.AddPart();
        missing.SetName("a");
        CurlCode setCode = missing.SetFileData(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "nope.txt"));
        IMimePartBuilder data = mime.AddPart();
        data.SetName("b");
        data.SetData(Encoding.ASCII.GetBytes("hello"));
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();

        CurlCode code = await PerformUpstreamMimePostAsync(easy, mime.Build(), []);

        string body =
            $"--{UpstreamBoundary}\r\nContent-Disposition: form-data; name=\"a\"\r\n\r\n\r\n"
            + $"--{UpstreamBoundary}\r\nContent-Disposition: form-data; name=\"b\"\r\n\r\nhello\r\n--{UpstreamBoundary}--\r\n";
        Assert.AreEqual(CurlCode.ReadError, setCode);
        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(249, body.Length);
        Assert.AreEqual(UpstreamRequestHead("Content-Length: 249") + body, Encoding.ASCII.GetString(connector.Written));
    }

    [TestMethod]
    public async Task PerformAsync_UpstreamCase2SevenBitPartOverNonAscii_StartsTheRequestThenFailsWithReadError()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        ManagedCurlEngine engine = CreateEngine(connector);
        IMimeBuilder mime = engine.CreateMime();
        IMimePartBuilder part = mime.AddPart();
        part.SetName("a");
        part.SetData([.. "h"u8, 0xC3, 0xA9, .. "llo"u8]);
        part.SetEncoder("7bit");
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();

        CurlCode code = await PerformUpstreamMimePostAsync(easy, mime.Build(), []);

        string sentBody = $"--{UpstreamBoundary}\r\nContent-Disposition: form-data; name=\"a\"\r\nContent-Transfer-Encoding: 7bit\r\n\r\nh";
        Assert.AreEqual(CurlCode.ReadError, code);
        Assert.AreEqual(128, sentBody.Length);
        Assert.AreEqual(UpstreamRequestHead("Content-Length: 187") + sentBody, Encoding.Latin1.GetString(connector.Written));
    }

    [TestMethod]
    public async Task PerformAsync_UpstreamCase3Base64EncoderOverSubparts_SendsUpstreamsEncodedBodyByteForByte()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        ManagedCurlEngine engine = CreateEngine(connector);
        IMimeBuilder nested = engine.CreateMime();
        IMimePartBuilder inner = nested.AddPart();
        inner.SetName("b");
        inner.SetData(Encoding.ASCII.GetBytes("hello"));
        IMimeBuilder mime = engine.CreateMime();
        IMimePartBuilder outer = mime.AddPart();
        outer.SetName("a");
        outer.SetSubParts(nested);
        outer.SetEncoder("base64");
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();

        CurlCode code = await PerformUpstreamMimePostAsync(easy, mime.Build(), []);

        string block = $"--{UpstreamInnerBoundary}\r\nContent-Disposition: attachment; name=\"b\"\r\n\r\nhello\r\n--{UpstreamInnerBoundary}--\r\n";
        string encoded = Convert.ToBase64String(Encoding.ASCII.GetBytes(block));
        string body =
            $"--{UpstreamBoundary}\r\nContent-Disposition: form-data; name=\"a\"\r\nContent-Type: multipart/mixed; boundary={UpstreamInnerBoundary}\r\n"
            + $"Content-Transfer-Encoding: base64\r\n\r\n{encoded[..76]}\r\n{encoded[76..152]}\r\n{encoded[152..]}\r\n--{UpstreamBoundary}--\r\n";
        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(483, body.Length);
        Assert.AreEqual(UpstreamRequestHead("Content-Length: 483") + body, Encoding.ASCII.GetString(connector.Written));
    }

    [TestMethod]
    public async Task PerformAsync_UpstreamCase4ChunkedPost_SendsThePartInOneChunkWithNoEmptyChunkBeforeIt()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        ManagedCurlEngine engine = CreateEngine(connector);
        IMimeBuilder mime = engine.CreateMime();
        IMimePartBuilder data = mime.AddPart();
        data.SetName("b");
        data.SetData(Encoding.ASCII.GetBytes("hello"));
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();

        CurlCode code = await PerformUpstreamMimePostAsync(easy, mime.Build(), ["Transfer-Encoding: chunked"]);

        string body = $"--{UpstreamBoundary}\r\nContent-Disposition: form-data; name=\"b\"\r\n\r\nhello\r\n--{UpstreamBoundary}--\r\n";
        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(0x99, body.Length);
        Assert.AreEqual(UpstreamRequestHead("Transfer-Encoding: chunked") + $"99\r\n{body}\r\n0\r\n\r\n", Encoding.ASCII.GetString(connector.Written));
    }

    // The request line and headers libcurl sends for a form POST, with the length or framing header the case uses.
    private static string UpstreamRequestHead(string framingHeader) =>
        $"POST /form HTTP/1.1\r\nHost: h\r\nAccept: */*\r\n{framingHeader}\r\nContent-Type: multipart/form-data; boundary={UpstreamBoundary}\r\n\r\n";

    // A MIME POST whose outer form takes UpstreamBoundary and any nested form UpstreamInnerBoundary, as each is asked for.
    private static async Task<CurlCode> PerformUpstreamMimePostAsync(ManagedEasyTransfer easy, EasyMimeBody body, IReadOnlyList<string> headers)
    {
        Queue<string> boundaries = new([UpstreamBoundary, UpstreamInnerBoundary]);
        easy.CreateFormBoundary = () => boundaries.Count > 0 ? boundaries.Dequeue() : UpstreamInnerBoundary;
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/form" },
            Http = new EasyHttpOptions { MimePost = body, HttpHeaders = headers },
        };

        return await easy.PerformAsync(CancellationToken.None);
    }
}
