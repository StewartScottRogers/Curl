using System.Text;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.Checksum.UnitTests;

[TestClass]
public sealed class ChecksumRedirectTests
{
    private const string Sha256OfHelloLine = "sha256:5891b5b522d5df086d0ff0b110fbd9d21bb4fc7163af34d08286a2e846f6be03";
    private const string Sha256OfNothing = "sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    [TestMethod]
    public async Task ExecuteAsync_FollowingRedirects_ChecksOnlyTheFinalBody()
    {
        var output = new MemoryStream();

        var results = await RunChainAsync(Sha256OfHelloLine, new HttpRequestOptions { FollowRedirects = true }, output);

        Assert.IsTrue(results.All(result => result.IsSuccess));
        Assert.AreEqual("hello\n", Encoding.ASCII.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_FollowingRedirectsWithTheFirstBodysDigest_FailsNamingTheFinalDigest()
    {
        var results = await RunChainAsync(Sha256OfNothing, new HttpRequestOptions { FollowRedirects = true }, new MemoryStream());

        Assert.IsTrue(results[0].IsSuccess);
        Assert.AreEqual(CurlExitCode.BadContentEncoding, results[1].ExitCode);
        Assert.AreEqual($"checksum mismatch: expected {Sha256OfNothing}, got {Sha256OfHelloLine}", results[1].ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_RedirectNotFollowed_ChecksTheRedirectsBody()
    {
        var results = await RunChainAsync(Sha256OfHelloLine, new HttpRequestOptions(), new MemoryStream());

        Assert.AreEqual(CurlExitCode.BadContentEncoding, results[0].ExitCode);
        Assert.AreEqual($"checksum mismatch: expected {Sha256OfHelloLine}, got {Sha256OfNothing}", results[0].ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_RedirectLimitReached_ChecksTheRedirectsBody()
    {
        var http = new HttpRequestOptions { FollowRedirects = true, MaxRedirects = 1, RedirectsFollowed = 1 };

        var results = await RunChainAsync(Sha256OfHelloLine, http, new MemoryStream());

        Assert.AreEqual(CurlExitCode.BadContentEncoding, results[0].ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoRedirectLimit_StillSkipsTheRedirect()
    {
        var http = new HttpRequestOptions { FollowRedirects = true, MaxRedirects = -1, RedirectsFollowed = 100 };

        var results = await RunChainAsync(Sha256OfHelloLine, http, new MemoryStream());

        Assert.IsTrue(results[0].IsSuccess);
    }

    [TestMethod]
    public async Task ExecuteAsync_FollowingRedirectsButTheResponseHasNoReport_ChecksTheBody()
    {
        var hook = new ChecksumTransferHook(ExpectedChecksum.TryParse(Sha256OfHelloLine)!);
        var context = new TransferContext { Url = CurlUrl.Parse("http://example.com/"), Output = Stream.Null, Http = new HttpRequestOptions { FollowRedirects = true } };

        var result = await hook.ExecuteAsync(context, _ => ValueTask.FromResult(new TransferResult(CurlExitCode.Ok, 0)));

        Assert.AreEqual(CurlExitCode.BadContentEncoding, result.ExitCode);
    }

    // Two hops as the redirect follower runs them: the hook sees the 302 with an empty body, then the 200 with "hello\n".
    private static async Task<TransferResult[]> RunChainAsync(string expected, HttpRequestOptions http, Stream output)
    {
        var hook = new ChecksumTransferHook(ExpectedChecksum.TryParse(expected)!);
        var context = new TransferContext { Url = CurlUrl.Parse("http://example.com/redir"), Output = output, Http = http };
        var redirect = new TransferResult(CurlExitCode.Ok, 0) { Report = new TransferReport { ResponseCode = 302, RedirectUrl = "http://example.com/plain" } };
        var final = new TransferResult(CurlExitCode.Ok, 6) { Report = new TransferReport { ResponseCode = 200 } };

        var first = await hook.ExecuteAsync(context, _ => ValueTask.FromResult(redirect));
        if (!first.IsSuccess || !http.FollowRedirects || http.RedirectsFollowed >= http.MaxRedirects && http.MaxRedirects >= 0)
        {
            return [first];
        }

        var second = await hook.ExecuteAsync(context, async inner =>
        {
            await inner.Output.WriteAsync("hello\n"u8.ToArray());
            return final;
        });
        return [first, second];
    }
}
