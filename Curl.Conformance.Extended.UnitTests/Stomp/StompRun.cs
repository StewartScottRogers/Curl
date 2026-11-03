using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Stomp;

/// <summary>
/// Runs curl's <c>stomp://</c> and <c>stomps://</c> client in process against a <see cref="ScriptedReferenceServer" />
/// whose frames are written from STOMP 1.2 (BL-4493), on the ports 61613 and 61614, and reads what each side sent.
/// </summary>
internal static class StompRun
{
    /// <summary>The STOMP port, which curl uses for <c>stomp://</c> when the URL names none.</summary>
    public const int Port = 61613;

    /// <summary>The STOMP over TLS port, which curl uses for <c>stomps://</c> when the URL names none.</summary>
    public const int SecurePort = 61614;

    /// <summary>The CONNECT frame curl sends to <c>localhost</c> with no credentials.</summary>
    public const string PlainConnect = "CONNECT\naccept-version:1.2\nhost:localhost\nheart-beat:0,0\n\n\0";

    /// <summary>A CONNECTED frame choosing STOMP 1.2.</summary>
    public const string Connected = "CONNECTED\nversion:1.2\n\n\0";

    /// <summary>The DISCONNECT frame curl sends after a SEND.</summary>
    public const string Disconnect = "DISCONNECT\nreceipt:disconnect\n\n\0";

    /// <summary>The RECEIPT frame answering curl's DISCONNECT.</summary>
    public const string Receipt = "RECEIPT\nreceipt-id:disconnect\n\n\0";

    /// <summary>The SUBSCRIBE frame curl sends for the destination <c>/q</c>.</summary>
    public const string SubscribeQ = "SUBSCRIBE\nid:0\ndestination:/q\nack:auto\n\n\0";

    private static readonly InProcessCurlRunner Curl = InProcessCurl.RunAsync;

    private static readonly Lazy<X509Certificate2> Certificate = new(() => ReferenceCertificate.CreateSelfSigned("localhost"));

    /// <summary>Gets the cases known to fail (<c>KnownFailures.txt</c> in the test output).</summary>
    internal static KnownFailures Known { get; } = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    /// <summary>Gets a server script that answers CONNECT with <paramref name="connected" /> and then sends <paramref name="frames" /> after the SUBSCRIBE.</summary>
    /// <param name="connected">The server's answer to CONNECT.</param>
    /// <param name="frames">What the server sends once curl has subscribed, after which it closes.</param>
    /// <returns>The script.</returns>
    public static ScriptStep[] SubscribeAnswered(string connected, params string[] frames) =>
        [ScriptStep.ReceiveUntil("\0"), ScriptStep.Send(connected), ScriptStep.ReceiveUntil("ack:auto\n\n\0"), .. frames.Select(ScriptStep.Send)];

    /// <summary>Gets a server script that answers CONNECT and then the DISCONNECT with <paramref name="receipt" />.</summary>
    /// <param name="receipt">The server's answer to DISCONNECT, or none to close without one.</param>
    /// <returns>The script.</returns>
    public static ScriptStep[] PublishAnswered(string? receipt = Receipt) =>
        [ScriptStep.ReceiveUntil("\0"), ScriptStep.Send(Connected), ScriptStep.ReceiveUntil(Disconnect), .. receipt is null ? Array.Empty<ScriptStep>() : [ScriptStep.Send(receipt)]];

    /// <summary>Runs curl with <paramref name="arguments" /> against <paramref name="steps" /> over plain TCP.</summary>
    /// <param name="steps">The server's script.</param>
    /// <param name="arguments">curl's arguments after <c>-sS -m 10</c>.</param>
    /// <returns>The run's result.</returns>
    public static Task<CurlRunResult> RunAsync(ScriptStep[] steps, params string[] arguments) =>
        ExtendedConformanceHarness.RunAsync(Curl, [ReferenceRoute.Tcp(Port, new ScriptedReferenceServer(steps))], ["-sS", "-m", "10", .. arguments]);

    /// <summary>Runs curl with <paramref name="arguments" /> against <paramref name="steps" /> over TLS on the <c>stomps</c> port.</summary>
    /// <param name="steps">The server's script.</param>
    /// <param name="arguments">curl's arguments after <c>-sS -k -m 10</c>.</param>
    /// <returns>The run's result.</returns>
    public static Task<CurlRunResult> RunTlsAsync(ScriptStep[] steps, params string[] arguments) =>
        ExtendedConformanceHarness.RunAsync(Curl, [ReferenceRoute.Tls(SecurePort, new ScriptedReferenceServer(steps), Certificate.Value)], ["-sS", "-k", "-m", "10", .. arguments]);

    /// <summary>Gets everything curl sent, as Latin-1 text; empty when it opened no connection.</summary>
    /// <param name="result">The run's result.</param>
    /// <returns>The text.</returns>
    public static string ClientText(CurlRunResult result) =>
        string.Concat(result.Exchanges.Select(exchange => Encoding.Latin1.GetString(exchange.ClientBytes)));

    /// <summary>Asserts the exit code, standard error (newlines as LF) and standard output.</summary>
    /// <param name="result">The run's result.</param>
    /// <param name="exitCode">The expected exit code.</param>
    /// <param name="standardError">The expected standard error.</param>
    /// <param name="standardOutput">The expected standard output, as Latin-1 text.</param>
    public static void AssertExit(CurlRunResult result, int exitCode, string standardError, string standardOutput = "")
    {
        // curl ends its message line with the platform's newline, CRLF on Windows.
        Assert.AreEqual(standardError, Encoding.Latin1.GetString(result.StandardError).Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(standardOutput, Encoding.Latin1.GetString(result.StandardOutput));
    }
}
