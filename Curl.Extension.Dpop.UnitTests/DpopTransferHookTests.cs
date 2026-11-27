using System.Security.Cryptography;
using System.Text;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.Dpop.UnitTests;

[TestClass]
public sealed class DpopTransferHookTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private string _directory = null!;
    private string _keyFile = null!;

    [TestInitialize]
    public void WriteKey()
    {
        _directory = Directory.CreateTempSubdirectory("dpop-").FullName;
        _keyFile = Path.Combine(_directory, "key.pem");
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        File.WriteAllText(_keyFile, ecdsa.ExportECPrivateKeyPem());
    }

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    public async Task ExecuteAsync_AnHttpRequest_SendsADpopHeaderProvingItsMethodTargetAndTime_Rfc9449Sections4Point1And4Point2()
    {
        var server = new FakeServer(Response(200));

        await Hook().ExecuteAsync(Context("https://Server.Example.COM:8443/r/x?q=1#f", new HttpRequestOptions(), postData: true), server.Invoke);

        var proof = ProofOf(server.Requests.Single());
        var payload = DpopProofTests.Decode(proof.Split('.')[1]);
        Assert.AreEqual("POST", payload.GetProperty("htm").GetString());
        Assert.AreEqual("https://server.example.com:8443/r/x", payload.GetProperty("htu").GetString());
        Assert.AreEqual(Now.ToUnixTimeSeconds(), payload.GetProperty("iat").GetInt64());
        Assert.IsFalse(payload.TryGetProperty("ath", out _));
        Assert.IsFalse(payload.TryGetProperty("nonce", out _));
        Assert.IsFalse(server.Requests.Single().Http!.Headers.Any(header => header.StartsWith("Authorization", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoRequests_GiveEachProofItsOwnJti_Rfc9449Section11Point1()
    {
        var server = new FakeServer(Response(200), Response(200));
        var hook = Hook();

        await hook.ExecuteAsync(Context(), server.Invoke);
        await hook.ExecuteAsync(Context(), server.Invoke);

        var ids = server.Requests.Select(request => DpopProofTests.Decode(ProofOf(request).Split('.')[1]).GetProperty("jti").GetString()).ToArray();
        Assert.AreNotEqual(ids[0], ids[1]);
        Assert.AreEqual(22, ids[0]!.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_ABearerToken_IsSentAsADpopTokenBoundByAth_Rfc9449Section7Point1()
    {
        var server = new FakeServer(Response(200));

        await Hook().ExecuteAsync(Context(http: new HttpRequestOptions { BearerToken = "Kz~8mXK1EalYznwH-LC-1fBAo.4Ljp~zsPE_NeO.gxU" }), server.Invoke);

        var request = server.Requests.Single();
        Assert.Contains("Authorization: DPoP Kz~8mXK1EalYznwH-LC-1fBAo.4Ljp~zsPE_NeO.gxU", request.Http!.Headers);
        Assert.AreEqual("fUHyO2r2Z3DZ53EsNrWBb0xWXoaNy59IiKCAqksmQEo", DpopProofTests.Decode(ProofOf(request).Split('.')[1]).GetProperty("ath").GetString());
    }

    [TestMethod]
    public async Task ExecuteAsync_ADpopAuthorizationHeaderGiven_BindsItsTokenWithoutAddingAnother_Rfc9449Section7Point1()
    {
        var server = new FakeServer(Response(200));

        await Hook().ExecuteAsync(Context(http: new HttpRequestOptions { Headers = ["Accept: */*", "authorization: dpop  tok "] }), server.Invoke);

        var request = server.Requests.Single();
        Assert.AreEqual(1, request.Http!.Headers.Count(header => header.StartsWith("authorization", StringComparison.OrdinalIgnoreCase)));
        Assert.AreEqual(DpopProof.AccessTokenHashOf("tok"), DpopProofTests.Decode(ProofOf(request).Split('.')[1]).GetProperty("ath").GetString());
    }

    [TestMethod]
    public async Task ExecuteAsync_AServerDemandingANonce_IsRetriedOnceWithItAndOnlyTheRetryIsWritten_Rfc9449Sections8And9()
    {
        var server = new FakeServer(Response(401, "n-1", "refused"), Response(200, body: "ok"));
        var context = Context();

        var result = await Hook().ExecuteAsync(context, server.Invoke);

        Assert.HasCount(2, server.Requests);
        Assert.IsFalse(DpopProofTests.Decode(ProofOf(server.Requests[0]).Split('.')[1]).TryGetProperty("nonce", out _));
        Assert.AreEqual("n-1", DpopProofTests.Decode(ProofOf(server.Requests[1]).Split('.')[1]).GetProperty("nonce").GetString());
        Assert.AreEqual(200, result.Report!.ResponseCode);
        Assert.AreEqual("ok", Text(context.Output));
        Assert.AreEqual("HTTP/1.1 200\r\n\r\n", Text(context.HeaderOutput!));
        Assert.AreEqual("HTTP/1.1 200\r\n\r\n", Text(context.DumpHeaderOutput!));
    }

    [TestMethod]
    public async Task ExecuteAsync_EachRequestAnOuterHookSends_IsSignedForItsOwnMethodAndTarget_Rfc9449Section4Point2()
    {
        var server = new FakeServer(Response(200), Response(201), Response(204));
        var hook = Hook();

        foreach (var (method, url) in new[] { ("OPTIONS", "https://a.example/tus"), ("POST", "https://a.example/tus"), ("PATCH", "https://a.example/files/1") })
        {
            await hook.ExecuteAsync(Context(url, new HttpRequestOptions { CustomMethod = method }), server.Invoke);
        }

        var proofs = server.Requests.Select(request => DpopProofTests.Decode(ProofOf(request).Split('.')[1])).ToList();
        CollectionAssert.AreEqual(new[] { "OPTIONS", "POST", "PATCH" }, proofs.Select(proof => proof.GetProperty("htm").GetString()).ToArray());
        CollectionAssert.AreEqual(
            new[] { "https://a.example/tus", "https://a.example/tus", "https://a.example/files/1" },
            proofs.Select(proof => proof.GetProperty("htu").GetString()).ToArray());
        Assert.AreEqual(3, proofs.Select(proof => proof.GetProperty("jti").GetString()).Distinct().Count());
    }

    [TestMethod]
    public async Task ExecuteAsync_ASeekableUploadRefusedForANonce_IsSentAgainFromWhereItStarted_Rfc9449Section9()
    {
        var upload = new MemoryStream("xxbody"u8.ToArray()) { Position = 2 };

        var sent = await SendUploadRefusedForANonceAsync(upload);

        CollectionAssert.AreEqual(new[] { "body", "body" }, sent);
    }

    [TestMethod]
    [DataRow(false, null, 2, 0)]
    [DataRow(true, null, 2, 1)]
    [DataRow(true, "seek callback returned error 1", 1, 1)]
    public async Task ExecuteAsync_ARewinderUploadRefusedForANonce_IsRetriedOnlyWhenItNeedsNoRewindOrRewinds_Rfc9449Section9(bool read, string? failure, int tries, int rewinds)
    {
        var upload = new RewinderUpload(read, failure);

        var sent = await SendUploadRefusedForANonceAsync(upload);

        Assert.HasCount(tries, sent);
        Assert.AreEqual(rewinds, upload.Rewinds);
    }

    [TestMethod]
    public async Task ExecuteAsync_AnUploadThatCannotBeRewoundRefusedForANonce_KeepsTheRefusal_Rfc9449Section9()
    {
        var sent = await SendUploadRefusedForANonceAsync(new UnseekableUpload());

        Assert.HasCount(1, sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_AnAuthorizationServerDemandingANonce_IsRetriedAndTheNonceKeptForLaterRequests_Rfc9449Section8()
    {
        var server = new FakeServer(Response(400, "n-1"), Response(200), Response(200, "n-2"), Response(200));
        var hook = Hook();

        await hook.ExecuteAsync(Context(), server.Invoke);
        await hook.ExecuteAsync(Context(), server.Invoke);
        await hook.ExecuteAsync(Context(), server.Invoke);

        Assert.HasCount(4, server.Requests);
        Assert.AreEqual("n-1", NonceOf(server.Requests[2]));
        Assert.AreEqual("n-2", NonceOf(server.Requests[3]));
    }

    [TestMethod]
    public async Task ExecuteAsync_ARefusalWithTheNonceAlreadySentOrAnotherCode_IsNotRetried_Rfc9449Section8()
    {
        var server = new FakeServer(Response(400, "n-1"), Response(401, "n-1"), Response(403, "n-2"), TransferResult.Failure(CurlExitCode.CouldntConnect, "refused"));
        var hook = Hook();

        await hook.ExecuteAsync(Context(), server.Invoke);
        var failed = await hook.ExecuteAsync(Context(), server.Invoke);
        var refused = await hook.ExecuteAsync(Context(), server.Invoke);

        Assert.HasCount(4, server.Requests);
        Assert.AreEqual(403, failed.Report!.ResponseCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, refused.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoncesOfOneOrigin_AreNotSentToAnother_Rfc9449Section8()
    {
        var server = new FakeServer(Response(200, "n-1"), Response(200));
        var hook = Hook();

        await hook.ExecuteAsync(Context("https://a.example/r"), server.Invoke);
        await hook.ExecuteAsync(Context("https://a.example:444/r"), server.Invoke);

        Assert.IsNull(NonceOf(server.Requests[1]));
    }

    [TestMethod]
    public async Task ExecuteAsync_ATransferWithoutHeaderOutputs_WritesOnlyTheBody()
    {
        var server = new FakeServer(Response(200, body: "ok"));
        var context = Context(headerOutputs: false);

        await Hook().ExecuteAsync(context, server.Invoke);

        Assert.AreEqual("ok", Text(context.Output));
    }

    [TestMethod]
    public async Task ExecuteAsync_AKeyFileThatCannotBeRead_FailsWithExit26WithoutSending()
    {
        var server = new FakeServer();
        var missing = Path.Combine(_directory, "missing.pem");

        var result = await new DpopTransferHook(missing).ExecuteAsync(Context(), server.Invoke);

        Assert.AreEqual(CurlExitCode.ReadError, result.ExitCode);
        Assert.AreEqual($"Failed to read a DPoP private key from {missing}", result.ErrorMessage);
        Assert.IsEmpty(server.Requests);
    }

    [TestMethod]
    public async Task ExecuteAsync_ATransferThatIsNotHttp_RunsAsWithoutTheHook()
    {
        var context = new TransferContext { Url = CurlUrl.Parse("ftp://example.com/r"), Output = new MemoryStream() };
        ITransferContext? sent = null;

        await new DpopTransferHook(Path.Combine(_directory, "missing.pem")).ExecuteAsync(context, given =>
        {
            sent = given;
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        Assert.AreSame(context, sent);
    }

    [TestMethod]
    public void MethodOf_EachRequestShape_NamesTheMethodCurlSends_Rfc9449Section4Point2()
    {
        Assert.AreEqual("PATCH", DpopTransferHook.MethodOf(Context(http: new HttpRequestOptions { CustomMethod = "PATCH" })));
        Assert.AreEqual("HEAD", DpopTransferHook.MethodOf(new TransferContext { Url = CurlUrl.Parse("http://a/"), Output = Stream.Null, NoBody = true, Http = new HttpRequestOptions() }));
        Assert.AreEqual("POST", DpopTransferHook.MethodOf(Context(postData: true)));
        Assert.AreEqual("POST", DpopTransferHook.MethodOf(new TransferContext { Url = CurlUrl.Parse("http://a/"), Output = Stream.Null, PostWithoutData = true, Http = new HttpRequestOptions() }));
        Assert.AreEqual("POST", DpopTransferHook.MethodOf(Context(http: new HttpRequestOptions { Body = new BytesBody(ReadOnlyMemory<byte>.Empty, "text/plain") })));
        Assert.AreEqual("PUT", DpopTransferHook.MethodOf(new TransferContext { Url = CurlUrl.Parse("http://a/"), Output = Stream.Null, Upload = Stream.Null, Http = new HttpRequestOptions() }));
        Assert.AreEqual("GET", DpopTransferHook.MethodOf(Context()));
    }

    [TestMethod]
    public void TargetUriOf_AUrl_DropsItsQueryAndFragmentAndDefaultPort_Rfc9449Section4Point3()
    {
        Assert.AreEqual("http://a.example/p", DpopTransferHook.TargetUriOf(CurlUrl.Parse("HTTP://A.example:80/p?x#y")));
        Assert.AreEqual("https://[::1]:8443/", DpopTransferHook.TargetUriOf(CurlUrl.Parse("https://[::1]:8443")));
    }

    /// <summary>Sends <paramref name="upload" /> to a server that refuses the first try with a nonce, and gives what each try read of it.</summary>
    private async Task<string[]> SendUploadRefusedForANonceAsync(Stream upload)
    {
        var server = new FakeServer(Response(401, "n-1"), Response(200));
        var sent = new List<string>();
        var context = new TransferContext { Url = CurlUrl.Parse("https://a.example/r"), Output = new MemoryStream(), HeaderOutput = new MemoryStream(), Upload = upload, Http = new HttpRequestOptions() };

        await Hook().ExecuteAsync(context, async request =>
        {
            using var read = new MemoryStream();
            await request.Upload!.CopyToAsync(read);
            sent.Add(Text(read));
            return await server.Invoke(request);
        });

        return [.. sent];
    }

    [TestMethod]
    public async Task ExecuteAsync_AStreamedRequestBodyRefusedForANonce_IsSentWholeAgain_Rfc9449Section9()
    {
        using var body = new MemoryStream("--form body"u8.ToArray()) { Position = 2 };

        var sent = await SendStreamBodyRefusedForANonceAsync(body);

        CollectionAssert.AreEqual(new[] { "form body", "form body" }, sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_AStreamedRequestBodyThatCannotBeRewoundRefusedForANonce_KeepsTheRefusal_Rfc9449Section9()
    {
        using var body = new UnseekableUpload();

        var sent = await SendStreamBodyRefusedForANonceAsync(body);

        Assert.HasCount(1, sent);
    }

    [TestMethod]
    [DataRow("grpc://A.example:50051/pkg.Service/Call", "http://a.example:50051/pkg.Service/Call")]
    [DataRow("GRPCS://a.example/pkg.Service/Call", "https://a.example/pkg.Service/Call")]
    public async Task ExecuteAsync_AGrpcCall_ProvesTheHttpPostThatCarriesIt_Rfc9449Section4Point2(string url, string target)
    {
        var server = new FakeServer(Response(200));

        await Hook().ExecuteAsync(Context(url), server.Invoke);

        var claims = DpopProofTests.Decode(ProofOf(server.Requests[0]).Split('.')[1]);
        Assert.AreEqual("POST", claims.GetProperty("htm").GetString());
        Assert.AreEqual(target, claims.GetProperty("htu").GetString());
    }

    [TestMethod]
    public async Task ExecuteAsync_AFurtherRequestOfTheTransfer_ReplacesOnlyItsOwnProofWithOneForThatRequest_Rfc9449Section4Point2()
    {
        var server = new FakeServer(Response(401, "n-1"), Response(200));
        var hook = Hook();
        await hook.ExecuteAsync(Context("https://a.example/r"), server.Invoke);
        var request = server.Requests[1];

        var signed = request.SignedHeadersFor("PATCH", CurlUrl.Parse("https://a.example/other?q=1"), ["X-A: 1", "DPoP: mine", .. request.Http!.Headers]);

        var proof = signed[2]["DPoP: ".Length..];
        CollectionAssert.AreEqual(new[] { "X-A: 1", "DPoP: mine", "DPoP: " + proof }, signed.ToArray());
        var claims = DpopProofTests.Decode(proof.Split('.')[1]);
        Assert.AreEqual("PATCH", claims.GetProperty("htm").GetString());
        Assert.AreEqual("https://a.example/other", claims.GetProperty("htu").GetString());
        Assert.AreEqual("n-1", claims.GetProperty("nonce").GetString());
        Assert.AreNotEqual(DpopProofTests.Decode(ProofOf(request).Split('.')[1]).GetProperty("jti").GetString(), claims.GetProperty("jti").GetString());
    }

    private async Task<string[]> SendStreamBodyRefusedForANonceAsync(Stream body)
    {
        var server = new FakeServer(Response(401, "n-1"), Response(200));
        var sent = new List<string>();
        var context = Context(http: new HttpRequestOptions { Body = new StreamBody(body, null, "multipart/form-data") });

        await Hook().ExecuteAsync(context, async request =>
        {
            using var read = new MemoryStream();
            await ((StreamBody)request.Http!.Body!).Content.CopyToAsync(read);
            sent.Add(Text(read));
            return await server.Invoke(request);
        });

        return [.. sent];
    }

    private static string? NonceOf(ITransferContext request) =>
        DpopProofTests.Decode(ProofOf(request).Split('.')[1]).TryGetProperty("nonce", out var nonce) ? nonce.GetString() : null;

    private static string ProofOf(ITransferContext request) => request.Http!.Headers.Single(header => header.StartsWith("DPoP: ", StringComparison.Ordinal))[6..];

    private static string Text(Stream stream) => Encoding.Latin1.GetString(((MemoryStream)stream).ToArray());

    private static (TransferResult Result, string Body) Response(int code, string? nonce = null, string body = "") =>
        (TransferResult.Success(body.Length) with
        {
            Report = new TransferReport { ResponseCode = code, ResponseHeaders = nonce is null ? [] : [new("DPoP-Nonce", nonce)] },
        }, body);

    private DpopTransferHook Hook() => new(_keyFile);

    private static TransferContext Context(string url = "https://a.example/r", HttpRequestOptions? http = null, bool postData = false, bool headerOutputs = true) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = new MemoryStream(),
        HeaderOutput = headerOutputs ? new MemoryStream() : null,
        DumpHeaderOutput = headerOutputs ? new MemoryStream() : null,
        PostData = postData ? "a=1"u8.ToArray() : (ReadOnlyMemory<byte>?)null,
        Http = http ?? new HttpRequestOptions(),
        TimeProvider = new FixedTimeProvider(Now),
    };

    /// <summary>Stands in for curl's handler: answers each request with the next response, writing its head and body.</summary>
    private sealed class FakeServer(params object[] responses)
    {
        public List<ITransferContext> Requests { get; } = [];

        public async ValueTask<TransferResult> Invoke(ITransferContext context)
        {
            var response = responses[Requests.Count];
            Requests.Add(context);
            if (response is not ValueTuple<TransferResult, string>(var result, var body))
            {
                return (TransferResult)response;
            }

            var head = Encoding.Latin1.GetBytes("HTTP/1.1 " + result.Report!.ResponseCode + "\r\n\r\n");
            await context.HeaderOutput!.WriteAsync(head);
            if (context.DumpHeaderOutput is { } dump)
            {
                await dump.WriteAsync(head);
            }

            await context.Output.WriteAsync(Encoding.Latin1.GetBytes(body));
            return result;
        }
    }

    /// <summary>An upload that cannot seek, such as a pipe.</summary>
    private class UnseekableUpload : MemoryStream
    {
        public override bool CanSeek => false;
    }

    /// <summary>An upload that cannot seek but rewinds through <see cref="IUploadRewinder" />, or fails to with a message.</summary>
    private sealed class RewinderUpload(bool read, string? failure) : UnseekableUpload, IUploadRewinder
    {
        public int Rewinds { get; private set; }

        public bool HasBeenRead => read;

        public string? RewindToStart()
        {
            Rewinds++;
            return failure;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
