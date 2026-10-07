using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

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
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_HttpProxyForADictUrl_ContextCarriesTheProxy()
    {
        RecordingProtocolHandler dict = RecordingProtocolHandler.WritingPath("dict");

        int exitCode = await RunAsync(["-sS", "-x", "http://127.0.0.1:1", "dict://example.com/d:x"], dict);

        ProxyEndpoint? proxy = dict.Contexts.Single().Proxy;
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("proxy is present", true, proxy is not null);
        Diagnostics.Assert("proxy host", "127.0.0.1", proxy?.Host);
        Diagnostics.Assert("proxy port", 1, proxy?.Port);
        Diagnostics.Assert("proxy kind", ProxyKind.Http, proxy?.Kind);
        Assert.AreEqual(0, exitCode);
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
        Diagnostics.Assert("proxy credential user name", "u", context.Proxy?.Credential?.UserName);
        Diagnostics.Assert("http forward proxy is the proxy", context.Proxy, context.Http?.ForwardProxy);
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

        ITransferContext context = file.Contexts.Single();
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("proxy", null, context.Proxy);
        Diagnostics.Assert("http forward proxy", null, context.Http?.ForwardProxy);
        Assert.AreEqual(0, exitCode);
        Assert.IsNull(context.Proxy);
        Assert.IsNull(context.Http?.ForwardProxy);
    }

    private async Task<int> RunAsync(IReadOnlyList<string> arguments, IProtocolHandler handler)
    {
        InMemoryFileSystem outputFiles = new();
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher([handler])),
                    outputFiles,
                    outputFiles,
                    new MemoryStream(),
                    new MemoryStream(),
                    new MemoryStream(),
                    runsOnWindows: false)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        return exitCode;
    }
}
