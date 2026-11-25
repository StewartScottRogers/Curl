using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using static Curl.Extension.ResumableUpload.Transfer.ScriptedNext;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>
/// Pins the resumable upload flows over a scripted rest of curl's transfer: tus 1.0 ("Creation", "Core Protocol") and
/// draft-ietf-httpbis-resumable-upload (upload creation, upload append, offset retrieval), with resumption after a
/// broken connection, the last response as the transfer's, and every request sent through <c>next</c> so
/// <c>-v</c>, <c>--trace</c>, <c>--har</c> and <c>--record</c> see it (EXT-119, ADR-1379).
/// </summary>
[TestClass]
public sealed class ResumableUploadTransferHookTests
{
    private static readonly Answer TusOptions = Response(204, "", "Tus-Resumable: 1.0.0", "Tus-Version: 1.0.0,0.2.2", "Tus-Extension: creation,expiration");
    private static readonly Answer TusCreated = Response(201, "", "Location: /files/24e5", "Tus-Resumable: 1.0.0");
    private static readonly Answer TusPatched = Response(204, "", "Tus-Resumable: 1.0.0", "Upload-Offset: 5");
    private static readonly Answer PlainOptions = Response(200, "", "Allow: GET, PUT");
    private static readonly Answer IetfCreated = Response(201, "", "Location: /uploads/7");
    private static readonly Answer IetfAppended = Response(200, "done", "Content-Length: 4");

    [TestMethod]
    public async Task ExecuteAsync_WithoutUploadOrOverAnotherScheme_CallsNextUnchanged()
    {
        var plain = new TransferContext { Url = CurlUrl.Parse("http://up.example.com/files"), Output = new MemoryStream() };
        var ftp = Context("ftp://up.example.com/files");
        var expected = TransferResult.Success(42);
        List<ITransferContext> given = [];

        foreach (var context in new ITransferContext[] { plain, ftp })
        {
            Assert.AreSame(expected, await new ResumableUploadTransferHook().ExecuteAsync(context, sent => { given.Add(sent); return ValueTask.FromResult(expected); }));
        }

        CollectionAssert.AreEqual(new ITransferContext[] { plain, ftp }, given);
    }

    [TestMethod]
    public void Constructor_NegativeResumptions_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ResumableUploadTransferHook(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ResumableUploadTransferHook(memoryLimit: -1));
        Assert.AreEqual(ResumableUploadTransferHook.DefaultMaxResumptions, new ResumableUploadTransferHook().MaxResumptions);
    }

    [TestMethod]
    public async Task ExecuteAsync_TusServer_SendsOptionsPostAndPatchThroughNext()
    {
        // EXT-119: each request goes through the rest of curl's transfer, so --har, --record, -v and --trace see all three.
        var next = new ScriptedNext(TusOptions, TusCreated, TusPatched);
        var context = Context("http://up.example.com/files?x=1", http: new HttpRequestOptions { Fail = HttpFailMode.Fail });

        var result = await new ResumableUploadTransferHook().ExecuteAsync(context, next.SendAsync);

        Assert.AreEqual(
            "OPTIONS http://up.example.com/files?x=1 [] -|POST http://up.example.com/files?x=1 [Tus-Resumable: 1.0.0,Upload-Length: 5] |"
            + "PATCH http://up.example.com/files/24e5 [Tus-Resumable: 1.0.0,Upload-Offset: 0,Content-Type: application/offset+octet-stream] hello",
            Describe(next));
        Assert.IsTrue(next.Sent.All(sent => sent.Context.Http!.Fail == HttpFailMode.None && sent.Context.HeaderOutput is not null && sent.Context.DumpHeaderOutput is null));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(204, result.Report!.ResponseCode);
        Assert.AreEqual("PATCH", result.Report.Method);
        Assert.AreEqual(5, result.Report.UploadSize);
    }

    [TestMethod]
    [DataRow(CurlExitCode.RecvError, "3", "lo")]
    [DataRow(CurlExitCode.SendError, "5", "")]
    [DataRow(CurlExitCode.GotNothing, "0", "hello")]
    [DataRow(CurlExitCode.Http2Stream, "2", "llo")]
    [DataRow(CurlExitCode.Http2, "1", "ello")]
    [DataRow(CurlExitCode.PartialFile, "4", "o")]
    [DataRow(CurlExitCode.Http3, "2", "llo")]
    public async Task ExecuteAsync_TusPatchBroken_RetrievesTheOffsetWithHeadAndPatchesFromIt(CurlExitCode broken, string offset, string rest)
    {
        // tus 1.0 "Core Protocol": after a broken PATCH the client sends HEAD and resumes from its Upload-Offset, the
        // upload's whole length included.
        var next = new ScriptedNext(TusOptions, TusCreated, Failure(broken), Response(200, "", "Upload-Offset: " + offset, "Tus-Resumable: 1.0.0"), TusPatched);

        var result = await new ResumableUploadTransferHook().ExecuteAsync(Context("http://up.example.com/files"), next.SendAsync);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsTrue(next.Sent[3].Context.NoBody);
        Assert.AreEqual(
            $"HEAD http://up.example.com/files/24e5 [Tus-Resumable: 1.0.0] -|PATCH http://up.example.com/files/24e5 [Tus-Resumable: 1.0.0,Upload-Offset: {offset},Content-Type: application/offset+octet-stream] {rest}",
            Describe(next, skip: 3));
    }

    [TestMethod]
    public async Task ExecuteAsync_IetfServer_CreatesWithUploadCompleteFalseAndWritesTheLastResponse()
    {
        // draft-ietf-httpbis-resumable-upload "Upload Creation" and "Upload Append"; -H lines follow the flow's fields,
        // except one naming a flow field or Content-Length.
        var next = new ScriptedNext(PlainOptions, IetfCreated, IetfAppended);
        var headerOutput = new MemoryStream();
        var dumpHeaderOutput = new MemoryStream();
        var context = Context(
            "http://up.example.com/files",
            http: new HttpRequestOptions { Headers = ["Authorization: Bearer t", "upload-offset: 9", "Content-Length: 9", "NoColon"] },
            headerOutput: headerOutput,
            dumpHeaderOutput: dumpHeaderOutput);

        var result = await new ResumableUploadTransferHook().ExecuteAsync(context, next.SendAsync);

        Assert.AreEqual(
            "OPTIONS http://up.example.com/files [Authorization: Bearer t,upload-offset: 9,NoColon] -|"
            + "PUT http://up.example.com/files [Upload-Draft-Interop-Version: 6,Upload-Complete: ?0,Upload-Length: 5,Authorization: Bearer t,upload-offset: 9,NoColon] |"
            + "PATCH http://up.example.com/uploads/7 [Upload-Draft-Interop-Version: 6,Upload-Complete: ?1,Upload-Offset: 0,Content-Type: application/partial-upload,Authorization: Bearer t,NoColon] hello",
            Describe(next));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("done", Encoding.ASCII.GetString(((MemoryStream)context.Output).ToArray()));
        Assert.AreEqual("HTTP/1.1 200 R\r\nContent-Length: 4\r\n\r\n", Encoding.ASCII.GetString(headerOutput.ToArray()));
        CollectionAssert.AreEqual(headerOutput.ToArray(), dumpHeaderOutput.ToArray());
        Assert.AreEqual(headerOutput.Length, result.Report!.HeaderSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_OptionsWithoutTusCreation_UsesTheIetfDraftWithCurlsMethod()
    {
        var next = new ScriptedNext(Response(204, "", "Tus-Version: 1.0.0", "Tus-Extension: termination"), IetfCreated, IetfAppended);

        await new ResumableUploadTransferHook().ExecuteAsync(Context("http://up.example.com/files", http: new HttpRequestOptions { CustomMethod = "POST" }), next.SendAsync);

        Assert.AreEqual("POST", next.Sent[1].Method);
    }

    [TestMethod]
    public async Task ExecuteAsync_LocationOnAnotherOrigin_DropsCredentialsButKeepsOtherFields()
    {
        // Curl drops Authorization, Cookie and Proxy-Authorization on a cross-origin redirect, and so does the upload.
        var next = new ScriptedNext(PlainOptions, Response(201, "", "Location: https://store.example.net/u/1"), IetfAppended);
        var http = new HttpRequestOptions { Headers = ["Authorization: Bearer t", "cookie: a=b", "Proxy-Authorization: Basic x", "X-Trace: 1"], BearerToken = "t", UserAgent = "agent" };
        var credentials = new NetworkCredential("user", "secret");

        await new ResumableUploadTransferHook().ExecuteAsync(Context("http://up.example.com/files", http: http, credentials: credentials), next.SendAsync);

        var creation = next.Sent[1].Context;
        var append = next.Sent[2].Context;
        Assert.AreSame(credentials, creation.Credentials);
        Assert.AreEqual("t", creation.Http!.BearerToken);
        Assert.IsTrue(creation.Http.SendsCookieStrings);
        Assert.IsNull(append.Credentials);
        Assert.IsNull(append.Http!.BearerToken);
        Assert.IsFalse(append.Http.SendsCookieStrings);
        Assert.AreEqual("agent", append.Http.UserAgent);
        Assert.AreEqual("https://store.example.net/u/1", next.Sent[2].Url);
        CollectionAssert.AreEqual(new[] { "X-Trace: 1" }, append.Http.Headers.Skip(4).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_UnseekableOrPartlyReadUpload_IsSentFromWhereItStands()
    {
        var unseekable = new UnseekableUpload(Encoding.ASCII.GetBytes("hello"));
        var partlyRead = new MemoryStream(Encoding.ASCII.GetBytes("xxhello")) { Position = 2 };

        foreach (var upload in new Stream[] { unseekable, partlyRead })
        {
            var next = new ScriptedNext(PlainOptions, IetfCreated, IetfAppended);
            await new ResumableUploadTransferHook().ExecuteAsync(Context("http://up.example.com/files", upload: upload), next.SendAsync);
            Assert.AreEqual("hello", next.Sent[2].Upload);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_UnseekableUploadTooLargeForTheBuffer_IsSpooledResumedFromTheOffsetAndTheSpoolDisposed()
    {
        // BL-5967, ADR-1401: past the memory limit the upload goes to a spool file, so no length is too long for it.
        var spool = new MemoryStream();
        var next = new ScriptedNext(PlainOptions, IetfCreated, Failure(CurlExitCode.RecvError), Response(204, "", "Upload-Offset: 2"), IetfAppended);
        var upload = new UnseekableUpload(Encoding.ASCII.GetBytes("hello"));

        var result = await new ResumableUploadTransferHook(memoryLimit: 4, createSpoolFile: () => spool)
            .ExecuteAsync(Context("http://up.example.com/files", upload: upload), next.SendAsync);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hello", next.Sent[2].Upload);
        Assert.AreEqual("llo", next.Sent[4].Upload);
        Assert.AreEqual(5, result.Report!.UploadSize);
        Assert.IsFalse(spool.CanRead);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnseekableUploadPastTheBufferWithoutASpoolFactory_IsUploadedThroughATemporaryFile()
    {
        var next = new ScriptedNext(PlainOptions, IetfCreated, IetfAppended);
        var upload = new UnseekableUpload(Encoding.ASCII.GetBytes("hello"));

        var result = await new ResumableUploadTransferHook(memoryLimit: 0)
            .ExecuteAsync(Context("http://up.example.com/files", upload: upload), next.SendAsync);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hello", next.Sent[2].Upload);
    }

    [TestMethod]
    public async Task ExecuteAsync_SpoolFileThatCannotBeCreated_EndsWithWriteErrorAndSendsNothing()
    {
        var next = new ScriptedNext();
        var upload = new UnseekableUpload(Encoding.ASCII.GetBytes("hello"));

        var result = await new ResumableUploadTransferHook(memoryLimit: 0, createSpoolFile: () => throw new IOException("disk full"))
            .ExecuteAsync(Context("http://up.example.com/files", upload: upload), next.SendAsync);

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual("Failed writing the upload to a temporary file: disk full", result.ErrorMessage);
        Assert.IsEmpty(next.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailureThatCannotResume_EndsWithItsExitCodeAndSendsNoMore()
    {
        var next = new ScriptedNext(Failure(CurlExitCode.CouldntConnect, "Failed to connect"));

        var result = await new ResumableUploadTransferHook().ExecuteAsync(Context("http://up.example.com/files"), next.SendAsync);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect", result.ErrorMessage);
        Assert.HasCount(1, next.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_BrokenWithNoResumptionLeft_EndsWithTheBreaksExitCode()
    {
        var next = new ScriptedNext(PlainOptions, IetfCreated, Failure(CurlExitCode.RecvError, message: null));

        var result = await new ResumableUploadTransferHook(maxResumptions: 0).ExecuteAsync(Context("http://up.example.com/files"), next.SendAsync);

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(string.Empty, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("Upload-Offset: 9")]
    [DataRow("X-Other: 1")]
    public async Task ExecuteAsync_OffsetRetrievalWithoutAnOffsetInTheUpload_EndsWithWeirdServerReply(string field)
    {
        var next = new ScriptedNext(PlainOptions, IetfCreated, Failure(CurlExitCode.RecvError), Response(204, "", field));

        var result = await new ResumableUploadTransferHook().ExecuteAsync(Context("http://up.example.com/files"), next.SendAsync);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_OffsetRetrievalAnswers404WithFail_EndsWithHttpReturnedErrorAndWritesNothing()
    {
        var next = new ScriptedNext(PlainOptions, IetfCreated, Failure(CurlExitCode.RecvError), Response(404, "gone"));
        var context = Context("http://up.example.com/files", http: new HttpRequestOptions { Fail = HttpFailMode.Fail }, headerOutput: new MemoryStream());

        var result = await new ResumableUploadTransferHook().ExecuteAsync(context, next.SendAsync);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual(0, context.Output.Length);
        Assert.AreEqual(0, context.HeaderOutput!.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_CreationRefusedWithFailWithBody_WritesTheBodyAndEndsWithHttpReturnedError()
    {
        var next = new ScriptedNext(PlainOptions, Response(403, "no"));
        var context = Context("http://up.example.com/files", http: new HttpRequestOptions { Fail = HttpFailMode.FailWithBody });

        var result = await new ResumableUploadTransferHook().ExecuteAsync(context, next.SendAsync);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("no", Encoding.ASCII.GetString(((MemoryStream)context.Output).ToArray()));
        Assert.HasCount(2, next.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResultsWithoutAReport_AreTakenAsNoStatusAndEndTheUpload()
    {
        // A transfer that reports no response (status 0) offers no tus, and its creation is no success.
        var next = new ScriptedNext(Response(0), Response(0));

        var result = await new ResumableUploadTransferHook().ExecuteAsync(Context("http://up.example.com/files"), next.SendAsync);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0, result.Report!.ResponseCode);
        Assert.HasCount(2, next.Sent);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecuteAsync_CreationAnsweredWithoutLocation_FailsAndSendsNoBody(bool tus)
    {
        // EXT-045, ADR-1103: no upload resource, so no upload bytes are sent.
        var next = new ScriptedNext(tus ? TusOptions : PlainOptions, Response(200, "ok"));

        var result = await new ResumableUploadTransferHook().ExecuteAsync(Context("http://up.example.com/files"), next.SendAsync);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        StringAssert.Contains(result.ErrorMessage, "did not create an upload resource");
        Assert.IsNull(result.Report);
        Assert.HasCount(2, next.Sent);
    }

    [TestMethod]
    public void RequestContext_OverridesTheRequestsMembersAndPassesTheRestThrough()
    {
        var inner = new TransferContext { Url = CurlUrl.Parse("http://up.example.com/"), Output = new MemoryStream(), ResumeFrom = 3, NoBody = false, PostWithoutData = true };
        var url = CurlUrl.Parse("http://up.example.com/files/1");
        var http = new HttpRequestOptions();
        var output = new MemoryStream();
        var head = new MemoryStream();

        ITransferContext request = new ResumableUploadRequestContext(inner, url, http, Stream.Null, noBody: true, output, head, credentials: null);

        Assert.AreSame(url, request.Url);
        Assert.AreSame(output, request.Output);
        Assert.AreSame(Stream.Null, request.Upload);
        Assert.IsNull(request.ResumeFrom);
        Assert.IsFalse(request.ResumeUploadFromUnknownOffset);
        Assert.IsNull(request.Range);
        Assert.IsNull(request.RangeText);
        Assert.IsTrue(request.NoBody);
        Assert.AreSame(head, request.HeaderOutput);
        Assert.IsNull(request.DumpHeaderOutput);
        Assert.IsNull(request.PostData);
        Assert.IsFalse(request.PostWithoutData);
        Assert.IsNull(request.Credentials);
        Assert.AreSame(http, request.Http);
        Assert.AreEqual(
            (inner.MaxFileSize, inner.TimeCondition, inner.RemoteTime, inner.TelnetOptions, inner.TftpBlockSize, inner.TftpNoOptions, inner.FtpDisableEpsv, inner.FtpSkipPasvIp),
            (request.MaxFileSize, request.TimeCondition, request.RemoteTime, request.TelnetOptions, request.TftpBlockSize, request.TftpNoOptions, request.FtpDisableEpsv, request.FtpSkipPasvIp));
        Assert.AreEqual(
            (inner.FtpFileMethod, inner.FtpCreateDirectories, inner.FtpAccount, inner.FtpAlternativeToUser, inner.FtpSendPret, inner.ListOnly, inner.UseAscii, inner.Append),
            (request.FtpFileMethod, request.FtpCreateDirectories, request.FtpAccount, request.FtpAlternativeToUser, request.FtpSendPret, request.ListOnly, request.UseAscii, request.Append));
        Assert.AreEqual(
            (inner.FtpPort, inner.FtpUseEprt, inner.FtpAcceptTimeout, inner.FtpServerResponseTimeout, inner.SslLevel, inner.FtpSslControlOnly, inner.FtpCommandChannelClearing, inner.QuoteCommands),
            (request.FtpPort, request.FtpUseEprt, request.FtpAcceptTimeout, request.FtpServerResponseTimeout, request.SslLevel, request.FtpSslControlOnly, request.FtpCommandChannelClearing, request.QuoteCommands));
        Assert.AreEqual(
            (inner.ConvertLineEndings, inner.CreateFileMode, inner.PathAsIs, inner.ConnectTimeout, inner.MaxTime, inner.OperationStarted, inner.Proxy, inner.NoProxy),
            (request.ConvertLineEndings, request.CreateFileMode, request.PathAsIs, request.ConnectTimeout, request.MaxTime, request.OperationStarted, request.Proxy, request.NoProxy));
        Assert.AreEqual(
            (inner.Mail, inner.Ssh, inner.TimeProvider, inner.Events, inner.Progress, inner.CancellationToken, inner.DiagnosticLog),
            (request.Mail, request.Ssh, request.TimeProvider, request.Events, request.Progress, request.CancellationToken, request.DiagnosticLog));
    }

    private static string Describe(ScriptedNext next, int skip = 0) =>
        string.Join('|', next.Sent.Skip(skip).Select(sent => $"{sent.Method} {sent.Url} [{string.Join(',', sent.Headers)}] {sent.Upload ?? "-"}"));

    private static TransferContext Context(
        string url, Stream? upload = null, HttpRequestOptions? http = null, Stream? headerOutput = null, Stream? dumpHeaderOutput = null, NetworkCredential? credentials = null) => new()
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            Upload = upload ?? new MemoryStream(Encoding.ASCII.GetBytes("hello")),
            Http = http,
            Credentials = credentials,
            HeaderOutput = headerOutput,
            DumpHeaderOutput = dumpHeaderOutput,
        };
}
