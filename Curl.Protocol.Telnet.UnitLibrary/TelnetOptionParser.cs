using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Telnet;

/// <summary>
/// Reads the <c>-u</c>/<c>--user</c> user name and the <c>-t</c>/<c>--telnet-option</c>
/// values, <c>NAME=VALUE</c> each, as curl 8.21.0's <c>lib/telnet.c</c> does once connected
/// and before sending a byte.
/// </summary>
/// <remarks>
/// <para>
/// A user name, empty included, becomes the first NEW-ENVIRON variable, <c>USER</c>, cut
/// to 250 characters as curl cuts it. A user name with any non-ASCII character is exit 43,
/// before any <c>-t</c> option is read.
/// </para>
/// <para>
/// The names are <c>TTYPE</c>, <c>XDISPLOC</c>, <c>NEW_ENV</c>, <c>WS</c> and
/// <c>BINARY</c>, matched ignoring case. <c>WS</c> is checked as curl checks it and the
/// last one sets the window size sent in a NAWS subnegotiation. A <c>BINARY</c> value
/// whose leading digits are all zeros (<c>0</c>, <c>00</c>, <c>0x</c>) refuses BINARY;
/// any other value, <c>x</c>, <c>+0</c> and <c>0</c> after a space included, leaves it on, as
/// measured against curl 8.21.0.
/// </para>
/// <para>
/// A value with any non-ASCII character makes curl skip the option unread, whatever its
/// name, so this does too. The first bad option ends the reading: a value without
/// <c>=</c> is exit 49, a <c>WS</c> value that is not <c>COLUMNSxROWS</c> of two numbers
/// up to 65535 is exit 49, and an unknown name is exit 48. curl picks the name by its
/// length in UTF-8 bytes first, so an unknown name as long as a known one gets curl's
/// generic message and any other unknown name gets <c>Unknown telnet option</c>.
/// </para>
/// </remarks>
internal static class TelnetOptionParser
{
    private const string TerminalTypeName = "TTYPE";

    private const string XDisplayLocationName = "XDISPLOC";

    private const string NewEnvironmentName = "NEW_ENV";

    private const string WindowSizeName = "WS";

    private const string BinaryName = "BINARY";

    /// <summary>The exit 48 text curl prints, without a <c>-v</c> line, for an option name of a known one's length that matches none.</summary>
    internal const string UnknownOptionMessage = "An unknown option was passed in to libcurl";

    /// <summary>The exit 43 text curl prints, without a <c>-v</c> line, for a non-ASCII user name.</summary>
    internal const string BadFunctionArgumentMessage = "A libcurl function was given a bad argument";

    /// <summary>
    /// The most user name characters curl 8.21.0 sends: it formats <c>USER,</c> and the
    /// name into a 256-byte buffer, so 255 characters with the terminating NUL.
    /// </summary>
    private const int MaximumUserNameLength = 250;

    /// <summary>The known names; no two are the same length, as curl relies on.</summary>
    private static readonly string[] KnownNames =
        [TerminalTypeName, XDisplayLocationName, NewEnvironmentName, WindowSizeName, BinaryName];

    /// <summary>
    /// Reads the user name, then every option, into <paramref name="values" />, in order.
    /// </summary>
    /// <param name="userName">The <c>-u</c> user name, or <see langword="null" /> without <c>-u</c>.</param>
    /// <param name="options">The <c>-t</c> values, verbatim.</param>
    /// <param name="values">Receives what the user name and the options supplied.</param>
    /// <returns>
    /// <see langword="null" />, or the failure a non-ASCII user name or the first bad option
    /// ends the transfer with.
    /// </returns>
    public static TransferResult? Parse(string? userName, IReadOnlyList<string> options, TelnetOptionValues values)
    {
        if (userName is not null)
        {
            if (!Ascii.IsValid(userName))
            {
                return new TransferResult(CurlExitCode.BadFunctionArgument, 0, BadFunctionArgumentMessage);
            }

            values.EnvironmentVariables.Add($"USER,{userName[..Math.Min(userName.Length, MaximumUserNameLength)]}");
        }

        foreach (string option in options)
        {
            TransferResult? failure = ParseOption(option, values);
            if (failure is not null)
            {
                return failure;
            }
        }

        return null;
    }

    private static TransferResult? ParseOption(string option, TelnetOptionValues values)
    {
        int separator = option.IndexOf('=', StringComparison.Ordinal);
        if (separator < 0)
        {
            return SyntaxError(option);
        }

        string name = option[..separator];
        string value = option[(separator + 1)..];
        if (!Ascii.IsValid(value))
        {
            return null;
        }

        int nameLength = Encoding.UTF8.GetByteCount(name);
        string? knownName = Array.Find(KnownNames, known => known.Length == nameLength);
        if (knownName is null)
        {
            return new TransferResult(CurlExitCode.UnknownOption, 0, $"Unknown telnet option {option}");
        }

        return string.Equals(name, knownName, StringComparison.OrdinalIgnoreCase)
            ? ApplyOption(knownName, option, value, values)
            : new TransferResult(CurlExitCode.UnknownOption, 0, UnknownOptionMessage);
    }

    private static TransferResult? ApplyOption(string knownName, string option, string value, TelnetOptionValues values)
    {
        switch (knownName)
        {
            case TerminalTypeName:
                values.TerminalType = value;
                return null;
            case XDisplayLocationName:
                values.XDisplayLocation = value;
                return null;
            case NewEnvironmentName:
                values.EnvironmentVariables.Add(value);
                return null;
            case WindowSizeName:
                return ApplyWindowSize(option, value, values);
            default:
                values.BinaryRefused |= IsZero(value);
                return null;
        }
    }

    private static bool IsZero(string value)
    {
        int digits = 0;
        while (digits < value.Length && char.IsAsciiDigit(value[digits]))
        {
            digits++;
        }

        return digits > 0 && value.AsSpan(0, digits).TrimStart('0').IsEmpty;
    }

    private static TransferResult? ApplyWindowSize(string option, string value, TelnetOptionValues values)
    {
        int index = 0;
        if (!TryReadDimension(value, ref index, out ushort columns)
            || index == value.Length
            || value[index] != 'x')
        {
            return SyntaxError(option);
        }

        index++;
        if (!TryReadDimension(value, ref index, out ushort rows))
        {
            return SyntaxError(option);
        }

        values.WindowSize = new TelnetWindowSize(columns, rows);
        return null;
    }

    private static bool TryReadDimension(string value, ref int index, out ushort dimension)
    {
        int start = index;
        dimension = 0;
        while (index < value.Length && char.IsAsciiDigit(value[index]))
        {
            int widened = (dimension * 10) + (value[index] - '0');
            if (widened > ushort.MaxValue)
            {
                return false;
            }

            dimension = (ushort)widened;

            index++;
        }

        return index > start;
    }

    private static TransferResult SyntaxError(string option) =>
        new(CurlExitCode.SetoptOptionSyntax, 0, $"Syntax error in telnet option: {option}");
}
