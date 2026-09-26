using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Telnet;

/// <summary>
/// Reads the <c>-t</c>/<c>--telnet-option</c> values, <c>NAME=VALUE</c> each, as curl
/// 8.21.0's <c>lib/telnet.c</c> does once connected and before sending a byte.
/// </summary>
/// <remarks>
/// <para>
/// The names are <c>TTYPE</c>, <c>XDISPLOC</c>, <c>NEW_ENV</c>, <c>WS</c> and
/// <c>BINARY</c>, matched ignoring case. <c>WS</c> and <c>BINARY</c> are checked as curl
/// checks them and otherwise ignored: the window size and binary refusal they ask for are
/// not negotiated.
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

    private const string UnknownOptionMessage = "An unknown option was passed in to libcurl";

    /// <summary>The known names; no two are the same length, as curl relies on.</summary>
    private static readonly string[] KnownNames =
        [TerminalTypeName, XDisplayLocationName, NewEnvironmentName, WindowSizeName, BinaryName];

    /// <summary>
    /// Reads every option into <paramref name="values" />, in order.
    /// </summary>
    /// <param name="options">The <c>-t</c> values, verbatim.</param>
    /// <param name="values">Receives what the options supplied.</param>
    /// <returns>
    /// <see langword="null" />, or the failure the first bad option ends the transfer with.
    /// </returns>
    public static TransferResult? Parse(IReadOnlyList<string> options, TelnetOptionValues values)
    {
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
                return IsWindowSize(value) ? null : SyntaxError(option);
            default:
                return null;
        }
    }

    private static bool IsWindowSize(string value)
    {
        int index = 0;
        if (!TryReadDimension(value, ref index) || index == value.Length || value[index] != 'x')
        {
            return false;
        }

        index++;
        return TryReadDimension(value, ref index);
    }

    private static bool TryReadDimension(string value, ref int index)
    {
        int start = index;
        int dimension = 0;
        while (index < value.Length && char.IsAsciiDigit(value[index]))
        {
            dimension = (dimension * 10) + (value[index] - '0');
            if (dimension > ushort.MaxValue)
            {
                return false;
            }

            index++;
        }

        return index > start;
    }

    private static TransferResult SyntaxError(string option) =>
        new(CurlExitCode.SetoptOptionSyntax, 0, $"Syntax error in telnet option: {option}");
}
