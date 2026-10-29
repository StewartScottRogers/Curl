using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Network;

namespace Curl.Engine;

/// <summary>
/// CGR-152 (BL-3681): curl 8.22.0 calls <c>CURLOPT_RESOLVER_START_FUNCTION</c> before it resolves an IP address
/// literal too, so a callback answering non-zero aborts the transfer with <c>CURLE_ABORTED_BY_CALLBACK</c>.
/// </summary>
[TestClass]
public sealed class Cgr152Tests
{
    [TestMethod]
    [DataRow("http://127.0.0.1:21700/", "127.0.0.1")]
    [DataRow("http://[::1]:21799/", "::1")]
    public async Task PerformAsync_IpLiteralHostAndResolverStartAborts_CallsItOnceAndReturnsAbortedByCallback(string url, string host)
    {
        int started = 0;
        await using ManagedCurlEngine engine = new();
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = url },
            Callbacks = new() { ResolverStart = _ => ++started },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.AbortedByCallback, code);
        Assert.AreEqual($"Could not resolve host: {host}", easy.ErrorText);
        Assert.AreEqual(1, started);
    }

    [TestMethod]
    public async Task PerformAsync_TwoHandlesOfOneEngine_CallTheResolverStartCallbackForEachLiteral()
    {
        int started = 0;
        await using ManagedCurlEngine engine = new();
        foreach (string url in new[] { "http://127.0.0.1:21700/", "http://127.0.0.1:21700/" })
        {
            await using IEasyTransfer easy = engine.CreateEasy();
            easy.Options = new EasyOptions
            {
                Network = new EasyNetworkOptions { Url = url },
                Callbacks = new() { ResolverStart = _ => ++started },
            };

            Assert.AreEqual(CurlCode.AbortedByCallback, await easy.PerformAsync(CancellationToken.None));
        }

        Assert.AreEqual(2, started);
    }

    [TestMethod]
    public async Task PerformAsync_SecondHandleToANameTheFirstHandleResolvedThroughResolve_CallsTheResolverStartCallbackAgain()
    {
        await using ManagedCurlEngine engine = new();
        await using IEasyTransfer first = engine.CreateEasy();
        first.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://127.0.0.1:21700/", Resolve = ["fake.example:21700:127.0.0.1"] },
            Callbacks = new() { ResolverStart = _ => 1 },
        };
        Assert.AreEqual(CurlCode.AbortedByCallback, await first.PerformAsync(CancellationToken.None));

        int started = 0;
        await using IEasyTransfer second = engine.CreateEasy();
        second.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://fake.example:21700/" },
            Callbacks = new() { ResolverStart = _ => ++started },
        };

        Assert.AreEqual(CurlCode.AbortedByCallback, await second.PerformAsync(CancellationToken.None));
        Assert.AreEqual(1, started);
    }
}
