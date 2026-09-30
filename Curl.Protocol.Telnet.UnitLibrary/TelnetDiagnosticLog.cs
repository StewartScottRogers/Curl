using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Telnet;

/// <summary>
/// Writes the TELNET session steps to Curl's own diagnostic log, component
/// <see cref="DiagnosticLogComponents.Telnet" /> (ADR-0222, BL-928): the failure that ends a
/// session as <c>error</c>, an option refused as <c>warning</c>, the session's start and end
/// as <c>info</c>, and each option negotiation received and sent as <c>verbose</c>.
/// </summary>
/// <param name="log">Where the lines go; <see cref="NoDiagnosticLog.Instance" /> writes nothing.</param>
/// <remarks>
/// Every method tests <see cref="IDiagnosticLog.IsEnabled" /> before it builds its message,
/// so a disabled level costs no formatting. An option is named as <c>-t</c> and RFC 1143
/// name it (<c>BINARY</c>, <c>ECHO</c>, <c>SGA</c>, <c>TTYPE</c>, <c>NAWS</c>,
/// <c>XDISPLOC</c>, <c>NEW-ENVIRON</c>), any other by its decimal number.
/// </remarks>
internal sealed class TelnetDiagnosticLog(IDiagnosticLog log)
{
    /// <summary>The negotiation commands by byte.</summary>
    private static readonly Dictionary<byte, string> CommandNames = new()
    {
        [TelnetByte.Will] = "WILL",
        [TelnetByte.Wont] = "WONT",
        [TelnetByte.Do] = "DO",
        [TelnetByte.Dont] = "DONT",
    };

    /// <summary>The options this side knows by name, by option number.</summary>
    private static readonly Dictionary<byte, string> OptionNames = new()
    {
        [TelnetByte.BinaryOption] = "BINARY",
        [TelnetByte.EchoOption] = "ECHO",
        [TelnetByte.SuppressGoAheadOption] = "SGA",
        [TelnetByte.TerminalTypeOption] = "TTYPE",
        [TelnetByte.WindowSizeOption] = "NAWS",
        [TelnetByte.XDisplayLocationOption] = "XDISPLOC",
        [TelnetByte.NewEnvironmentOption] = "NEW-ENVIRON",
    };

    /// <summary>Logs, at <c>info</c>, that the session to <paramref name="host" /> started.</summary>
    /// <param name="host">The host connected to.</param>
    /// <param name="port">The port connected to.</param>
    public void SessionStarted(string host, int port)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, string.Create(CultureInfo.InvariantCulture, $"session started to {host}:{port}"));
        }
    }

    /// <summary>
    /// Logs how the session ended: <c>info</c> with the bytes and milliseconds for a success,
    /// <c>error</c> with the <see cref="CurlExitCode" /> and message for a failure.
    /// </summary>
    /// <param name="result">The session's outcome.</param>
    /// <param name="elapsed">How long the session took.</param>
    public void SessionEnded(TransferResult result, TimeSpan elapsed)
    {
        if (!result.IsSuccess)
        {
            Failed(result);
        }
        else if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, string.Create(
                CultureInfo.InvariantCulture,
                $"session ended: {result.BytesTransferred} bytes in {(long)elapsed.TotalMilliseconds} ms"));
        }
    }

    /// <summary>Logs, at <c>error</c>, a failure that ends the session with its <see cref="CurlExitCode" />.</summary>
    /// <param name="result">The failure.</param>
    public void Failed(TransferResult result)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Error))
        {
            Write(DiagnosticLogLevel.Error, string.Create(
                CultureInfo.InvariantCulture,
                $"failed with {result.ExitCode} ({(int)result.ExitCode}): {result.ErrorMessage}"));
        }
    }

    /// <summary>Logs, at <c>verbose</c>, an option negotiation the server sent, such as <c>recv DO NAWS</c>.</summary>
    /// <param name="command"><see cref="TelnetByte.Will" />, <see cref="TelnetByte.Wont" />, <see cref="TelnetByte.Do" /> or <see cref="TelnetByte.Dont" />.</param>
    /// <param name="option">The option named.</param>
    public void OptionReceived(byte command, byte option) => Negotiation("recv ", command, option);

    /// <summary>Logs, at <c>verbose</c>, an option negotiation this side sent, such as <c>sent WILL TTYPE</c>.</summary>
    /// <param name="command"><see cref="TelnetByte.Will" />, <see cref="TelnetByte.Wont" />, <see cref="TelnetByte.Do" /> or <see cref="TelnetByte.Dont" />.</param>
    /// <param name="option">The option named.</param>
    public void OptionSent(byte command, byte option) => Negotiation("sent ", command, option);

    /// <summary>
    /// Logs, at <c>warning</c>, that the server's request or offer of an option this side
    /// does not agree to was refused, and with which command.
    /// </summary>
    /// <param name="refusal"><see cref="TelnetByte.Wont" /> or <see cref="TelnetByte.Dont" />.</param>
    /// <param name="option">The option refused.</param>
    public void OptionRefused(byte refusal, byte option)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Warning))
        {
            Write(DiagnosticLogLevel.Warning, "refused option " + OptionName(option) + " with " + CommandNames[refusal]);
        }
    }

    private static string OptionName(byte option) =>
        OptionNames.TryGetValue(option, out string? name) ? name : option.ToString(CultureInfo.InvariantCulture);

    private void Negotiation(string direction, byte command, byte option)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, direction + CommandNames[command] + " " + OptionName(option));
        }
    }

    private void Write(DiagnosticLogLevel level, string message) => log.Write(level, DiagnosticLogComponents.Telnet, message);
}
