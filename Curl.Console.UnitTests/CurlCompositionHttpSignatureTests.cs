using System.Text;

using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the <c>--httpsig-*</c> options end to end through the production composition over a
/// <see cref="ScriptedConnector" />, signing with a clock stopped at the Unix epoch, so
/// <c>created=0</c> as upstream's debug build fixes it (BL-2182): the <c>Signature-Input</c> and
/// <c>Signature</c> lines go right after <c>Host</c> (upstream test 5001), the setup checks refuse
/// as <c>config2setopts.c</c> does (tests 5008 to 5010), and a redirect to another origin is sent
/// unsigned (test 5023).
/// </summary>
[TestClass]
public sealed class CurlCompositionHttpSignatureTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello";

    private const string Ed25519Key = "9f8362f87a484a954e6e740c5b4c0e84229139a20aa8ab56ff66586f6a7d29c5";

    private const string Tail = "User-Agent: curl/8.22.0\r\nAccept: */*\r\n";

    [TestMethod]
    public async Task RunAsync_Ed25519GetWithQuery_SignsAsUpstreamTest5001()
    {
        ScriptedConnector server = new([Latin1(Ok)]);

        (int exitCode, string standardError) = await RunAsync(server, "http://example.com:8000/5001/resource?action=read", "--httpsig-algo", "ed25519", "--httpsig-key", Ed25519Key, "--httpsig-keyid", "my-key-1", "--connect-to", "example.com:8000:127.0.0.1:18000");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode, standardError);
        Assert.AreEqual(
            "GET /5001/resource?action=read HTTP/1.1\r\n"
            + "Host: example.com:8000\r\n"
            + "Signature-Input: sig1=(\"@method\" \"@authority\" \"@path\" \"@query\");created=0;keyid=\"my-key-1\";alg=\"ed25519\"\r\n"
            + "Signature: sig1=:RQniOeqmwdRzGvoDIMJ8XJha75evJWgqo5/66EeuJeEGczZtnP2U/F52Lzd/y7Vd1DCb8oUcCKHrKi2VJI7lBA==:\r\n"
            + Tail
            + "\r\n",
            Latin1(server.Written));
    }

    [TestMethod]
    [DataRow(2, "curl: --httpsig-key is required", new[] { "--httpsig-keyid", "k" }, DisplayName = "no key")]
    [DataRow(2, "curl: --httpsig-keyid is required", new[] { "--httpsig-key", "00" }, DisplayName = "no key id")]
    [DataRow(2, "", new[] { "--no-show-error", "--httpsig-keyid", "k" }, DisplayName = "no key, errors hidden")]
    [DataRow(2, "curl: --httpsig-algo: unsupported algorithm 'rsa'", new[] { "--httpsig-algo", "rsa", "--httpsig-key", "00", "--httpsig-keyid", "k" }, DisplayName = "unknown algorithm")]
    [DataRow(26, "curl: httpsig: cannot open key file 'no-such-dir/no-such.key'", new[] { "--httpsig-key", "@no-such-dir/no-such.key", "--httpsig-keyid", "k" }, DisplayName = "missing key file")]
    [DataRow(43, "curl: (43) httpsig: invalid hex at position 0 ('zz')", new[] { "--httpsig-key", "zz", "--httpsig-keyid", "k" }, DisplayName = "bad hex")]
    [DataRow(43, "curl: (43) httpsig: unknown component '@x'", new[] { "--httpsig-key", Ed25519Key, "--httpsig-keyid", "k", "--httpsig-headers", "@x" }, DisplayName = "unknown component")]
    public async Task RunAsync_RefusedOptions_ExitWithCurlsCodeAndLine(int expectedExitCode, string expectedLine, string[] options)
    {
        ScriptedConnector server = new([Latin1(Ok)]);

        (int exitCode, string standardError) = await RunAsync(server, "http://127.0.0.1:18000/x", options);

        Diagnostics.Assert("exit code", expectedExitCode, exitCode);
        Assert.AreEqual(expectedExitCode, exitCode, standardError);
        Assert.AreEqual(expectedLine, standardError.TrimEnd('\r', '\n'));
    }

    [TestMethod]
    public async Task RunAsync_EmptyKeyFile_ExitsWith43()
    {
        string directory = Directory.CreateTempSubdirectory("curl-httpsig-").FullName;
        try
        {
            string keyFile = Path.Combine(directory, "empty.key");
            await File.WriteAllTextAsync(keyFile, string.Empty, TestContext.CancellationToken);
            ScriptedConnector server = new([Latin1(Ok)]);

            (int exitCode, string standardError) = await RunAsync(server, "http://127.0.0.1:18000/x", "--httpsig-key", "@" + keyFile, "--httpsig-keyid", "k");

            Diagnostics.Assert("exit code", 43, exitCode);
            Assert.AreEqual(43, exitCode, standardError);
            Assert.AreEqual($"curl: httpsig: key file '{keyFile}' is empty", standardError.TrimEnd('\r', '\n'));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task RunAsync_KeyFile_SignsWithItsText()
    {
        string directory = Directory.CreateTempSubdirectory("curl-httpsig-").FullName;
        try
        {
            string keyFile = Path.Combine(directory, "hmac.key");
            await File.WriteAllTextAsync(keyFile, "00112233445566778899aabbccddeeff\n", TestContext.CancellationToken);
            ScriptedConnector server = new([Latin1(Ok)]);

            (int exitCode, string standardError) = await RunAsync(server, "http://example.com:7000/5002/resource", "--httpsig-algo", "HMAC-SHA256", "--httpsig-key", "@" + keyFile, "--httpsig-keyid", "shared-key-1", "--connect-to", "example.com:7000:127.0.0.1:18000");

            Diagnostics.Assert("exit code", 0, exitCode);
            Assert.AreEqual(0, exitCode, standardError);
            StringAssert.Contains(Latin1(server.Written), "Signature-Input: sig1=(\"@method\" \"@authority\" \"@path\");created=0;keyid=\"shared-key-1\";alg=\"hmac-sha256\"\r\nSignature: sig1=:", StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task RunAsync_RedirectToAnotherPort_SendsTheHopUnsigned()
    {
        ScriptedConnector server = new(
        [
            Latin1("HTTP/1.1 302 Found\r\nLocation: http://127.0.0.1:18001/next\r\nContent-Length: 0\r\n\r\n"),
            Latin1(Ok),
        ]);

        (int exitCode, string standardError) = await RunAsync(server, "http://127.0.0.1:18000/first", "-L", "--httpsig-key", Ed25519Key, "--httpsig-keyid", "k");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode, standardError);
        string written = Latin1(server.Written);
        string second = written[written.IndexOf("GET /next", StringComparison.Ordinal)..];
        Assert.AreEqual("GET /next HTTP/1.1\r\nHost: 127.0.0.1:18001\r\n" + Tail + "\r\n", second);
        StringAssert.StartsWith(written, "GET /first HTTP/1.1\r\nHost: 127.0.0.1:18000\r\nSignature-Input: sig1=", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task RunAsync_RedirectToUrlWithCredentials_SendsTheHopWithNoAuthorization()
    {
        ScriptedConnector server = new(
        [
            Latin1("HTTP/1.1 301 OK\r\nLocation: http://bob:alice@127.0.0.1:18001/next\r\nContent-Length: 0\r\n\r\n"),
            Latin1(Ok),
        ]);

        (int exitCode, string standardError) = await RunAsync(server, "http://127.0.0.1:18000/first", "-L", "--httpsig-key", Ed25519Key, "--httpsig-keyid", "k");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode, standardError);
        string written = Latin1(server.Written);
        string second = written[written.IndexOf("GET /next", StringComparison.Ordinal)..];
        Assert.AreEqual("GET /next HTTP/1.1\r\nHost: 127.0.0.1:18001\r\n" + Tail + "\r\n", second);
    }

    private async Task<(int ExitCode, string StandardError)> RunAsync(ScriptedConnector server, string url, params string[] options)
    {
        string[] arguments = [.. options, url];
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        using MemoryStream standardInput = new();
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("signing clock", now.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await CurlComposition
                .CreateRunner(
                    standardOutput,
                    standardError,
                    standardInput,
                    server,
                    new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"),
                    signingClock: new FixedUtcClock(now))
                .RunAsync(["-sS", .. arguments]);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("request", server.Written);
        Diagnostics.Bytes("stdout", standardOutput.ToArray());
        Diagnostics.Bytes("stderr", standardError.ToArray());
        return (exitCode, Encoding.Latin1.GetString(standardError.ToArray()));
    }

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    /// <summary>A clock stopped at one instant.</summary>
    private sealed class FixedUtcClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
