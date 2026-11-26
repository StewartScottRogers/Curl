using Curl.Authentication;
using Curl.Core;
using Curl.Engine;
using Curl.Networking;
using Curl.Original.Protocol.Http;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that <c>curl -k -I https://...</c> against a server that accepted <c>h2</c> by ALPN and also
/// advertises <c>h3</c> in <c>alt-svc</c> prints an <c>HTTP/2 200 </c> head with the lower-case
/// <c>alt-svc</c> field, as curl 8.21.0 does (BL-5290; the ALPN offer is BL-5288).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerHttpsH2WithAltSvcH3Tests
{
    private readonly InMemoryFileSystem fileSystem = new();

    private readonly TextWaitingStream standardOutput = new();

    private readonly TextWaitingStream standardError = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_HeadOverHttpsWhereServerAcceptedH2AndAdvertisesH3_PrintsAnHttp2OkHeadWithLowerCaseAltSvc()
    {
        Http2ServerConnector server = new() { ApplicationProtocol = "h2", ResponseHeaders = [("alt-svc", "h3=\":8443\"; ma=86400")] };
        Diagnostics.Arrange("command line", "-sS -k -I https://h/hello.txt");
        CurlCommandRunner runner = new(
            options =>
            {
                PoolingConnector pool = new(server, TimeProvider.System);
                HttpProtocolHandler http = new(pool, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));
                return new TransferDispatch(new ProtocolDispatcher([http]), [], connectionPool: pool);
            },
            fileSystem,
            fileSystem,
            standardOutput,
            standardError,
            new MemoryStream(),
            runsOnWindows: false,
            parsesAsWindowsBuild: false,
            writesProgressMeter: false);

        int exitCode = await runner.RunAsync(["-sS", "-k", "-I", "https://h/hello.txt"]).WaitAsync(TimeSpan.FromSeconds(60));

        string stdout = standardOutput.Text.Replace("\r\n", "\n", StringComparison.Ordinal);
        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stdout", stdout);
        Diagnostics.Assert("stdout", "HTTP/2 200 \nalt-svc: h3=\":8443\"; ma=86400\n\n", stdout);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("HTTP/2 200 \nalt-svc: h3=\":8443\"; ma=86400\n\n", stdout);
    }
}
