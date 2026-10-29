using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.Options;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;

namespace Curl.Engine;

// CURLOPT_HTTPPOST through a whole transfer, its CURLFORM_STREAM parts read through CURLOPT_READFUNCTION (BL-2482).
public sealed partial class ManagedEasyTransferTests
{
    [TestMethod]
    public async Task PerformAsync_HttpPostOfATextAndAStreamField_SendsTheRequestTheToolSendsForTheSameFields()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        ManagedCurlEngine engine = CreateEngine(connector);
        ICurlFormBuilder form = engine.CreateForm();
        Assert.AreEqual(CurlFormAddCode.Ok, form.Add([new(CurlFormOption.CopyName, "a"), new(CurlFormOption.CopyContents, "1")]));
        Assert.AreEqual(CurlFormAddCode.Ok, form.Add([new(CurlFormOption.CopyName, "c"), new(CurlFormOption.Stream, new object()), new(CurlFormOption.ContentsLength, 5L)]));
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();

        CurlCode code = await PerformHttpPostAsync(easy, "http://h/form", form.Build(), ReaderOf("hello"));
        byte[] toolBytes = await PerformToolFormAsync("http://h/form", "-F", "a=1", "-F", "c=hello");

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(Encoding.ASCII.GetString(toolBytes), Encoding.ASCII.GetString(connector.Written));
    }

    [TestMethod]
    public async Task PerformAsync_HttpPostStreamFieldWithNoContentsLength_SendsTheBodyChunkedFromTheReadCallback()
    {
        // An unknown size asks for 100-continue, as libcurl does, so the server answers it before the body comes.
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n"), Encoding.ASCII.GetBytes(Response)]);
        ManagedCurlEngine engine = CreateEngine(connector);
        ICurlFormBuilder form = engine.CreateForm();
        Assert.AreEqual(CurlFormAddCode.Ok, form.Add([new(CurlFormOption.CopyName, "c"), new(CurlFormOption.Stream, new object())]));
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();

        CurlCode code = await PerformHttpPostAsync(easy, "http://h/form", form.Build(), ReaderOf("hello"));

        Assert.AreEqual(CurlCode.Ok, code);
        string written = Encoding.ASCII.GetString(connector.Written);
        string body = $"--{FormBoundary}\r\nContent-Disposition: form-data; name=\"c\"\r\n\r\nhello\r\n--{FormBoundary}--\r\n";
        StringAssert.StartsWith(written, "POST /form HTTP/1.1\r\n");
        StringAssert.Contains(written, "Transfer-Encoding: chunked\r\n");
        Assert.DoesNotContain("Content-Length", written);
        Assert.AreEqual(body, Dechunked(written[(written.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..]));
    }

    [TestMethod]
    public async Task PerformAsync_HttpPostOfAMissingFile_ConnectsAndSendsTheHeadThenFailsWithReadErrorAndNoText()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        ManagedCurlEngine engine = CreateEngine(connector);
        ICurlFormBuilder form = engine.CreateForm();
        string missing = Path.Combine(Path.GetTempPath(), "cgr262-missing-" + Guid.NewGuid().ToString("N"));
        Assert.AreEqual(CurlFormAddCode.Ok, form.Add([new(CurlFormOption.CopyName, "f"), new(CurlFormOption.File, missing)]));
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();

        CurlCode code = await PerformHttpPostAsync(easy, "http://h/form", form.Build(), ReaderOf(string.Empty));

        Assert.AreEqual(CurlCode.ReadError, code);
        Assert.AreEqual(string.Empty, easy.ErrorText);
        StringAssert.StartsWith(Encoding.ASCII.GetString(connector.Written), "POST /form HTTP/1.1\r\n");
    }

    [TestMethod]
    public async Task PerformAsync_MimePostThatFailsToBuild_FailsWithItsCodeBeforeConnecting()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        ManagedCurlEngine engine = CreateEngine(connector);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();
        EasyMimeBody mime = new() { Parts = [new EasyMimePart { Name = "a\nb", Data = new EasyMimeBytes(Encoding.UTF8.GetBytes("1")) }] };
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/form" },
            Http = new EasyHttpOptions { MimePost = mime, MimeOptions = MimeOptions.FormEscape },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.BadFunctionArgument, code);
        Assert.IsEmpty(connector.Written);
    }

    private static async Task<CurlCode> PerformHttpPostAsync(ManagedEasyTransfer easy, string url, EasyMimeBody form, ReadCallback read)
    {
        easy.CreateFormBoundary = () => FormBoundary;
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = url },
            Http = new EasyHttpOptions { HttpPost = form },
            Callbacks = new EasyCallbackOptions { Read = read },
        };

        return await easy.PerformAsync(CancellationToken.None);
    }
}
