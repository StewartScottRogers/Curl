using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins what follows the <c>curl: (NN)</c> line after a TLS failure against curl 8.21.0
/// (mingw, Schannel), measured on Windows on 2026-09-26 against
/// <c>https://self-signed.badssl.com/</c>: exit 60 adds the five-line sslcerts help block
/// (ADR-0009), exits 35 and 77 add nothing, and <c>-s</c> without <c>-S</c> prints neither
/// the error line nor the block. No test opens a socket.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerTlsFailureTests
{
    private const string VerificationMessage =
        "schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - The certificate chain was issued by an authority that is not trusted.";

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem fileSystem = new();

    private static string ExpectedHelpBlock =>
        "More details here: https://curl.se/docs/sslcerts.html" + Environment.NewLine
        + Environment.NewLine
        + "curl failed to verify the legitimacy of the server and therefore could not" + Environment.NewLine
        + "establish a secure connection to it. To learn more about this situation and" + Environment.NewLine
        + "how to fix it, please visit the webpage mentioned above." + Environment.NewLine;

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_PeerFailedVerification_PrintsTheErrorLineThenTheSslCertsHelpBlock()
    {
        int exitCode = await RunAsync(["https://self-signed.example/"], CurlExitCode.PeerFailedVerification, VerificationMessage);

        Assert.AreEqual(60, exitCode);
        Assert.AreEqual("curl: (60) " + VerificationMessage + Environment.NewLine + ExpectedHelpBlock, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_SslConnectError_PrintsOnlyTheErrorLine()
    {
        int exitCode = await RunAsync(["https://tls.example/"], CurlExitCode.SslConnectError, "schannel: failed to receive handshake");

        Assert.AreEqual(35, exitCode);
        Assert.AreEqual("curl: (35) schannel: failed to receive handshake" + Environment.NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_SslCacertBadfile_PrintsOnlyTheErrorLine()
    {
        int exitCode = await RunAsync(["https://tls.example/"], CurlExitCode.SslCacertBadfile, "schannel: failed to open CA file");

        Assert.AreEqual(77, exitCode);
        Assert.AreEqual("curl: (77) schannel: failed to open CA file" + Environment.NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_PeerFailedVerificationUnderSilent_PrintsNeitherTheErrorLineNorTheHelpBlock()
    {
        int exitCode = await RunAsync(["-s", "https://self-signed.example/"], CurlExitCode.PeerFailedVerification, VerificationMessage);

        Assert.AreEqual(60, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_PeerFailedVerificationUnderSilentShowError_PrintsTheErrorLineThenTheSslCertsHelpBlock()
    {
        int exitCode = await RunAsync(["-sS", "https://self-signed.example/"], CurlExitCode.PeerFailedVerification, VerificationMessage);

        Assert.AreEqual(60, exitCode);
        Assert.AreEqual("curl: (60) " + VerificationMessage + Environment.NewLine + ExpectedHelpBlock, StandardErrorText);
    }

    private Task<int> RunAsync(IReadOnlyList<string> arguments, CurlExitCode failure, string message) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(
                    new ProtocolDispatcher([RecordingProtocolHandler.Failing("https", failure, message)]),
                    []),
                fileSystem,
                fileSystem,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false)
            .RunAsync(arguments);
}
