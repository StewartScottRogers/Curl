using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Xmpp;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Xmpp;

/// <summary>
/// Runs curl's <c>xmpp://</c> and <c>xmpps://</c> client in process against an <see cref="XmppReferenceServer" />
/// written from RFC 6120 (BL-4495), on the default ports 5222 and 5223, and reads what each side sent.
/// </summary>
internal static class XmppRun
{
    /// <summary>A URL that logs in to the reference server's domain and addresses <c>bob@localhost</c>.</summary>
    public const string Url = "xmpp://localhost/bob@localhost";

    /// <summary>The stream header curl opens every stream with (RFC 6120, 4.7).</summary>
    public const string StreamHeader =
        "<?xml version='1.0'?><stream:stream to='localhost' version='1.0' xml:lang='en' xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams'>";

    private static readonly InProcessCurlRunner Curl = InProcessCurl.RunAsync;

    private static readonly Lazy<X509Certificate2> Certificate = new(() => ReferenceCertificate.CreateSelfSigned("localhost"));

    /// <summary>Gives a script that offers STARTTLS as <paramref name="startTls" /> says, with the reference certificate.</summary>
    /// <param name="startTls">How STARTTLS is offered.</param>
    /// <returns>The script.</returns>
    public static XmppReferenceScript WithStartTls(XmppStartTls startTls) => new() { StartTls = startTls, Certificate = Certificate.Value };

    /// <summary>Runs curl with <paramref name="arguments" /> against <paramref name="server" /> over plain TCP on port 5222.</summary>
    /// <param name="server">The reference server.</param>
    /// <param name="arguments">curl's arguments after <c>-sS -k -m 10</c>.</param>
    /// <returns>The run's result.</returns>
    public static Task<CurlRunResult> RunAsync(XmppReferenceServer server, params string[] arguments) =>
        ExtendedConformanceHarness.RunAsync(Curl, [ReferenceRoute.Tcp(5222, server)], ["-sS", "-k", "-m", "10", .. arguments]);

    /// <summary>Runs curl with <paramref name="arguments" /> against <paramref name="server" /> over TLS from the first byte on port 5223.</summary>
    /// <param name="server">The reference server.</param>
    /// <param name="arguments">curl's arguments after <c>-sS -k -m 10</c>.</param>
    /// <returns>The run's result.</returns>
    public static Task<CurlRunResult> RunTlsAsync(XmppReferenceServer server, params string[] arguments) =>
        ExtendedConformanceHarness.RunAsync(Curl, [ReferenceRoute.Tls(5223, server, Certificate.Value)], ["-sS", "-k", "-m", "10", .. arguments]);

    /// <summary>Gets everything curl sent on its only connection, as UTF-8 text (ciphertext after STARTTLS).</summary>
    /// <param name="result">The run's result.</param>
    /// <returns>The text.</returns>
    public static string ClientText(CurlRunResult result) =>
        string.Concat(result.Exchanges.Select(exchange => Encoding.UTF8.GetString(exchange.ClientBytes)));

    /// <summary>Asserts the exit code, standard error (newlines as LF) and standard output.</summary>
    /// <param name="result">The run's result.</param>
    /// <param name="exitCode">The expected exit code.</param>
    /// <param name="standardError">The expected standard error.</param>
    /// <param name="standardOutput">The expected standard output.</param>
    public static void AssertExit(CurlRunResult result, int exitCode, string standardError, string standardOutput = "")
    {
        Assert.AreEqual(standardError, Encoding.UTF8.GetString(result.StandardError).Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(standardOutput, Encoding.UTF8.GetString(result.StandardOutput));
    }
}
