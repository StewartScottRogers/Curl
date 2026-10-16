using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.Options;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Cli;
using Curl.Console;
using Curl.Core;
using Curl.Core.FileSystem;
using Curl.Core.Multipart;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

// CURLOPT_MIMEPOST through a whole transfer, compared with the -F body the curl tool builds for the same parts (BL-2439).
public sealed partial class ManagedEasyTransferTests
{
    private const string FormBoundary = "------------------------d74496d66958873e";

    [TestMethod]
    public async Task PerformAsync_MimePostOfTextFileAndEncodedParts_SendsTheRequestTheToolSendsForTheSameFormParts()
    {
        string path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "file body\n");
            ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
            ManagedCurlEngine engine = CreateEngine(connector);
            IMimeBuilder mime = engine.CreateMime();
            IMimePartBuilder text = mime.AddPart();
            text.SetName("a");
            text.SetData(Encoding.UTF8.GetBytes("1"));
            IMimePartBuilder upload = mime.AddPart();
            upload.SetName("f");
            upload.SetFileData(path);
            upload.SetType("text/x");
            upload.SetFileName("r.txt");
            IMimePartBuilder content = mime.AddPart();
            content.SetName("c");
            content.SetFileData(path);
            content.SetFileName(null);
            IMimePartBuilder encoded = mime.AddPart();
            encoded.SetName("e");
            encoded.SetData(Encoding.UTF8.GetBytes("hi"));
            encoded.SetEncoder("base64");
            encoded.SetHeaders(["X-A: 1"]);

            await using ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();
            CurlCode code = await PerformMimePostAsync(easy, "http://h/form", mime.Build());
            byte[] toolBytes = await PerformToolFormAsync(
                "http://h/form", "-F", "a=1", "-F", $"f=@{path};type=text/x;filename=r.txt", "-F", $"c=<{path}", "-F", "e=hi;encoder=base64;headers=X-A: 1");

            Assert.AreEqual(CurlCode.Ok, code);
            StringAssert.Contains(Encoding.ASCII.GetString(connector.Written), "Content-Disposition: form-data; name=\"f\"; filename=\"r.txt\"");
            Assert.AreEqual(Encoding.ASCII.GetString(toolBytes), Encoding.ASCII.GetString(connector.Written));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task PerformAsync_MimePostWithSubParts_SendsTheRequestTheToolSendsForANestedForm()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        ManagedCurlEngine engine = CreateEngine(connector);
        IMimeBuilder mime = engine.CreateMime();
        IMimePartBuilder outer = mime.AddPart();
        outer.SetName("m");
        outer.SetType("multipart/mixed");
        IMimeBuilder nested = engine.CreateMime();
        nested.AddPart().SetName("x");
        nested.Build();
        IMimePartBuilder inner = nested.AddPart();
        inner.SetName("y");
        inner.SetData(Encoding.UTF8.GetBytes("2"));
        outer.SetSubParts(nested);

        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();
        CurlCode code = await PerformMimePostAsync(easy, "http://h/form", mime.Build());
        byte[] toolBytes = await PerformToolFormAsync("http://h/form", "-F", "m=(;type=multipart/mixed", "-F", "x=", "-F", "y=2", "-F", "=)");

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(Encoding.ASCII.GetString(toolBytes), Encoding.ASCII.GetString(connector.Written));
    }

    [TestMethod]
    public async Task PerformAsync_MimePostOfAMissingFile_FailsWithReadErrorBeforeSendingAnything()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        ManagedCurlEngine engine = CreateEngine(connector);
        IMimeBuilder mime = engine.CreateMime();
        IMimePartBuilder part = mime.AddPart();
        part.SetName("f");
        part.SetFileData(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.txt"));
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();

        CurlCode code = await PerformMimePostAsync(easy, "http://h/form", mime.Build());

        Assert.AreEqual(CurlCode.ReadError, code);
        Assert.AreEqual(MultipartFormBodyBuilder.OpenFailedMessage, easy.ErrorText);
        Assert.IsEmpty(connector.Written);
    }

    [TestMethod]
    public async Task PerformAsync_MimePostOfEveryByteValue_SendsTheBytesUnchanged()
    {
        byte[] everyByte = [.. Enumerable.Range(0, 256).Select(value => (byte)value)];
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        ManagedCurlEngine engine = CreateEngine(connector);
        IMimeBuilder mime = engine.CreateMime();
        IMimePartBuilder part = mime.AddPart();
        part.SetName("b");
        part.SetData(everyByte);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();

        CurlCode code = await PerformMimePostAsync(easy, "http://h/form", mime.Build());

        Assert.AreEqual(CurlCode.Ok, code);
        byte[] expectedBody = [
            .. Encoding.ASCII.GetBytes($"--{FormBoundary}\r\nContent-Disposition: form-data; name=\"b\"\r\n\r\n"),
            .. everyByte,
            .. Encoding.ASCII.GetBytes($"\r\n--{FormBoundary}--\r\n")];
        string written = Encoding.Latin1.GetString(connector.Written);
        StringAssert.EndsWith(written, Encoding.Latin1.GetString(expectedBody));
        StringAssert.Contains(written, $"Content-Length: {expectedBody.Length}\r\n");
    }

    [TestMethod]
    public async Task PerformAsync_MimePostOfACallbackPartOfKnownSize_SendsWhatTheToolSendsForTheSameTextWithAContentLength()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        ManagedCurlEngine engine = CreateEngine(connector);
        IMimeBuilder mime = engine.CreateMime();
        IMimePartBuilder part = mime.AddPart();
        part.SetName("c");
        part.SetDataCallback(5, ReaderOf("hello"), null);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();

        CurlCode code = await PerformMimePostAsync(easy, "http://h/form", mime.Build());
        byte[] toolBytes = await PerformToolFormAsync("http://h/form", "-F", "c=hello");

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(Encoding.ASCII.GetString(toolBytes), Encoding.ASCII.GetString(connector.Written));
    }

    [TestMethod]
    public async Task PerformAsync_MimePostOfACallbackPartOfUnknownSize_SendsTheBodyChunkedWithNoContentLength()
    {
        // An unknown size asks for 100-continue, as libcurl does, so the server answers it before the body comes.
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n"), Encoding.ASCII.GetBytes(Response)]);
        ManagedCurlEngine engine = CreateEngine(connector);
        IMimeBuilder mime = engine.CreateMime();
        IMimePartBuilder part = mime.AddPart();
        part.SetName("c");
        part.SetDataCallback(-1, ReaderOf("hello"), null);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();

        CurlCode code = await PerformMimePostAsync(easy, "http://h/form", mime.Build());

        Assert.AreEqual(CurlCode.Ok, code);
        string written = Encoding.ASCII.GetString(connector.Written);
        string body = $"--{FormBoundary}\r\nContent-Disposition: form-data; name=\"c\"\r\n\r\nhello\r\n--{FormBoundary}--\r\n";
        StringAssert.Contains(written, "Transfer-Encoding: chunked\r\n");
        StringAssert.Contains(written, "Expect: 100-continue\r\n");
        Assert.DoesNotContain("Content-Length", written);
        Assert.AreEqual(body, Dechunked(written[(written.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..]));
    }

    [TestMethod]
    public async Task PerformAsync_MimePostWhoseCallbackAborts_EndsWithAbortedByCallback()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        ManagedCurlEngine engine = CreateEngine(connector);
        IMimeBuilder mime = engine.CreateMime();
        IMimePartBuilder part = mime.AddPart();
        part.SetName("c");
        part.SetDataCallback(5, _ => Api.Callbacks.ReadCallbackResult.Abort, null);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();

        CurlCode code = await PerformMimePostAsync(easy, "http://h/form", mime.Build());

        Assert.AreEqual(CurlCode.AbortedByCallback, code);
        Assert.AreEqual(CallbackReadStream.AbortedText, easy.ErrorText);
    }

    [TestMethod]
    public async Task Duplicate_FixedFormBoundary_KeepsTheBoundary()
    {
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(new ScriptedConnector([])).CreateEasy();
        easy.CreateFormBoundary = () => FormBoundary;

        await using ManagedEasyTransfer copy = (ManagedEasyTransfer)easy.Duplicate();

        Assert.AreEqual(FormBoundary, copy.CreateFormBoundary());
    }

    // A read callback handing out text's ASCII bytes, as much as each buffer holds.
    private static Api.Callbacks.ReadCallback ReaderOf(string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        int offset = 0;
        return buffer =>
        {
            int count = Math.Min(buffer.Length, bytes.Length - offset);
            bytes.AsSpan(offset, count).CopyTo(buffer);
            offset += count;
            return count;
        };
    }

    // The data of a chunked body, its hexadecimal sizes and CRLFs removed.
    private static string Dechunked(string chunked)
    {
        StringBuilder data = new();
        int position = 0;
        while (true)
        {
            int lineEnd = chunked.IndexOf("\r\n", position, StringComparison.Ordinal);
            int size = Convert.ToInt32(chunked[position..lineEnd], 16);
            if (size == 0)
            {
                return data.ToString();
            }

            data.Append(chunked, lineEnd + 2, size);
            position = lineEnd + 2 + size + 2;
        }
    }

    private static async Task<CurlCode> PerformMimePostAsync(ManagedEasyTransfer easy, string url, EasyMimeBody body)
    {
        easy.CreateFormBoundary = () => FormBoundary;
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = url },
            Http = new EasyHttpOptions { MimePost = body },
        };

        return await easy.PerformAsync(CancellationToken.None);
    }

    // The tool's -F path: the parts EasyOptionsFromCommandLine maps from the command line, built by the runner's
    // MultipartFormBodyBuilder and sent as the context's form body (CurlCommandRunner.BuildFormAsync).
    private static async Task<byte[]> PerformToolFormAsync(string url, params string[] arguments)
    {
        CommandLineOptions commandLine = CommandLineParser.Parse([.. arguments, url], _ => true).Options!;
        EasyMimeBody mimePost = EasyOptionsFromCommandLine.HttpOf(commandLine).MimePost!;
        MultipartFormBuildResult form = await new MultipartFormBodyBuilder(new PhysicalFileSystem(), Encoding.UTF8, () => FormBoundary)
            .BuildAsync(MultipartFormPartMapping.FromMimeBody(mimePost), MultipartNameEscaping.Percent, null, CancellationToken.None);
        Assert.IsTrue(form.IsBuilt);
        EasyOptions options = new() { Network = new EasyNetworkOptions { Url = url } };
        EngineTransferSettings settings = ManagedEasyTransfer.TransferSettingsOf(options);
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        Assert.IsTrue(CurlUrl.TryParse(url, pathAsIs: false, out CurlUrl? transferUrl));
        await using Stream content = form.Body.Content;
        TransferContext context = new TransferContextFactory(Stream.Null, TimeProvider.System).Create(
            settings,
            options.Http,
            options.Behaviour,
            options.Transfer,
            options.Protocols,
            options.Proxy,
            options.Authentication,
            options.Network,
            transferUrl,
            Stream.Null,
            range: null,
            resumeFrom: null,
            headerOutput: null,
            formBody: form.Body);

        await TransferPerformer.FollowWatchingSpeedAsync(
            new EngineTransferState(),
            new RedirectFollower(CreateDispatcher(connector, cookies: null)),
            context,
            RedirectPolicyMapping.Of(settings, options.Http, options.Authentication));

        return connector.Written;
    }
}
