using System.Security.Cryptography;
using System.Text;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.ContentDigest.UnitTests;

[TestClass]
public sealed class ContentDigestTransferHookTests
{
    private const string HelloWorld = "{\"hello\": \"world\"}";

    private const string HelloWorldSha256 = "sha-256=:X48E9qOokqqrvdts8nOJRJN3OWDUoyWxBf7kbu9DBPE=:";

    private static readonly string HelloWorldSha512 = "sha-512=:" + Convert.ToBase64String(SHA512.HashData(Encoding.ASCII.GetBytes(HelloWorld))) + ":";

    private readonly ITransferHook _hook = new ContentDigestTransferHook();

    [TestMethod]
    public void RequestHeadersFor_ADataBody_SendsItsSha256ContentDigestAndReprDigestAndAsksForTheServers_Rfc9530Sections2To4()
    {
        var context = Context(new HttpRequestOptions { Body = new BytesBody(Encoding.ASCII.GetBytes(HelloWorld), "application/json") });

        var headers = _hook.RequestHeadersFor(context).ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "Want-Content-Digest: sha-256=10, sha-512=5",
                "Want-Repr-Digest: sha-256=10, sha-512=5",
                "Content-Digest: " + HelloWorldSha256,
                "Repr-Digest: " + HelloWorldSha256,
            },
            headers);
    }

    [TestMethod]
    public void RequestHeadersFor_NoBodyAndFieldsGivenWithH_AddsOnlyTheFieldsNotGiven()
    {
        var context = Context(new HttpRequestOptions { Headers = ["want-repr-digest: sha-512=1"] });

        var headers = _hook.RequestHeadersFor(context).ToArray();

        CollectionAssert.AreEqual(new[] { "Want-Content-Digest: sha-256=10, sha-512=5" }, headers);
    }

    [TestMethod]
    public async Task ExecuteAsync_MatchingContentDigestAndReprDigest_PassesTheBodyAndResultThrough_Rfc9530Sections2And3()
    {
        var context = Context();

        var result = await _hook.ExecuteAsync(context, Origin(200, HelloWorld, ("Content-Digest", HelloWorldSha256), ("Repr-Digest", HelloWorldSha512)));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(HelloWorld, Encoding.ASCII.GetString(((MemoryStream)context.Output).ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_AContentDigestThatDoesNotMatch_FailsWithExit56AndKeepsTheReport_Rfc9530Section2()
    {
        var result = await _hook.ExecuteAsync(Context(), Origin(200, "other", ("Content-Digest", HelloWorldSha256)));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Content-Digest sha-256 does not match the response body", result.ErrorMessage);
        Assert.AreEqual(5, result.BytesTransferred);
        Assert.AreEqual(200, result.Report!.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_AReprDigestSplitOverTwoFieldLinesThatDoesNotMatch_FailsWithExit56_Rfc9530Section3()
    {
        var result = await _hook.ExecuteAsync(Context(), Origin(200, "other", ("Repr-Digest", "md5=:AAAA:"), ("repr-digest", HelloWorldSha512)));

        Assert.AreEqual("Repr-Digest sha-512 does not match the response body", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_APartialResponse_ChecksContentDigestButNotReprDigest_Rfc9530Section3()
    {
        var result = await _hook.ExecuteAsync(Context(), Origin(206, HelloWorld, ("Content-Digest", HelloWorldSha256), ("Repr-Digest", "sha-256=:AAAA:")));
        var failed = await _hook.ExecuteAsync(Context(), Origin(206, "other", ("Content-Digest", HelloWorldSha256)));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(CurlExitCode.RecvError, failed.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_OnlyUnsupportedOrUnparsableDigests_AreIgnored_Rfc9530Section2()
    {
        var result = await _hook.ExecuteAsync(Context(), Origin(200, "body", ("Content-Digest", "md5=:AAAA:"), ("Repr-Digest", "sha-256")));

        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    public async Task ExecuteAsync_ANotModifiedOrHeadResponse_IsNotChecked()
    {
        var notModified = await _hook.ExecuteAsync(Context(), Origin(304, string.Empty, ("Repr-Digest", HelloWorldSha256)));
        var head = await _hook.ExecuteAsync(Context(noBody: true), Origin(200, string.Empty, ("Repr-Digest", HelloWorldSha256)));

        Assert.IsTrue(notModified.IsSuccess);
        Assert.IsTrue(head.IsSuccess);
    }

    [TestMethod]
    public async Task ExecuteAsync_ABodyCurlDecodedFromAContentCoding_IsNotChecked()
    {
        var compressed = new HttpRequestOptions { Compressed = true };

        var decoded = await _hook.ExecuteAsync(Context(compressed), Origin(200, "body", ("Content-Encoding", "gzip"), ("Content-Digest", HelloWorldSha256)));
        var identity = await _hook.ExecuteAsync(Context(compressed), Origin(200, "body", ("Content-Encoding", " Identity "), ("Content-Digest", HelloWorldSha256)));
        var uncoded = await _hook.ExecuteAsync(Context(compressed), Origin(200, "body", ("Content-Digest", HelloWorldSha256)));
        var raw = await _hook.ExecuteAsync(Context(), Origin(200, "body", ("Content-Encoding", "gzip"), ("Content-Digest", HelloWorldSha256)));

        Assert.IsTrue(decoded.IsSuccess);
        Assert.AreEqual(CurlExitCode.RecvError, identity.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, uncoded.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, raw.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_AFailedOrReportlessTransfer_IsReturnedAsItIs()
    {
        var failure = TransferResult.Failure(CurlExitCode.RecvError, "refused");
        var success = TransferResult.Success(0);

        Assert.AreSame(failure, await _hook.ExecuteAsync(Context(), _ => ValueTask.FromResult(failure)));
        Assert.AreSame(success, await _hook.ExecuteAsync(Context(), _ => ValueTask.FromResult(success)));
    }

    [TestMethod]
    public async Task ExecuteAsync_ATransferThatIsNotHttp_RunsWithTheContextUnchanged()
    {
        var context = new TransferContext { Url = CurlUrl.Parse("ftp://example.com/r"), Output = new MemoryStream() };
        ITransferContext? sent = null;

        await _hook.ExecuteAsync(context, inner =>
        {
            sent = inner;
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        Assert.AreSame(context, sent);
    }

    private static TransferContext Context(HttpRequestOptions? http = null, bool noBody = false) => new()
    {
        Url = CurlUrl.Parse("http://example.com/r"),
        Output = new MemoryStream(),
        Http = http ?? new HttpRequestOptions(),
        NoBody = noBody,
    };

    /// <summary>Stands in for curl's handler: writes one recorded response body and reports its fields.</summary>
    private static Func<ITransferContext, ValueTask<TransferResult>> Origin(int code, string body, params (string Name, string Value)[] fields) =>
        async context =>
        {
            await context.Output.WriteAsync(Encoding.ASCII.GetBytes(body));
            return TransferResult.Success(body.Length) with
            {
                Report = new TransferReport
                {
                    ResponseCode = code,
                    ResponseHeaders = [.. fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value))],
                },
            };
        };
}
