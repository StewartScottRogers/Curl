using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Nats;

/// <summary>
/// Runs curl's <c>nats://</c> and <c>natss://</c> client in process against a <see cref="ScriptedReferenceServer" />
/// written from the NATS client protocol (BL-4487), on the default port 4222, and reads what each side sent.
/// </summary>
internal static class NatsRun
{
    /// <summary>The NATS client port, which curl uses when the URL names none.</summary>
    public const int Port = 4222;

    /// <summary>The CONNECT line curl sends over plain TCP with no credentials.</summary>
    public const string PlainConnect =
        "CONNECT {\"verbose\":false,\"pedantic\":false,\"tls_required\":false,\"name\":\"curl\",\"lang\":\"curl\",\"version\":\"1.0.0\",\"protocol\":1}\r\n";

    private static readonly InProcessCurlRunner Curl = InProcessCurl.RunAsync;

    private static readonly Lazy<X509Certificate2> Certificate = new(() => ReferenceCertificate.CreateSelfSigned("localhost"));

    /// <summary>Runs curl with <paramref name="arguments" /> against <paramref name="steps" /> over plain TCP.</summary>
    /// <param name="steps">The server's script.</param>
    /// <param name="arguments">curl's arguments after <c>-sS -m 10</c>.</param>
    /// <returns>The run's result.</returns>
    public static Task<CurlRunResult> RunAsync(ScriptStep[] steps, params string[] arguments) =>
        ExtendedConformanceHarness.RunAsync(Curl, [ReferenceRoute.Tcp(Port, new ScriptedReferenceServer(steps))], ["-sS", "-m", "10", .. arguments]);

    /// <summary>Runs curl with <paramref name="arguments" /> against <paramref name="steps" /> over TLS from the first byte.</summary>
    /// <param name="steps">The server's script.</param>
    /// <param name="arguments">curl's arguments after <c>-sS -k -m 10</c>.</param>
    /// <returns>The run's result.</returns>
    public static Task<CurlRunResult> RunTlsAsync(ScriptStep[] steps, params string[] arguments) =>
        ExtendedConformanceHarness.RunAsync(Curl, [ReferenceRoute.Tls(Port, new ScriptedReferenceServer(steps), Certificate.Value)], ["-sS", "-k", "-m", "10", .. arguments]);

    /// <summary>Gets everything curl sent on its only connection, as Latin-1 text; empty when it opened none.</summary>
    /// <param name="result">The run's result.</param>
    /// <returns>The text.</returns>
    public static string ClientText(CurlRunResult result) =>
        string.Concat(result.Exchanges.Select(exchange => Encoding.Latin1.GetString(exchange.ClientBytes)));

    /// <summary>Asserts the exit code, standard error (newlines as LF) and standard output.</summary>
    /// <param name="result">The run's result.</param>
    /// <param name="exitCode">The expected exit code.</param>
    /// <param name="standardError">The expected standard error.</param>
    /// <param name="standardOutput">The expected standard output.</param>
    public static void AssertExit(CurlRunResult result, int exitCode, string standardError, string standardOutput = "")
    {
        // curl ends its message line with the platform's newline, CRLF on Windows.
        Assert.AreEqual(standardError, Encoding.Latin1.GetString(result.StandardError).Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(standardOutput, Encoding.Latin1.GetString(result.StandardOutput));
    }
}
