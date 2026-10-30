using System.Text;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.ResumableUpload.Http;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>
/// Pins the resumable upload flows over fake connections: tus 1.0 ("Creation", "Core Protocol") and
/// draft-ietf-httpbis-resumable-upload (upload creation, upload append, offset retrieval), with resumption after a
/// broken connection and the last response as the transfer's.
/// </summary>
[TestClass]
public sealed class ResumableUploadTransferHookTests
{
    private const string TusOptions = "HTTP/1.1 204 No Content\r\nTus-Resumable: 1.0.0\r\nTus-Version: 1.0.0,0.2.2\r\nTus-Extension: creation,expiration\r\n\r\n";
    private const string TusCreated = "HTTP/1.1 201 Created\r\nLocation: /files/24e5\r\nTus-Resumable: 1.0.0\r\nContent-Length: 0\r\n\r\n";
    private const string TusPatched = "HTTP/1.1 204 No Content\r\nTus-Resumable: 1.0.0\r\nUpload-Offset: 5\r\n\r\n";
    private const string PlainOptions = "HTTP/1.1 200 OK\r\nAllow: GET, PUT\r\nContent-Length: 0\r\n\r\n";
    private const string IetfCreated = "HTTP/1.1 201 Created\r\nLocation: /uploads/7\r\nContent-Length: 0\r\n\r\n";
    private const string IetfAppended = "HTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\ndone";
    private const string Host = "Host: up.example.com\r\n";

    [TestMethod]
    public async Task ExecuteAsync_WithoutUpload_CallsNextUnchanged()
    {
        var context = Context("http://up.example.com/files", upload: null);

        var result = await new ResumableUploadTransferHook().ExecuteAsync(context, Services(new ScriptedConnector()), Next(context));

        Assert.AreSame(NextResult, result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SchemeOtherThanHttp_CallsNextUnchanged()
    {
        var context = Context("ftp://up.example.com/files");

        var result = await new ResumableUploadTransferHook().ExecuteAsync(context, Services(new ScriptedConnector()), Next(context));

        Assert.AreSame(NextResult, result);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutServices_CallsNextForOtherTransfersAndFailsAnUpload()
    {
        var plain = Context("https://up.example.com/files", upload: null);

        var passed = await new ResumableUploadTransferHook().ExecuteAsync(plain, Next(plain));
        var failed = await new ResumableUploadTransferHook().ExecuteAsync(Context("HTTPS://up.example.com/files"), Next(plain));

        Assert.AreSame(NextResult, passed);
        Assert.AreEqual(CurlExitCode.FailedInit, failed.ExitCode);
        Assert.AreEqual(ResumableUploadTransferHook.NoServicesMessage, failed.ErrorMessage);
    }

    [TestMethod]
    public void Constructor_NegativeResumptions_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ResumableUploadTransferHook(-1));
        Assert.AreEqual(ResumableUploadTransferHook.DefaultMaxResumptions, new ResumableUploadTransferHook().MaxResumptions);
    }

    [TestMethod]
    public async Task ExecuteAsync_TusServer_CreatesWithPostAndAppendsWithPatch()
    {
        // tus 1.0 "Creation": POST with Upload-Length, 201 with Location; "Core Protocol": PATCH with Upload-Offset and
        // Content-Type: application/offset+octet-stream, 204 with the new offset.
        var connection = new RecordedConnection(TusOptions + TusCreated + TusPatched);
        var context = Context("http://up.example.com/files?x=1", http: new HttpRequestOptions { Fail = HttpFailMode.Fail });

        var result = await new ResumableUploadTransferHook().ExecuteAsync(context, Services(new ScriptedConnector(ConnectResult.Connected(connection))), NeverSent);

        Assert.AreEqual(
            "OPTIONS /files?x=1 HTTP/1.1\r\n" + Host + "\r\n"
            + "POST /files?x=1 HTTP/1.1\r\n" + Host + "Tus-Resumable: 1.0.0\r\nUpload-Length: 5\r\nContent-Length: 0\r\n\r\n"
            + "PATCH /files/24e5 HTTP/1.1\r\n" + Host + "Tus-Resumable: 1.0.0\r\nUpload-Offset: 0\r\nContent-Type: application/offset+octet-stream\r\nContent-Length: 5\r\n\r\nhello",
            connection.Written);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(204, result.Report!.ResponseCode);
        Assert.AreEqual("PATCH", result.Report.Method);
        Assert.AreEqual(5, result.Report.UploadSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_TusPatchBrokenMidBody_RetrievesTheOffsetWithHeadAndPatchesFromIt()
    {
        // tus 1.0 "Core Protocol": after a broken PATCH the client sends HEAD and resumes from its Upload-Offset.
        var broken = new RecordedConnection(TusOptions + TusCreated, writesBeforeBreak: 3);
        var resumed = new RecordedConnection("HTTP/1.1 200 OK\r\nUpload-Offset: 3\r\nUpload-Length: 5\r\nTus-Resumable: 1.0.0\r\n\r\n" + TusPatched);
        var connector = new ScriptedConnector(ConnectResult.Connected(broken), ConnectResult.Connected(resumed));

        var result = await new ResumableUploadTransferHook().ExecuteAsync(Context("http://up.example.com/files"), Services(connector), NeverSent);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsTrue(broken.IsDisposed);
        Assert.AreEqual(
            "HEAD /files/24e5 HTTP/1.1\r\n" + Host + "Tus-Resumable: 1.0.0\r\n\r\n"
            + "PATCH /files/24e5 HTTP/1.1\r\n" + Host + "Tus-Resumable: 1.0.0\r\nUpload-Offset: 3\r\nContent-Type: application/offset+octet-stream\r\nContent-Length: 2\r\n\r\nlo",
            resumed.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_IetfServer_CreatesWithUploadCompleteFalseAndAppendsTheWhole()
    {
        // draft-ietf-httpbis-resumable-upload "Upload Creation" (Upload-Complete: ?0, Upload-Length) and "Upload Append"
        // (PATCH, application/partial-upload, Upload-Offset, Upload-Complete: ?1); -H lines follow the flow's fields.
        var connection = new RecordedConnection(PlainOptions + IetfCreated + IetfAppended);
        var headerOutput = new MemoryStream();
        var dumpHeaderOutput = new MemoryStream();
        var context = Context(
            "http://up.example.com/files",
            http: new HttpRequestOptions { Headers = ["Authorization: Bearer t", "host: evil", "Content-Length: 9", "NoColon", "Empty:"] },
            headerOutput: headerOutput,
            dumpHeaderOutput: dumpHeaderOutput);

        var result = await new ResumableUploadTransferHook().ExecuteAsync(context, Services(new ScriptedConnector(ConnectResult.Connected(connection))), NeverSent);

        Assert.AreEqual(
            "OPTIONS /files HTTP/1.1\r\n" + Host + "Authorization: Bearer t\r\n\r\n"
            + "PUT /files HTTP/1.1\r\n" + Host + "Upload-Draft-Interop-Version: 6\r\nUpload-Complete: ?0\r\nUpload-Length: 5\r\nAuthorization: Bearer t\r\nContent-Length: 0\r\n\r\n"
            + "PATCH /uploads/7 HTTP/1.1\r\n" + Host + "Upload-Draft-Interop-Version: 6\r\nUpload-Complete: ?1\r\nUpload-Offset: 0\r\nContent-Type: application/partial-upload\r\nAuthorization: Bearer t\r\nContent-Length: 5\r\n\r\nhello",
            connection.Written);
        Assert.AreEqual("done", Encoding.ASCII.GetString(((MemoryStream)context.Output).ToArray()));
        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\n", Encoding.ASCII.GetString(headerOutput.ToArray()));
        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\n", Encoding.ASCII.GetString(dumpHeaderOutput.ToArray()));
        Assert.AreEqual(TransferResult.Success(4).BytesTransferred, result.BytesTransferred);
        Assert.AreEqual(200, result.Report!.ResponseCode);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 404 Not Found\r\nTus-Version: 1.0.0\r\nTus-Extension: creation\r\nContent-Length: 0\r\n\r\n")]
    [DataRow("HTTP/1.1 204 No Content\r\nTus-Extension: creation\r\n\r\n")]
    [DataRow("HTTP/1.1 204 No Content\r\nTus-Version: 0.2.2\r\nTus-Extension: creation\r\n\r\n")]
    [DataRow("HTTP/1.1 204 No Content\r\nTus-Version: 1.0.0\r\n\r\n")]
    [DataRow("HTTP/1.1 204 No Content\r\nTus-Version: 1.0.0\r\nTus-Extension: expiration\r\n\r\n")]
    public async Task ExecuteAsync_OptionsWithoutTusCreation_UsesTheIetfDraftWithCurlsMethod(string options)
    {
        var connection = new RecordedConnection(options + IetfCreated + IetfAppended);
        var context = Context("http://up.example.com/files", http: new HttpRequestOptions { CustomMethod = "POST" });

        await new ResumableUploadTransferHook().ExecuteAsync(context, Services(new ScriptedConnector(ConnectResult.Connected(connection))), NeverSent);

        StringAssert.Contains(connection.Written, "POST /files HTTP/1.1\r\n" + Host + "Upload-Draft-Interop-Version: 6\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_IetfAppendResponseLost_RetrievesTheOffsetAndAppendsFromIt()
    {
        // draft-ietf-httpbis-resumable-upload "Offset Retrieval": HEAD gives Upload-Offset; the append resumes from it.
        var broken = new RecordedConnection(PlainOptions + IetfCreated);
        var resumed = new RecordedConnection("HTTP/1.1 204 No Content\r\nUpload-Offset: 4\r\nUpload-Complete: ?0\r\n\r\n" + IetfAppended);
        var connector = new ScriptedConnector(ConnectResult.Connected(broken), ConnectResult.Connected(resumed));

        var result = await new ResumableUploadTransferHook().ExecuteAsync(Context("http://up.example.com/files"), Services(connector), NeverSent);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(
            "HEAD /uploads/7 HTTP/1.1\r\n" + Host + "Upload-Draft-Interop-Version: 6\r\n\r\n"
            + "PATCH /uploads/7 HTTP/1.1\r\n" + Host + "Upload-Draft-Interop-Version: 6\r\nUpload-Complete: ?1\r\nUpload-Offset: 4\r\nContent-Type: application/partial-upload\r\nContent-Length: 1\r\n\r\no",
            resumed.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_ClosedConnectionOrLocationOnAnotherHost_ConnectsAgainToTheRightTarget()
    {
        var options = new RecordedConnection("HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 0\r\n\r\n");
        var creation = new RecordedConnection("HTTP/1.1 201 Created\r\nLocation: https://store.example.net:8443/u/1\r\nContent-Length: 0\r\n\r\n");
        var append = new RecordedConnection(IetfAppended);
        var connector = new ScriptedConnector(ConnectResult.Connected(options), ConnectResult.Connected(creation), ConnectResult.Connected(append));

        var result = await new ResumableUploadTransferHook().ExecuteAsync(Context("http://up.example.com/files"), Services(connector), NeverSent);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsTrue(options.IsDisposed);
        Assert.IsTrue(creation.IsDisposed);
        Assert.AreEqual(new ConnectTarget("store.example.net", 8443, true), connector.Targets[2]);
        StringAssert.StartsWith(append.Written, "PATCH /u/1 HTTP/1.1\r\nHost: store.example.net:8443\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_UnseekableOrPartlyReadUpload_IsBufferedFromWhereItStands()
    {
        var partlyRead = new MemoryStream(Encoding.ASCII.GetBytes("xhello")) { Position = 1 };
        var unseekable = new UnseekableStream(Encoding.ASCII.GetBytes("hello"));
        var first = new RecordedConnection(PlainOptions + IetfCreated + IetfAppended);
        var second = new RecordedConnection(PlainOptions + IetfCreated + IetfAppended);

        await new ResumableUploadTransferHook().ExecuteAsync(Context("http://up.example.com/files", partlyRead), Services(new ScriptedConnector(ConnectResult.Connected(first))), NeverSent);
        await new ResumableUploadTransferHook().ExecuteAsync(Context("http://up.example.com/files", unseekable), Services(new ScriptedConnector(ConnectResult.Connected(second))), NeverSent);

        StringAssert.EndsWith(first.Written, "Content-Length: 5\r\n\r\nhello");
        StringAssert.EndsWith(second.Written, "Content-Length: 5\r\n\r\nhello");
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_EndsWithTheConnectorsExitCode()
    {
        var connector = new ScriptedConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect to up.example.com port 80"));

        var result = await new ResumableUploadTransferHook().ExecuteAsync(Context("http://up.example.com/files"), Services(connector), NeverSent);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to up.example.com port 80", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_BrokenWithNoResumptionLeft_EndsWithSendError()
    {
        var broken = new RecordedConnection(TusOptions + TusCreated, writesBeforeBreak: 3);

        var result = await new ResumableUploadTransferHook(0).ExecuteAsync(
            Context("http://up.example.com/files"), Services(new ScriptedConnector(ConnectResult.Connected(broken))), NeverSent);

        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("Send failure: The connection was reset.", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResponseLostWithNoResumptionLeft_EndsWithRecvError()
    {
        var broken = new RecordedConnection(PlainOptions);

        var result = await new ResumableUploadTransferHook(0).ExecuteAsync(
            Context("http://up.example.com/files"), Services(new ScriptedConnector(ConnectResult.Connected(broken))), NeverSent);

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        StringAssert.StartsWith(result.ErrorMessage, "Recv failure: ");
    }

    [TestMethod]
    public async Task ExecuteAsync_MalformedResponse_EndsWithWeirdServerReply()
    {
        var connection = new RecordedConnection("garbage\r\n\r\n");

        var result = await new ResumableUploadTransferHook().ExecuteAsync(
            Context("http://up.example.com/files"), Services(new ScriptedConnector(ConnectResult.Connected(connection))), NeverSent);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 204 No Content\r\nUpload-Complete: ?0\r\n\r\n")]
    [DataRow("HTTP/1.1 204 No Content\r\nUpload-Offset: 6\r\n\r\n")]
    public async Task ExecuteAsync_OffsetRetrievalWithoutAnOffsetInTheUpload_EndsWithWeirdServerReply(string retrieval)
    {
        var connector = new ScriptedConnector(
            ConnectResult.Connected(new RecordedConnection(PlainOptions + IetfCreated)), ConnectResult.Connected(new RecordedConnection(retrieval)));

        var result = await new ResumableUploadTransferHook().ExecuteAsync(Context("http://up.example.com/files"), Services(connector), NeverSent);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("The offset retrieval response gave no Upload-Offset within the upload's 5 bytes.", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_OffsetRetrievalAnswers404WithFail_EndsWithHttpReturnedErrorAndWritesNothing()
    {
        var connector = new ScriptedConnector(
            ConnectResult.Connected(new RecordedConnection(PlainOptions + IetfCreated)),
            ConnectResult.Connected(new RecordedConnection("HTTP/1.1 404 Not Found\r\n\r\n")));
        var headerOutput = new MemoryStream();
        var context = Context("http://up.example.com/files", http: new HttpRequestOptions { Fail = HttpFailMode.Fail }, headerOutput: headerOutput);

        var result = await new ResumableUploadTransferHook().ExecuteAsync(context, Services(connector), NeverSent);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("The requested URL returned error: 404", result.ErrorMessage);
        Assert.AreEqual("HEAD", result.Report!.Method);
        Assert.AreEqual(0, headerOutput.Length);
        Assert.AreEqual(0, context.Output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_CreationRefusedWithFailWithBody_WritesTheBodyAndEndsWithHttpReturnedError()
    {
        var connection = new RecordedConnection(PlainOptions + "HTTP/1.1 413 Content Too Large\r\nContent-Length: 7\r\n\r\ntoo big");
        var context = Context("http://up.example.com/files", http: new HttpRequestOptions { Fail = HttpFailMode.FailWithBody });

        var result = await new ResumableUploadTransferHook().ExecuteAsync(context, Services(new ScriptedConnector(ConnectResult.Connected(connection))), NeverSent);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("too big", Encoding.ASCII.GetString(((MemoryStream)context.Output).ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_CreationWithoutLocation_EndsWithTheCreationResponse()
    {
        var connection = new RecordedConnection(PlainOptions + "HTTP/1.1 201 Created\r\nContent-Length: 2\r\n\r\nok");
        var context = Context("http://up.example.com/files");

        var result = await new ResumableUploadTransferHook().ExecuteAsync(context, Services(new ScriptedConnector(ConnectResult.Connected(connection))), NeverSent);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(201, result.Report!.ResponseCode);
        Assert.AreEqual("ok", Encoding.ASCII.GetString(((MemoryStream)context.Output).ToArray()));
    }

    private static readonly TransferResult NextResult = TransferResult.Success(42);

    private static ValueTask<TransferResult> NeverSent(ITransferContext context) => throw new AssertFailedException("curl's handler ran.");

    private static Func<ITransferContext, ValueTask<TransferResult>> Next(ITransferContext expected) => given =>
    {
        Assert.AreSame(expected, given);
        return ValueTask.FromResult(NextResult);
    };

    private static TransferHookServices Services(IConnector connector) => new(connector, new UnusedListener());

    private static TransferContext Context(
        string url, Stream? upload = null, HttpRequestOptions? http = null, Stream? headerOutput = null, Stream? dumpHeaderOutput = null) => new()
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            Upload = upload ?? new MemoryStream(Encoding.ASCII.GetBytes("hello")),
            Http = http,
            HeaderOutput = headerOutput,
            DumpHeaderOutput = dumpHeaderOutput,
        };

    private static TransferContext Context(string url, Stream? upload) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = new MemoryStream(),
        Upload = upload,
    };

    private sealed class UnusedListener : IConnectionListener
    {
        public ValueTask<ListenResult> ListenAsync(ListenTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("The listener was used.");
    }

    private sealed class UnseekableStream(byte[] content) : MemoryStream(content)
    {
        public override bool CanSeek => false;
    }
}
