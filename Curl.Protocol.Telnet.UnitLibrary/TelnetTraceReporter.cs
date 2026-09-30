using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Telnet;

/// <summary>
/// Reports a TELNET session to <see cref="ITransferEvents" /> as curl 8.21.0's <c>-v</c> and
/// <c>--trace</c> show it after connecting (measured, BL-935 Notes): each option negotiation
/// received and sent as <c>RCVD DO TERM TYPE</c> or <c>SENT WONT ECHO</c>, any other command
/// received as <c>RCVD IAC NOP</c>, each subnegotiation received and sent in the pieces
/// <c>lib/telnet.c</c>'s <c>printsub</c> writes, one information line each, each run of
/// output data as data received, and the line the connection ends with.
/// </summary>
/// <param name="events">Where the lines and blocks go.</param>
/// <remarks>
/// Nothing sent is reported as data - neither negotiation nor the upload - because curl
/// traces neither, and the server's close is not reported as a zero-byte block.
/// </remarks>
internal sealed class TelnetTraceReporter(ITransferEvents events)
{
    /// <summary>The first option number <c>lib/telnet.c</c> has no name for.</summary>
    private const int FirstUnnamedOption = 40;

    /// <summary>The lowest command byte <c>lib/telnet.c</c> names, <c>EOF</c>.</summary>
    private const int FirstNamedCommand = 236;

    /// <summary>The option byte <c>lib/telnet.c</c> names <c>EXOPL</c>, beyond its table.</summary>
    private const byte ExtendedOptionsList = 0xFF;

    /// <summary>The subnegotiation qualifiers <c>printsub</c> names, by value.</summary>
    private static readonly string[] QualifierNames = [" IS", " SEND", " INFO/REPLY", " NAME"];

    /// <summary>curl's names for options 0 to 39, by option number.</summary>
    private static readonly string[] OptionNames =
    [
        "BINARY", "ECHO", "RCP", "SUPPRESS GO AHEAD",
        "NAME", "STATUS", "TIMING MARK", "RCTE",
        "NAOL", "NAOP", "NAOCRD", "NAOHTS",
        "NAOHTD", "NAOFFD", "NAOVTS", "NAOVTD",
        "NAOLFD", "EXTEND ASCII", "LOGOUT", "BYTE MACRO",
        "DE TERMINAL", "SUPDUP", "SUPDUP OUTPUT", "SEND LOCATION",
        "TERM TYPE", "END OF RECORD", "TACACS UID", "OUTPUT MARKING",
        "TTYLOC", "3270 REGIME", "X3 PAD", "NAWS",
        "TERM SPEED", "LFLOW", "LINEMODE", "XDISPLOC",
        "OLD-ENVIRON", "AUTHENTICATION", "ENCRYPT", "NEW-ENVIRON",
    ];

    /// <summary>The options <c>printsub</c> names without <c>(unsupported)</c>.</summary>
    private static readonly byte[] SubnegotiatedOptions =
    [
        TelnetByte.TerminalTypeOption,
        TelnetByte.WindowSizeOption,
        TelnetByte.XDisplayLocationOption,
        TelnetByte.NewEnvironmentOption,
    ];

    /// <summary>curl's names for command bytes 236 to 255, by byte less 236.</summary>
    private static readonly string[] CommandNames =
    [
        "EOF", "SUSP", "ABORT", "EOR", "SE", "NOP", "DMARK", "BRK", "IP", "AO",
        "AYT", "EC", "EL", "GA", "SB", "WILL", "WONT", "DO", "DONT", "IAC",
    ];

    /// <summary>Reports a <c>WILL</c>, <c>WONT</c>, <c>DO</c> or <c>DONT</c> received.</summary>
    /// <param name="command">The negotiation command.</param>
    /// <param name="option">The option it names.</param>
    public void OptionReceived(byte command, byte option) => events.ReportInfo(NegotiationLine("RCVD", command, option));

    /// <summary>Reports a <c>WILL</c>, <c>WONT</c>, <c>DO</c> or <c>DONT</c> sent.</summary>
    /// <param name="command">The negotiation command.</param>
    /// <param name="option">The option it names.</param>
    public void OptionSent(byte command, byte option) => events.ReportInfo(NegotiationLine("SENT", command, option));

    /// <summary>
    /// Reports a command received that is neither negotiation, subnegotiation nor an escaped
    /// <c>0xFF</c>: <c>RCVD IAC</c> and its name, or its number below 236.
    /// </summary>
    /// <param name="command">The byte after <c>IAC</c>.</param>
    public void CommandReceived(byte command) =>
        events.ReportInfo("RCVD IAC " + (command >= FirstNamedCommand
            ? CommandNames[command - FirstNamedCommand]
            : command.ToString(CultureInfo.InvariantCulture)));

    /// <summary>Reports a subnegotiation received.</summary>
    /// <param name="subnegotiation">
    /// Its bytes between <c>IAC SB</c> and <c>IAC SE</c>, option first, with each doubled
    /// <c>0xFF</c> undone; not empty.
    /// </param>
    public void SubnegotiationReceived(ReadOnlySpan<byte> subnegotiation) => Subnegotiation("RCVD", subnegotiation);

    /// <summary>Reports a subnegotiation sent.</summary>
    /// <param name="subnegotiation">
    /// Its bytes between <c>IAC SB</c> and <c>IAC SE</c>, option first, before any
    /// <c>0xFF</c> is doubled.
    /// </param>
    public void SubnegotiationSent(ReadOnlySpan<byte> subnegotiation) => Subnegotiation("SENT", subnegotiation);

    /// <summary>Reports one run of output data, as curl passes each run to its writer.</summary>
    /// <param name="data">The bytes of the run.</param>
    public void DataReceived(ReadOnlySpan<byte> data) => events.ReportDataReceived(data);

    /// <summary>
    /// Reports how the session ended, as curl 8.21.0 does: the failure's message unless it is
    /// a text curl prints without <c>failf</c>, then <c>closing connection #N</c> after an
    /// output write failure and <c>shutting down connection #N</c> after anything else.
    /// </summary>
    /// <param name="result">How the session ended.</param>
    /// <param name="connectionNumber">The connection's number.</param>
    public void ConnectionEnded(TransferResult result, long connectionNumber)
    {
        if (result.ErrorMessage is { } message && !IsStrerrorText(message))
        {
            events.ReportInfo(message);
        }

        string ending = result.ExitCode == CurlExitCode.WriteError ? "closing" : "shutting down";
        events.ReportInfo(string.Create(CultureInfo.InvariantCulture, $"{ending} connection #{connectionNumber}"));
    }

    /// <summary>
    /// Whether <paramref name="message" /> is the text curl prints for an exit code it
    /// returns without calling <c>failf</c>, and so without a <c>-v</c> line.
    /// </summary>
    private static bool IsStrerrorText(string message) =>
        message is TelnetOptionParser.BadFunctionArgumentMessage or TelnetOptionParser.UnknownOptionMessage;

    private static string NegotiationLine(string direction, byte command, byte option)
    {
        string name = command switch
        {
            TelnetByte.Will => "WILL",
            TelnetByte.Wont => "WONT",
            TelnetByte.Do => "DO",
            _ => "DONT",
        };
        return direction + " " + name + " " + OptionName(option);
    }

    private static string OptionName(byte option) => option switch
    {
        < FirstUnnamedOption => OptionNames[option],
        ExtendedOptionsList => "EXOPL",
        _ => option.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>
    /// Reports a subnegotiation in the pieces <c>printsub</c> writes, each its own line:
    /// the direction, the option, then the window size for NAWS, or the qualifier and what
    /// follows it for any other. One of a single byte is <c>(Empty suboption?)</c>, as
    /// measured.
    /// </summary>
    private void Subnegotiation(string direction, ReadOnlySpan<byte> subnegotiation)
    {
        events.ReportInfo(direction + " IAC SB ");
        if (subnegotiation.Length < 2)
        {
            events.ReportInfo("(Empty suboption?)");
            return;
        }

        byte option = subnegotiation[0];
        events.ReportInfo(SubnegotiationOptionName(option));
        if (option == TelnetByte.WindowSizeOption)
        {
            ReportWindowSize(subnegotiation);
            return;
        }

        if (subnegotiation[1] < QualifierNames.Length)
        {
            events.ReportInfo(QualifierNames[subnegotiation[1]]);
        }

        ReportParameters(option, subnegotiation);
    }

    /// <summary>
    /// The option as <c>printsub</c> names it: the four options curl subnegotiates by name,
    /// any other it has a name for with <c>(unsupported)</c>, and the rest by number.
    /// </summary>
    private static string SubnegotiationOptionName(byte option)
    {
        if (option >= FirstUnnamedOption)
        {
            return option.ToString(CultureInfo.InvariantCulture) + " (unknown)";
        }

        return Array.IndexOf(SubnegotiatedOptions, option) >= 0
            ? OptionNames[option]
            : OptionNames[option] + " (unsupported)";
    }

    private void ReportWindowSize(ReadOnlySpan<byte> subnegotiation)
    {
        if (subnegotiation.Length > 4)
        {
            int width = (subnegotiation[1] << 8) | subnegotiation[2];
            int height = (subnegotiation[3] << 8) | subnegotiation[4];
            events.ReportInfo(string.Create(CultureInfo.InvariantCulture, $"Width: {width} ; Height: {height}"));
        }
    }

    /// <summary>
    /// Reports what follows the qualifier: a terminal type or X display location quoted, a
    /// NEW-ENVIRON <c>IS</c> list byte by byte, and for any other option each byte in hex.
    /// </summary>
    private void ReportParameters(byte option, ReadOnlySpan<byte> subnegotiation)
    {
        ReadOnlySpan<byte> parameters = subnegotiation[2..];
        switch (option)
        {
            case TelnetByte.TerminalTypeOption or TelnetByte.XDisplayLocationOption:
                events.ReportInfo(" \"" + CString(parameters) + "\"");
                break;
            case TelnetByte.NewEnvironmentOption:
                ReportEnvironment(subnegotiation);
                break;
            default:
                foreach (byte value in parameters)
                {
                    events.ReportInfo(" " + value.ToString("x2", CultureInfo.InvariantCulture));
                }

                break;
        }
    }

    /// <summary>
    /// Reports a NEW-ENVIRON <c>IS</c> list as <c>printsub</c> does: a lone space, then each
    /// byte after the first variable marker on its own line, a variable marker as
    /// <c>", "</c> and a value marker as <c>" = "</c>. Any other qualifier reports nothing.
    /// </summary>
    private void ReportEnvironment(ReadOnlySpan<byte> subnegotiation)
    {
        if (subnegotiation[1] != TelnetByte.IsQualifier)
        {
            return;
        }

        events.ReportInfo(" ");
        for (int index = 3; index < subnegotiation.Length; index++)
        {
            events.ReportInfo(subnegotiation[index] switch
            {
                TelnetByte.EnvironmentVariable => ", ",
                TelnetByte.EnvironmentValue => " = ",
                byte other => Encoding.Latin1.GetString([other]),
            });
        }
    }

    /// <summary>The bytes up to the first NUL, as <c>printf</c>'s <c>%s</c> writes them.</summary>
    private static string CString(ReadOnlySpan<byte> bytes)
    {
        int nul = bytes.IndexOf((byte)0);
        return Encoding.Latin1.GetString(nul < 0 ? bytes : bytes[..nul]);
    }
}
