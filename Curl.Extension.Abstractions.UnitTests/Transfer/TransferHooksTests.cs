using System.Text;

namespace Curl.Extension.Abstractions.Transfer;

[TestClass]
public sealed class TransferHooksTests
{
    private static readonly ExtensionOption Example = new("example-hook", ExtensionOptionArgument.None, "Run the example hook");

    [TestMethod]
    public async Task ExecuteAsync_WithAHook_AddsItsHeaderRewritesTheBodyAndReadsStatusHeadersAndTimings()
    {
        TransferTimings timings = new(1, null, 2, 3, 4, 5);
        FakeHandler handler = new("hello", new TransferReport { ResponseCode = 200, ResponseHeaders = [new("Content-Type", "text/plain")], Timings = timings });
        ExampleHook hook = new();
        MemoryStream output = new();
        TransferHooks hooks = TransferHooks.ActivatedBy(new ExtensionOptionValues(new Dictionary<string, string> { ["example-hook"] = "" }), [ExampleHook.ActivatedBy(hook)]);

        TransferResult result = await hooks.Around(handler).ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("http://example.com/"),
            Output = output,
            Http = new HttpRequestOptions { Headers = ["X-Curl: own"] },
        });

        CollectionAssert.AreEqual(new[] { "X-Curl: own", "X-Extension: added" }, handler.Context!.Http!.Headers.ToArray());
        Assert.AreEqual("HELLO", Encoding.ASCII.GetString(output.ToArray()));
        Assert.AreSame(result, hook.Finished);
        Assert.AreEqual(200, hook.Finished!.Report!.ResponseCode);
        Assert.AreEqual("text/plain", hook.Finished.Report.ResponseHeaders.Single(header => header.Key == "Content-Type").Value);
        Assert.AreSame(timings, hook.Finished.Report.Timings);
        Assert.AreSame(handler.SupportedSchemes, hooks.Around(handler).SupportedSchemes);
    }

    [TestMethod]
    public void ActivatedBy_WithNoExtensionOptionGiven_HoldsNoHookAndGivesTheHandlerBack()
    {
        FakeHandler handler = new("", null);

        TransferHooks hooks = TransferHooks.ActivatedBy(ExtensionOptionValues.None, [ExampleHook.ActivatedBy(new ExampleHook())]);

        Assert.IsEmpty(hooks.Hooks);
        Assert.AreSame(handler, hooks.Around(handler));
        Assert.IsEmpty(TransferHooks.None.Hooks);
        Assert.AreSame(handler, TransferHooks.None.Around(handler));
    }

    [TestMethod]
    public async Task ExecuteAsync_WithAHookImplementingNothing_LeavesTheTransferAsCurlBuiltIt()
    {
        FakeHandler handler = new("body", null);
        MemoryStream output = new();
        TransferContext context = new() { Url = CurlUrl.Parse("ftp://example.com/file"), Output = output };

        TransferResult result = await new TransferHooks([new SilentHook()]).Around(handler).ExecuteAsync(context);

        Assert.IsNull(handler.Context!.Http);
        Assert.AreSame(output, handler.Context.Output);
        Assert.AreEqual("body", Encoding.ASCII.GetString(output.ToArray()));
        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithAnHttpTransferAndAHookAddingNoHeader_KeepsCurlsHeaders()
    {
        FakeHandler handler = new("", null);
        TransferContext context = new() { Url = CurlUrl.Parse("http://example.com/"), Output = new MemoryStream(), Http = new HttpRequestOptions { Headers = ["X-Curl: own"] } };

        await new TransferHooks([new SilentHook()]).Around(handler).ExecuteAsync(context);

        CollectionAssert.AreEqual(new[] { "X-Curl: own" }, handler.Context!.Http!.Headers.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_WithAHook_GivesTheHandlerEveryOtherSettingOfTheTransfer()
    {
        FakeHandler handler = new("", null);
        TransferContext context = new() { Url = CurlUrl.Parse("http://example.com/"), Output = new MemoryStream() };

        await new TransferHooks([new SilentHook()]).Around(handler).ExecuteAsync(context);

        ITransferContext given = handler.Context!;
        ITransferContext own = context;
        Assert.AreSame(own.Url, given.Url);
        Assert.AreEqual(own.Upload, given.Upload);
        Assert.AreEqual(own.ResumeFrom, given.ResumeFrom);
        Assert.AreEqual(own.ResumeUploadFromUnknownOffset, given.ResumeUploadFromUnknownOffset);
        Assert.AreEqual(own.Range, given.Range);
        Assert.AreEqual(own.RangeText, given.RangeText);
        Assert.AreEqual(own.MaxFileSize, given.MaxFileSize);
        Assert.AreEqual(own.NoBody, given.NoBody);
        Assert.AreEqual(own.TimeCondition, given.TimeCondition);
        Assert.AreEqual(own.RemoteTime, given.RemoteTime);
        Assert.AreEqual(own.HeaderOutput, given.HeaderOutput);
        Assert.AreEqual(own.DumpHeaderOutput, given.DumpHeaderOutput);
        Assert.AreEqual(own.PostData, given.PostData);
        Assert.AreEqual(own.PostWithoutData, given.PostWithoutData);
        Assert.AreEqual(own.Credentials, given.Credentials);
        Assert.AreSame(own.TelnetOptions, given.TelnetOptions);
        Assert.AreEqual(own.TftpBlockSize, given.TftpBlockSize);
        Assert.AreEqual(own.TftpNoOptions, given.TftpNoOptions);
        Assert.AreEqual(own.FtpDisableEpsv, given.FtpDisableEpsv);
        Assert.AreEqual(own.FtpSkipPasvIp, given.FtpSkipPasvIp);
        Assert.AreEqual(own.FtpFileMethod, given.FtpFileMethod);
        Assert.AreEqual(own.FtpCreateDirectories, given.FtpCreateDirectories);
        Assert.AreEqual(own.FtpAccount, given.FtpAccount);
        Assert.AreEqual(own.FtpAlternativeToUser, given.FtpAlternativeToUser);
        Assert.AreEqual(own.FtpSendPret, given.FtpSendPret);
        Assert.AreEqual(own.ListOnly, given.ListOnly);
        Assert.AreEqual(own.UseAscii, given.UseAscii);
        Assert.AreEqual(own.Append, given.Append);
        Assert.AreEqual(own.FtpPort, given.FtpPort);
        Assert.AreEqual(own.FtpUseEprt, given.FtpUseEprt);
        Assert.AreEqual(own.FtpAcceptTimeout, given.FtpAcceptTimeout);
        Assert.AreEqual(own.FtpServerResponseTimeout, given.FtpServerResponseTimeout);
        Assert.AreEqual(own.SslLevel, given.SslLevel);
        Assert.AreEqual(own.FtpSslControlOnly, given.FtpSslControlOnly);
        Assert.AreEqual(own.FtpCommandChannelClearing, given.FtpCommandChannelClearing);
        Assert.AreSame(own.QuoteCommands, given.QuoteCommands);
        Assert.AreEqual(own.ConvertLineEndings, given.ConvertLineEndings);
        Assert.AreEqual(own.CreateFileMode, given.CreateFileMode);
        Assert.AreEqual(own.PathAsIs, given.PathAsIs);
        Assert.AreEqual(own.ConnectTimeout, given.ConnectTimeout);
        Assert.AreEqual(own.MaxTime, given.MaxTime);
        Assert.AreEqual(own.OperationStarted, given.OperationStarted);
        Assert.AreEqual(own.Proxy, given.Proxy);
        Assert.AreEqual(own.Mail, given.Mail);
        Assert.AreEqual(own.Ssh, given.Ssh);
        Assert.AreEqual(own.TimeProvider, given.TimeProvider);
        Assert.AreEqual(own.Events, given.Events);
        Assert.AreEqual(own.Progress, given.Progress);
        Assert.AreEqual(own.CancellationToken, given.CancellationToken);
        Assert.AreSame(own.DiagnosticLog, given.DiagnosticLog);
    }

    private sealed class FakeHandler(string body, TransferReport? report) : IProtocolHandler
    {
        public ITransferContext? Context { get; private set; }

        public IReadOnlyCollection<string> SupportedSchemes { get; } = ["http"];

        public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            Context = context;
            await context.Output.WriteAsync(Encoding.ASCII.GetBytes(body));
            return TransferResult.Success(body.Length) with { Report = report };
        }
    }

    private sealed class ExampleHook : ITransferHook
    {
        public TransferResult? Finished { get; private set; }

        public static Func<ExtensionOptionValues, ITransferHook?> ActivatedBy(ExampleHook hook) =>
            values => values.IsGiven(Example) ? hook : null;

        public IEnumerable<string> RequestHeadersFor(ITransferContext context) => ["X-Extension: added"];

        public Stream ResponseBodyOver(Stream output) => new UpperCasingStream(output);

        public void TransferFinished(TransferResult result) => Finished = result;
    }

    private sealed class SilentHook : ITransferHook;

    private sealed class UpperCasingStream(Stream next) : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() => next.Flush();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            next.Write(Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(buffer, offset, count).ToUpperInvariant()));
    }
}
