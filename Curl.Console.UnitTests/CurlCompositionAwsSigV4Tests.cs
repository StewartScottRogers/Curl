using System.Text;

using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <c>--aws-sigv4</c> end to end through the production composition over a
/// <see cref="ScriptedConnector" />, signing with a clock stopped at the instant each case was
/// measured. Every expected request was measured with curl 8.21.0 (mingw, Schannel) through
/// <c>Record-CurlExchange.ps1</c> with <c>-u AKID:SECRET</c> (BL-628 and BL-629 Notes): the
/// <c>Authorization</c>, <c>X-Amz-Date</c> and <c>x-amz-content-sha256</c> lines go right
/// after <c>Host</c>, before <c>User-Agent</c>.
/// </summary>
[TestClass]
public sealed class CurlCompositionAwsSigV4Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello";

    private const string EmptyPayloadHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    private const string Tail = "User-Agent: curl/8.21.0\r\nAccept: */*\r\n";

    private static readonly DateTimeOffset MeasuredForBl628 = new(2026, 9, 29, 6, 1, 11, TimeSpan.Zero);

    private static readonly DateTimeOffset MeasuredForBl629 = new(2026, 9, 29, 16, 53, 40, TimeSpan.Zero);

    [TestMethod]
    public async Task RunAsync_AwsSigV4GetWithQuery_SignsAsCurlDoes()
    {
        ScriptedConnector server = new([Latin1(Ok)]);

        (int exitCode, string standardError) = await RunAsync(server, MeasuredForBl628, "--aws-sigv4", "aws:amz:us-east-1:s3", "http://127.0.0.1:18628/bucket/key%20a?b=2&a=1&c");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode, standardError);
        Assert.AreEqual(
            "GET /bucket/key%20a?b=2&a=1&c HTTP/1.1\r\n"
            + "Host: 127.0.0.1:18628\r\n"
            + "Authorization: AWS4-HMAC-SHA256 Credential=AKID/20260929/us-east-1/s3/aws4_request, SignedHeaders=host;x-amz-content-sha256;x-amz-date, Signature=6f96d5ca68a6971972090dfc3d89b3f9674419c900e06d56ef1c7b28373389d7\r\n"
            + "X-Amz-Date: 20260929T060111Z\r\n"
            + $"x-amz-content-sha256: {EmptyPayloadHash}\r\n"
            + Tail
            + "\r\n",
            Latin1(server.Written));
    }

    [TestMethod]
    public async Task RunAsync_AwsSigV4PostData_SignsTheBodyAsCurlDoes()
    {
        ScriptedConnector server = new([Latin1(Ok)]);

        (int exitCode, string standardError) = await RunAsync(server, MeasuredForBl628, "--aws-sigv4", "aws:amz:us-east-1:s3", "-d", "hello=world", "http://127.0.0.1:18628/upload");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode, standardError);
        Assert.AreEqual(
            "POST /upload HTTP/1.1\r\n"
            + "Host: 127.0.0.1:18628\r\n"
            + "Authorization: AWS4-HMAC-SHA256 Credential=AKID/20260929/us-east-1/s3/aws4_request, SignedHeaders=host;x-amz-content-sha256;x-amz-date, Signature=445ec533470e073a3ca813c59a7f730706cf61288fda6024ac5ff39fd37d85e7\r\n"
            + "X-Amz-Date: 20260929T060111Z\r\n"
            + "x-amz-content-sha256: 3d011e09502a84552a0f8ae112d024cc2c115597e3a577d5f49007902c221dc5\r\n"
            + Tail
            + "Content-Length: 11\r\n"
            + "Content-Type: application/x-www-form-urlencoded\r\n"
            + "\r\n"
            + "hello=world",
            Latin1(server.Written));
    }

    [TestMethod]
    public async Task RunAsync_AwsSigV4ProviderOnly_TakesServiceAndRegionFromTheHostAsCurlDoes()
    {
        ScriptedConnector server = new([Latin1(Ok)]);

        (int exitCode, string standardError) = await RunAsync(server, MeasuredForBl628.AddSeconds(1), "--aws-sigv4", "osc", "--connect-to", "::127.0.0.1:18628", "http://fcu.eu-west-2.outscale.com:18628/path");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode, standardError);
        Assert.AreEqual(
            "GET /path HTTP/1.1\r\n"
            + "Host: fcu.eu-west-2.outscale.com:18628\r\n"
            + "Authorization: OSC4-HMAC-SHA256 Credential=AKID/20260929/eu-west-2/fcu/osc4_request, SignedHeaders=host;x-osc-date, Signature=235bcff21c7d26e2cff783594087d84a221296654fe89689144b13fc6690bee5\r\n"
            + "X-Osc-Date: 20260929T060112Z\r\n"
            + Tail
            + "\r\n",
            Latin1(server.Written));
    }

    [TestMethod]
    public async Task RunAsync_AwsSigV4WithCustomHeaders_SignsThemAndSendsThemAfterCurlsOwn()
    {
        ScriptedConnector server = new([Latin1(Ok)]);

        (int exitCode, string standardError) = await RunAsync(server, MeasuredForBl629, "--aws-sigv4", "aws:amz:us-east-1:s3", "-H", "X-Custom:  a   b ", "-H", "Content-Type: text/plain", "http://127.0.0.1:18629/bucket/key");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode, standardError);
        Assert.AreEqual(
            "GET /bucket/key HTTP/1.1\r\n"
            + "Host: 127.0.0.1:18629\r\n"
            + "Authorization: AWS4-HMAC-SHA256 Credential=AKID/20260929/us-east-1/s3/aws4_request, SignedHeaders=content-type;host;x-amz-content-sha256;x-amz-date;x-custom, Signature=a6ee51dc33bbbbe7a73f1d4e932381e7aada94fbbe3ff25a9a24134876814dec\r\n"
            + "X-Amz-Date: 20260929T165340Z\r\n"
            + $"x-amz-content-sha256: {EmptyPayloadHash}\r\n"
            + Tail
            + "X-Custom:  a   b \r\n"
            + "Content-Type: text/plain\r\n"
            + "\r\n",
            Latin1(server.Written));
    }

    [TestMethod]
    public async Task RunAsync_AwsSigV4FollowingARedirect_SignsEachHopForItsOwnPathAndQuery()
    {
        ScriptedConnector server = new(
        [
            Latin1("HTTP/1.1 302 Found\r\nLocation: /second?x=1\r\nContent-Length: 0\r\n\r\n"),
            Latin1(Ok),
        ]);

        (int exitCode, string standardError) = await RunAsync(server, MeasuredForBl629, "-L", "--aws-sigv4", "aws:amz:us-east-1:s3", "http://127.0.0.1:18630/first");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode, standardError);
        Assert.AreEqual(
            "GET /first HTTP/1.1\r\n"
            + "Host: 127.0.0.1:18630\r\n"
            + "Authorization: AWS4-HMAC-SHA256 Credential=AKID/20260929/us-east-1/s3/aws4_request, SignedHeaders=host;x-amz-content-sha256;x-amz-date, Signature=d64892f15fbd9b7fe585d3df0ed3a26e84e620588f73c9268d6acce9a104f8a9\r\n"
            + "X-Amz-Date: 20260929T165340Z\r\n"
            + $"x-amz-content-sha256: {EmptyPayloadHash}\r\n"
            + Tail
            + "\r\n"
            + "GET /second?x=1 HTTP/1.1\r\n"
            + "Host: 127.0.0.1:18630\r\n"
            + "Authorization: AWS4-HMAC-SHA256 Credential=AKID/20260929/us-east-1/s3/aws4_request, SignedHeaders=host;x-amz-content-sha256;x-amz-date, Signature=0921049d176f0b5d18a2ea8fc9dcb971867d5cc9fe7be0eaf89fb0f86a325ccb\r\n"
            + "X-Amz-Date: 20260929T165340Z\r\n"
            + $"x-amz-content-sha256: {EmptyPayloadHash}\r\n"
            + Tail
            + "\r\n",
            Latin1(server.Written));
    }

    /// <summary>
    /// Measured: <c>-sv</c> prints the string to sign, the signature and the scheme after
    /// <c>using HTTP/1.x</c>, just before the request.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_AwsSigV4Verbose_PrintsTheSigningLinesJustBeforeTheRequest()
    {
        ScriptedConnector server = new([Latin1(Ok)]);

        (int exitCode, string standardError) = await RunAsync(server, new DateTimeOffset(2026, 9, 29, 20, 42, 0, TimeSpan.Zero), "-v", "--aws-sigv4", "aws:amz:us-east-1:s3", "http://127.0.0.1:18633/x");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode, standardError);
        StringAssert.Contains(
            standardError.Replace("\r\n", "\n", StringComparison.Ordinal),
            "* using HTTP/1.x\n"
            + "* aws_sigv4: String to sign (enclosed in []) - [AWS4-HMAC-SHA256\n20260929T204200Z\n20260929/us-east-1/s3/aws4_request\ne8d1e6c370aff2389eacec848783f05e19624004eee6d4357afa7d27b3ce09d9]\n"
            + "* aws_sigv4: Signature - 65b76dcb57fed633eed53e8515f9b2e6a53bbe4d8d5c792def45eebd25da8846\n"
            + "* Server auth using AWS_SIGV4 with user 'AKID'\n"
            + "> GET /x HTTP/1.1",
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Measured 2026-10-02 (BL-1244): <c>-v --aws-sigv4 aws:amz</c> against
    /// <c>s3.eu-west-1.localhost</c> prints the service and region it picked from the host
    /// after <c>using HTTP/1.x</c> and before the string to sign.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_AwsSigV4VerboseRegionAndServiceFromHost_PrintsThePickedLinesBeforeTheStringToSign()
    {
        ScriptedConnector server = new([Latin1(Ok)]);

        (int exitCode, string standardError) = await RunAsync(server, MeasuredForBl629, "-v", "--aws-sigv4", "aws:amz", "http://s3.eu-west-1.localhost:18644/");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode, standardError);
        StringAssert.Contains(
            standardError.Replace("\r\n", "\n", StringComparison.Ordinal),
            "* using HTTP/1.x\n"
            + "* aws_sigv4: picked service s3 from host\n"
            + "* aws_sigv4: picked region eu-west-1 from host\n"
            + "* aws_sigv4: String to sign (enclosed in []) - [AWS4-HMAC-SHA256\n",
            StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task RunAsync_AwsSigV4VerboseRegionAndServiceInTheParameter_PrintsNoPickedLine()
    {
        ScriptedConnector server = new([Latin1(Ok)]);

        (int exitCode, string standardError) = await RunAsync(server, MeasuredForBl629, "-v", "--aws-sigv4", "aws:amz:us-east-1:s3", "http://s3.eu-west-1.localhost:18644/");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode, standardError);
        Assert.DoesNotContain("picked", standardError, StringComparison.Ordinal);
        StringAssert.Contains(standardError, "* aws_sigv4: String to sign", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task RunAsync_AwsSigV4RegionAndServiceFromHostWithoutVerbose_PrintsNoPickedLine()
    {
        ScriptedConnector server = new([Latin1(Ok)]);

        (int exitCode, string standardError) = await RunAsync(server, MeasuredForBl629, "--aws-sigv4", "aws:amz", "http://s3.eu-west-1.localhost:18644/");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode, standardError);
        Assert.AreEqual(string.Empty, standardError);
    }

    /// <summary>
    /// <c>--aws-sigv4 aws</c> against <c>localhost</c> names no service: curl 8.21.0 connects,
    /// sends nothing and fails with exit 3 (measured, BL-629 Notes).
    /// </summary>
    [TestMethod]
    public async Task RunAsync_AwsSigV4WithoutAService_FailsWithExit3AndSendsNothing()
    {
        ScriptedConnector server = new([Latin1(Ok)]);

        (int exitCode, string standardError) = await RunAsync(server, MeasuredForBl629, "--aws-sigv4", "aws", "http://localhost:18631/x");

        Diagnostics.Assert("exit code", (int)CurlExitCode.UrlMalformat, exitCode);
        Assert.AreEqual((int)CurlExitCode.UrlMalformat, exitCode);
        Assert.AreEqual("curl: (3) aws-sigv4: service missing in parameters and hostname\n", standardError.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.AreEqual(0, server.Written.Length);
    }

    /// <summary>
    /// Runs <c>-sS -u AKID:SECRET</c> and <paramref name="arguments" /> through the production
    /// composition over <paramref name="server" />, signing at <paramref name="now" />.
    /// </summary>
    private async Task<(int ExitCode, string StandardError)> RunAsync(ScriptedConnector server, DateTimeOffset now, params string[] arguments)
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        using MemoryStream standardInput = new();
        Diagnostics.Arrange("arguments", "-sS -u AKID:SECRET " + string.Join(' ', arguments));
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
                .RunAsync(["-sS", "-u", "AKID:SECRET", .. arguments]);
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
