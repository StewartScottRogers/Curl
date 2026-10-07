using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Protocol.Smtp.Fakes;

/// <summary>
/// One SMTP transfer run against a scripted connection, and what it left behind.
/// </summary>
public sealed record SmtpRun(
    TransferResult Result,
    ScriptedConnection Connection,
    QueuedConnector Connector,
    QueuedTlsProvider Tls)
{
    /// <summary>The host name the handler is told the local machine has.</summary>
    public const string LocalHostName = "local-machine";

    /// <summary>The argv of an English Windows system, which every recording was made on.</summary>
    internal static SmtpCommandLineText Windows1252 { get; } =
        SmtpCommandLineText.ForPlatform(isWindows: true, CodePagesEncodingProvider.Instance.GetEncoding(1252));

    /// <summary>
    /// The reply the scripted server gives <c>HELP</c>, which a session with no message to
    /// send asks for once it is open (BL-543).
    /// </summary>
    public const string HelpReply = "214 help\r\n";

    /// <summary>Gets the result of a session whose <c>HELP</c> was answered <see cref="HelpReply" />.</summary>
    public static TransferResult HelpAnswered { get; } =
        TransferResult.Success(HelpReply.Length) with { Report = new TransferReport { ResponseCode = 214 } };

    /// <summary>Gets every byte written to the plaintext connection, as Latin-1 text.</summary>
    public string Sent => Encoding.Latin1.GetString(Connection.Sent);

    /// <summary>
    /// Runs <paramref name="url" /> against <paramref name="connection" />, writing the URL,
    /// the script and the security level as ARRANGE lines and the run's result, commands and
    /// handshakes as ACT lines.
    /// </summary>
    internal static Task<SmtpRun> ExecuteAsync(
        TestDiagnostics diagnostics,
        string url,
        ScriptedConnection connection,
        TransportSecurityLevel sslLevel = TransportSecurityLevel.None,
        params ConnectResult[] handshakes) =>
        ExecuteAsync(diagnostics, new TransferContext { Url = CurlUrl.Parse(url), Output = Stream.Null, SslLevel = sslLevel }, connection, null, Windows1252, handshakes);

    /// <summary>
    /// Runs <paramref name="context" /> against <paramref name="connection" />, writing the
    /// context and the script as ARRANGE lines and the run's result, commands and handshakes
    /// as ACT lines.
    /// </summary>
    internal static Task<SmtpRun> ExecuteAsync(
        TestDiagnostics diagnostics,
        TransferContext context,
        ScriptedConnection connection,
        ISaslAuthenticator? saslAuthenticator = null,
        params ConnectResult[] handshakes) =>
        ExecuteAsync(diagnostics, context, connection, saslAuthenticator, Windows1252, handshakes);

    /// <summary>
    /// Runs the transfer with the handler sending command-line text in
    /// <paramref name="commandLineText" />'s argv bytes, writing the context and the script
    /// as ARRANGE lines and the run's result, commands and handshakes as ACT lines.
    /// </summary>
    internal static async Task<SmtpRun> ExecuteAsync(
        TestDiagnostics diagnostics,
        TransferContext context,
        ScriptedConnection connection,
        ISaslAuthenticator? saslAuthenticator,
        SmtpCommandLineText commandLineText,
        params ConnectResult[] handshakes)
    {
        diagnostics.ArrangeContext(context, connection.Script);
        diagnostics.Arrange("handshakes", string.Join(", ", handshakes.Select(handshake => handshake.Connection is null ? $"failed {handshake.ExitCode}" : "connected")));
        SmtpRun run = await ExecuteAsync(context, connection, saslAuthenticator, commandLineText, handshakes);
        diagnostics.ActRun(run);
        return run;
    }

    public static Task<SmtpRun> ExecuteAsync(
        string url,
        ScriptedConnection connection,
        TransportSecurityLevel sslLevel = TransportSecurityLevel.None,
        params ConnectResult[] handshakes) =>
        ExecuteAsync(new TransferContext { Url = CurlUrl.Parse(url), Output = Stream.Null, SslLevel = sslLevel }, connection, handshakes);

    public static Task<SmtpRun> ExecuteAsync(
        TransferContext context,
        ScriptedConnection connection,
        params ConnectResult[] handshakes) =>
        ExecuteAsync(context, connection, saslAuthenticator: null, handshakes);

    public static Task<SmtpRun> ExecuteAsync(
        TransferContext context,
        ScriptedConnection connection,
        ISaslAuthenticator? saslAuthenticator,
        params ConnectResult[] handshakes) =>
        ExecuteAsync(context, connection, saslAuthenticator, Windows1252, handshakes);

    /// <summary>
    /// Runs the transfer with the handler sending command-line text in
    /// <paramref name="commandLineText" />'s argv bytes. The other overloads use Windows-1252,
    /// the argv of the Windows curl every recording was made with, so they pin the same bytes
    /// on every host.
    /// </summary>
    internal static async Task<SmtpRun> ExecuteAsync(
        TransferContext context,
        ScriptedConnection connection,
        ISaslAuthenticator? saslAuthenticator,
        SmtpCommandLineText commandLineText,
        params ConnectResult[] handshakes)
    {
        var connector = new QueuedConnector(ConnectResult.Connected(connection));
        var tls = new QueuedTlsProvider(handshakes);

        TransferResult result = await new SmtpProtocolHandler(connector, tls, saslAuthenticator, () => LocalHostName, commandLineText).ExecuteAsync(context);

        return new SmtpRun(result, connection, connector, tls);
    }
}
