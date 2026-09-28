using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins the proxy the runner puts on each transfer's <see cref="ITransferContext.Proxy" />
/// (ADR-0056 rules 1 and 5): the selected proxy for every networked scheme, and none for
/// <c>file</c>, which curl 8.21.0 transfers without reading the proxy at all (measured
/// 2026-09-27: <c>curl -x foo://h:1 file:///C:/Windows/win.ini</c> exits 0).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerProxyContextTests
{
    [TestMethod]
    public async Task RunAsync_HttpProxyForADictUrl_ContextCarriesTheProxy()
    {
        RecordingProtocolHandler dict = RecordingProtocolHandler.WritingPath("dict");

        int exitCode = await RunAsync(["-sS", "-x", "http://127.0.0.1:1", "dict://example.com/d:x"], dict);

        Assert.AreEqual(0, exitCode);
        ProxyEndpoint? proxy = dict.Contexts.Single().Proxy;
        Assert.IsNotNull(proxy);
        Assert.AreEqual("127.0.0.1", proxy.Host);
        Assert.AreEqual(1, proxy.Port);
        Assert.AreEqual(ProxyKind.Http, proxy.Kind);
    }

    [TestMethod]
    public async Task RunAsync_ProxyUserOptionForADictUrl_ContextProxyCarriesTheCredential()
    {
        RecordingProtocolHandler dict = RecordingProtocolHandler.WritingPath("dict");

        await RunAsync(["-sS", "-x", "http://127.0.0.1:1", "-U", "u:p", "dict://example.com/d:x"], dict);

        ITransferContext context = dict.Contexts.Single();
        Assert.AreEqual("u", context.Proxy?.Credential?.UserName);
        Assert.AreEqual(context.Http?.ForwardProxy, context.Proxy);
    }

    [TestMethod]
    [DataRow("127.0.0.1:1")]
    [DataRow("foo://127.0.0.1:1")]
    public async Task RunAsync_ProxyOptionForAFileUrl_ContextCarriesNoProxy(string proxyText)
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        int exitCode = await RunAsync(["-sS", "-x", proxyText, "file:///source.txt"], file);

        Assert.AreEqual(0, exitCode);
        ITransferContext context = file.Contexts.Single();
        Assert.IsNull(context.Proxy);
        Assert.IsNull(context.Http?.ForwardProxy);
    }

    private static Task<int> RunAsync(IReadOnlyList<string> arguments, IProtocolHandler handler)
    {
        InMemoryFileSystem outputFiles = new();

        return new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([handler])),
                outputFiles,
                outputFiles,
                new MemoryStream(),
                new MemoryStream(),
                new MemoryStream(),
                runsOnWindows: false)
            .RunAsync(arguments);
    }
}
