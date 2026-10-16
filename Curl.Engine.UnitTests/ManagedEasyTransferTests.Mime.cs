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
    public async Task Duplicate_FixedFormBoundary_KeepsTheBoundary()
    {
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(new ScriptedConnector([])).CreateEasy();
        easy.CreateFormBoundary = () => FormBoundary;

        await using ManagedEasyTransfer copy = (ManagedEasyTransfer)easy.Duplicate();

        Assert.AreEqual(FormBoundary, copy.CreateFormBoundary());
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
