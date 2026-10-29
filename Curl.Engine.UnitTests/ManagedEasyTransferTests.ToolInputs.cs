using System.Net;
using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins <see cref="ManagedEasyTransfer.ToolInputs" /> and <see cref="ManagedEasyTransfer.LastResult" /> (BL-2395,
/// ADR-0554): a perform from the options puts each piece the curl tool's runner chose on the context as the
/// runner's own <see cref="TransferContextFactory.Create" /> call does, leaves the cookie, alt-svc and HSTS files
/// to the runner, and keeps its whole result for the runner to read.
/// </summary>
public sealed partial class ManagedEasyTransferTests
{
    [TestMethod]
    public async Task LastResult_PerformFromOptions_IsTheWholeResult()
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.Failing("http", CurlExitCode.CouldntConnect, "Failed to connect");
        await using ManagedEasyTransfer easy = EasyOver(handler);
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/x" } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.CouldntConnect, code);
        Assert.IsNotNull(easy.LastResult);
        Assert.AreEqual(CurlExitCode.CouldntConnect, easy.LastResult.ExitCode);
        Assert.AreEqual("Failed to connect", easy.LastResult.ErrorMessage);
    }

    [TestMethod]
    public async Task LastResult_PerformWithNoUrlAfterATransfer_IsNull()
    {
        await using ManagedEasyTransfer easy = EasyOver(RecordingProtocolHandler.WritingPath("http"));
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/x" } };
        await easy.PerformAsync(CancellationToken.None);
        Assert.IsTrue(easy.LastResult!.IsSuccess);
        easy.Options = new EasyOptions();

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.UrlMalformat, code);
        Assert.IsNull(easy.LastResult);
    }

    [TestMethod]
    public async Task PerformAsync_ToolInputsWithAUrl_TransfersItInPlaceOfAParseOfTheOptionsUrl()
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath("http");
        await using ManagedEasyTransfer easy = EasyOver(handler);
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://[" } };
        easy.ToolInputs = new EngineToolTransferInputs(Url: CurlUrl.Parse("http://h/abc"));
        using MemoryStream output = new();
        easy.ToolOutputs = new EngineToolTransferOutputs(output);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("/abc", Encoding.ASCII.GetString(output.ToArray()));
        Assert.AreSame(easy.ToolInputs.Url, easy.LastContext!.Url);
        Assert.IsTrue(easy.LastResult!.IsSuccess);
        Assert.AreEqual("http://[", easy.Info.EffectiveUrl);
    }

    [TestMethod]
    public async Task PerformAsync_ToolInputsWithAProxy_PutsItOnTheContextAndTheHttpForwardProxy()
    {
        ProxyEndpoint proxy = new(ProxyKind.Http, "proxy", 3128, null);

        (ITransferContext context, TransferContext runners) = await ContextsWithAsync(new EngineToolTransferInputs(Proxy: proxy));

        Assert.AreSame(proxy, context.Proxy);
        Assert.AreSame(proxy, context.Http!.ForwardProxy);
        Assert.AreEqual(runners.Http!.ForwardProxy, context.Http.ForwardProxy);
    }

    [TestMethod]
    public async Task PerformAsync_ToolInputsWithLookedUpCredentials_CarriesThemInPlaceOfTheOptions()
    {
        NetworkCredential credentials = new("netrc-user", "netrc-pass");

        (ITransferContext context, TransferContext runners) = await ContextsWithAsync(new EngineToolTransferInputs(LookedUpCredentials: credentials));

        Assert.AreSame(credentials, context.Credentials);
        Assert.AreSame(runners.Credentials, context.Credentials);
    }

    [TestMethod]
    public async Task PerformAsync_ToolInputsWithSshOptions_PutsThemOnTheContext()
    {
        SshOptions ssh = new() { KnownHostsPath = "known_hosts" };

        (ITransferContext context, TransferContext runners) = await ContextsWithAsync(new EngineToolTransferInputs(Ssh: ssh));

        Assert.AreSame(ssh, context.Ssh);
        Assert.AreSame(runners.Ssh, context.Ssh);
    }

    [TestMethod]
    public async Task PerformAsync_ToolInputsWithIfNoneMatchLines_SendsThemAfterTheOtherHeaders()
    {
        (ITransferContext context, TransferContext runners) = await ContextsWithAsync(new EngineToolTransferInputs(IfNoneMatchHeaders: ["If-None-Match: \"e\""]));

        Assert.AreEqual("If-None-Match: \"e\"", context.Http!.Headers[^1]);
        CollectionAssert.AreEqual(runners.Http!.Headers.ToArray(), context.Http.Headers.ToArray());
    }

    [TestMethod]
    public async Task PerformAsync_ToolInputsWithARange_PutsItOnTheContext()
    {
        ByteRange range = ByteRange.Bounded(2, 5);

        (ITransferContext context, TransferContext runners) = await ContextsWithAsync(new EngineToolTransferInputs(Range: range));

        Assert.AreSame(range, context.Range);
        Assert.AreSame(runners.Range, context.Range);
    }

    [TestMethod]
    public async Task PerformAsync_ToolInputsWithAResumeOffset_PutsItOnTheContextInPlaceOfTheOptions()
    {
        (ITransferContext context, TransferContext runners) = await ContextsWithAsync(new EngineToolTransferInputs(ResumeFrom: 42));

        Assert.AreEqual(42L, context.ResumeFrom);
        Assert.AreEqual(runners.ResumeFrom, context.ResumeFrom);
    }

    [TestMethod]
    public async Task PerformAsync_ToolInputsWithAFormBody_SendsItAsTheHttpBody()
    {
        BytesBody form = new(Encoding.ASCII.GetBytes("--b--"), "multipart/form-data; boundary=b");

        (ITransferContext context, TransferContext runners) = await ContextsWithAsync(new EngineToolTransferInputs(FormBody: form));

        Assert.AreSame(form, context.Http!.Body);
        Assert.AreSame(runners.Http!.Body, context.Http.Body);
    }

    [TestMethod]
    public async Task PerformAsync_ToolInputsWithAnUpload_PutsItOnTheContextInPlaceOfTheReadCallback()
    {
        using MemoryStream upload = new(Encoding.ASCII.GetBytes("up"));

        (ITransferContext context, TransferContext runners) = await ContextsWithAsync(new EngineToolTransferInputs(Upload: upload));

        Assert.AreSame(upload, context.Upload);
        Assert.AreSame(runners.Upload, context.Upload);
    }

    [TestMethod]
    public async Task PerformAsync_ToolInputsWithAnAltSvcCache_StoresAltSvcHeadersInIt()
    {
        AltSvcTransferCache altSvc = await AltSvcTransferCache.OpenAsync(new EasyHttpOptions(), [], new MemoryFileSystem(), TimeProvider.System);

        (ITransferContext context, TransferContext runners) = await ContextsWithAsync(new EngineToolTransferInputs(AltSvc: altSvc));

        Assert.AreSame(altSvc, context.Http!.AltSvcStore);
        Assert.AreSame(runners.Http!.AltSvcStore, context.Http.AltSvcStore);
    }

    [TestMethod]
    public async Task PerformAsync_ToolInputsSet_LeavesTheCookieAltSvcAndHstsFilesToTheRunner()
    {
        MemoryFileSystem files = new() { Files = { ["hsts.txt"] = Encoding.ASCII.GetBytes("h \"20991231 00:00:00\"\n") } };
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath("http");
        await using ManagedEasyTransfer easy = EasyOver(handler);
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Http = new EasyHttpOptions { CookieFiles = ["in.txt"], CookieJar = "out.txt", AltSvc = "altsvc.txt", Hsts = "hsts.txt" },
        };
        easy.Host = HostOf(files);
        easy.ToolInputs = new EngineToolTransferInputs();

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.IsEmpty(files.Opened);
        Assert.AreEqual("http", handler.Contexts.Single().Url.Scheme);
        Assert.IsNull(handler.Contexts.Single().Http!.AltSvcStore);
    }

    [TestMethod]
    public async Task PerformAsync_NoToolInputs_TakesTheResumeOffsetFromTheOptions()
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath("http");
        await using ManagedEasyTransfer easy = EasyOver(handler);
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/x" }, Transfer = new() { ResumeFrom = 7 } };

        await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(7L, handler.Contexts.Single().ResumeFrom);
        Assert.IsNull(handler.Contexts.Single().Range);
        Assert.IsNull(handler.Contexts.Single().Proxy);
    }

    [TestMethod]
    public async Task PerformAsync_NoToolInputsAndResumeFromZero_PassesNoResumeOffset()
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath("http");
        await using ManagedEasyTransfer easy = EasyOver(handler);
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/x" }, Transfer = new() { ResumeFrom = 0 } };

        await easy.PerformAsync(CancellationToken.None);

        Assert.IsNull(handler.Contexts.Single().ResumeFrom);
    }

    [TestMethod]
    public async Task Duplicate_HandleWithToolInputs_DoesNotCopyThemAndResetClearsThem()
    {
        await using ManagedEasyTransfer easy = EasyOver(RecordingProtocolHandler.WritingPath("http"));
        easy.ToolInputs = new EngineToolTransferInputs(ResumeFrom: 1);

        await using IEasyTransfer copy = easy.Duplicate();
        easy.Reset();

        Assert.IsNull(((ManagedEasyTransfer)copy).ToolInputs);
        Assert.IsNull(easy.ToolInputs);
    }

    private static ManagedEasyTransfer EasyOver(RecordingProtocolHandler handler) =>
        (ManagedEasyTransfer)new ManagedCurlEngine(TimeProvider.System, (_, _, _) => new ProtocolDispatcher([handler]), readEnvironmentVariable: _ => null).CreateEasy();

    // The context the handle builds with the inputs, and the one the runner's own
    // transferContextFactory.Create call builds from the same options and pieces.
    private static async Task<(ITransferContext Context, TransferContext Runners)> ContextsWithAsync(EngineToolTransferInputs inputs)
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath("http");
        await using ManagedEasyTransfer easy = EasyOver(handler);
        EasyOptions options = new() { Network = new EasyNetworkOptions { Url = "http://h/x" }, Http = new EasyHttpOptions { HttpHeaders = ["X-A: 1"] } };
        easy.Options = options;
        easy.ToolInputs = inputs;

        await easy.PerformAsync(CancellationToken.None);

        TransferContext runners = new TransferContextFactory(Stream.Null, TimeProvider.System).Create(
            ManagedEasyTransfer.TransferSettingsOf(options),
            options.Http,
            options.Behaviour,
            options.Transfer,
            options.Protocols,
            options.Proxy,
            options.Authentication,
            options.Network,
            CurlUrl.Parse("http://h/x"),
            Stream.Null,
            inputs.Range,
            inputs.ResumeFrom,
            headerOutput: null,
            inputs.FormBody,
            inputs.Upload,
            inputs.Proxy,
            lookedUpCredentials: inputs.LookedUpCredentials,
            ifNoneMatchHeaders: inputs.IfNoneMatchHeaders,
            altSvc: inputs.AltSvc,
            ssh: inputs.Ssh);
        return (handler.Contexts.Single(), runners);
    }
}
