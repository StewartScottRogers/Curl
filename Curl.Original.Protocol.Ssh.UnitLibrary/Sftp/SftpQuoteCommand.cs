using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Ssh;

namespace Curl.Original.Protocol.Ssh.Sftp;

/// <summary>
/// One <c>-Q</c>/<c>--quote</c> command for an SFTP transfer, read as curl 8.21.0's
/// <c>sftp_quote</c> reads it (ADR-0247): a leading <c>*</c> lets the command fail;
/// <c>pwd</c>, in any case, stands alone; every other command is its name, a space and
/// its paths, each taken by <c>Curl_get_pathname</c>. Measured 2026-09-29 (BL-572).
/// </summary>
/// <param name="Operation">What the command does.</param>
/// <param name="IgnoresFailure">Whether a leading <c>*</c> asked for a failure on the server to be passed over.</param>
/// <param name="Text">The command as given, less its <c>*</c>, which curl's messages quote.</param>
/// <param name="FirstPath">The first argument's bytes: the path, or for <c>chgrp</c>, <c>chmod</c>, <c>chown</c>, <c>atime</c> and <c>mtime</c> the value to set; empty for <c>pwd</c>.</param>
/// <param name="SecondPath">The second argument's bytes, for the commands that take two; otherwise empty.</param>
internal sealed record SftpQuoteCommand(SftpQuoteOperation Operation, bool IgnoresFailure, string Text, byte[] FirstPath, byte[] SecondPath)
{
    /// <summary>curl's message for a command that is not one it knows.</summary>
    internal const string UnknownCommand = "Unknown SFTP command";

    /// <summary>curl's message for anything after a command's last argument.</summary>
    internal const string SuspiciousData = "Suspicious data after the command line";

    private const string ChangeAttributeSecondParameter = "attribute";

    private const string SymbolicLinkSecondParameter = "Syntax error in ln/symlink: Bad second parameter";

    private const string RenameSecondParameter = "Syntax error in rename: Bad second parameter";

    // curl's order of tests, each name with its space; the text for a missing second
    // argument, or null for a command that takes one.
    private static readonly (string Prefix, SftpQuoteOperation Operation, string? SecondParameterFailure)[] Commands =
    [
        ("chgrp ", SftpQuoteOperation.ChangeGroup, ChangeAttributeSecondParameter),
        ("chmod ", SftpQuoteOperation.ChangeMode, ChangeAttributeSecondParameter),
        ("chown ", SftpQuoteOperation.ChangeOwner, ChangeAttributeSecondParameter),
        ("atime ", SftpQuoteOperation.SetAccessTime, ChangeAttributeSecondParameter),
        ("mtime ", SftpQuoteOperation.SetModifyTime, ChangeAttributeSecondParameter),
        ("ln ", SftpQuoteOperation.SymbolicLink, SymbolicLinkSecondParameter),
        ("symlink ", SftpQuoteOperation.SymbolicLink, SymbolicLinkSecondParameter),
        ("mkdir ", SftpQuoteOperation.MakeDirectory, null),
        ("rename ", SftpQuoteOperation.Rename, RenameSecondParameter),
        ("rmdir ", SftpQuoteOperation.RemoveDirectory, null),
        ("rm ", SftpQuoteOperation.Remove, null),
        ("statvfs ", SftpQuoteOperation.StatFileSystem, null),
    ];

    /// <summary>
    /// Reads <paramref name="value" /> as curl does.
    /// </summary>
    /// <param name="value">The command, with its <c>-</c> already removed and any <c>*</c> still on it.</param>
    /// <param name="homeDirectory">The server's answer to <c>REALPATH .</c>, which an unquoted <c>/~/</c> path starts from.</param>
    /// <returns>The command.</returns>
    /// <exception cref="SshTransferException">
    /// Exit 21 with curl's message: <c>Syntax error command '&lt;command&gt;', missing
    /// parameter</c> when no space follows the name, <c>Syntax error: Bad first parameter to
    /// '&lt;command&gt;'</c> or the command's own text for a bad second argument,
    /// <see cref="SuspiciousData" /> for more after the last, and
    /// <see cref="UnknownCommand" /> for a name curl does not know; exit 100 with the same bad
    /// first or second parameter text for a path of 65535 bytes or more (BL-2918).
    /// </exception>
    internal static SftpQuoteCommand Parse(string value, byte[] homeDirectory)
    {
        bool ignoresFailure = value.StartsWith('*');
        string text = ignoresFailure ? value[1..] : value;
        return string.Equals(text, "pwd", StringComparison.OrdinalIgnoreCase)
            ? new SftpQuoteCommand(SftpQuoteOperation.PrintWorkingDirectory, ignoresFailure, text, [], [])
            : ParseWithArguments(text, ignoresFailure, homeDirectory);
    }

    /// <summary>
    /// curl 8.22.0's <c>Curl_get_pathname</c> on its own, for the port of upstream's
    /// <c>unit2604</c> (BL-2905): reads one path from the start of <paramref name="value" />
    /// as each argument of a <c>-Q</c> command is read.
    /// </summary>
    /// <param name="value">The text the path starts in, blanks before it allowed.</param>
    /// <param name="homeDirectory">The home directory an unquoted <c>/~/</c> path starts from.</param>
    /// <returns>
    /// curl's code (<see cref="CurlExitCode.Ok" />; <see cref="CurlExitCode.QuoteError" /> for
    /// no path, an empty or unclosed quote or an unknown escape;
    /// <see cref="CurlExitCode.TooLarge" /> for a path of 65535 bytes or more), the path, and
    /// the text after the path and its blanks; the path and the rest are empty unless the
    /// code is <see cref="CurlExitCode.Ok" />.
    /// </returns>
    internal static (CurlExitCode Result, byte[] Path, string Remainder) ReadPathname(string value, byte[] homeDirectory)
    {
        ArgumentReader reader = new(value, 0, homeDirectory);
        CurlExitCode result = reader.ReadPathname(out byte[] path);
        return result == CurlExitCode.Ok ? (result, path, value[reader.Position..]) : (result, [], "");
    }

    // curl takes the first argument before it looks at the name, so a bad one is reported
    // even for a name it does not know.
    private static SftpQuoteCommand ParseWithArguments(string text, bool ignoresFailure, byte[] homeDirectory)
    {
        int space = text.IndexOf(' ', StringComparison.Ordinal);
        ArgumentReader arguments = space >= 0
            ? new ArgumentReader(text, space, homeDirectory)
            : throw SshTransferException.SftpQuoteFailed($"Syntax error command '{text}', missing parameter");
        byte[] first = arguments.Read(out CurlExitCode firstResult)
            ?? throw SshTransferException.SftpQuoteFailed(firstResult, $"Syntax error: Bad first parameter to '{text}'");
        (string _, SftpQuoteOperation operation, string? secondParameterFailure) = Find(text);
        byte[] second = secondParameterFailure is null ? [] : ReadSecond(arguments, text, secondParameterFailure);
        return arguments.AtEnd
            ? new SftpQuoteCommand(operation, ignoresFailure, text, first, second)
            : throw SshTransferException.SftpQuoteFailed(SuspiciousData);
    }

    private static (string Prefix, SftpQuoteOperation Operation, string? SecondParameterFailure) Find(string text)
    {
        (string Prefix, SftpQuoteOperation Operation, string? SecondParameterFailure) found =
            Array.Find(Commands, command => text.StartsWith(command.Prefix, StringComparison.Ordinal));
        return found.Operation != SftpQuoteOperation.None ? found : throw SshTransferException.SftpQuoteFailed(UnknownCommand);
    }

    private static byte[] ReadSecond(ArgumentReader arguments, string text, string failure) =>
        arguments.Read(out CurlExitCode result) ?? throw SshTransferException.SftpQuoteFailed(
            result,
            failure == ChangeAttributeSecondParameter ? $"Syntax error in {text}: Bad second parameter" : failure);

    // curl's Curl_get_pathname over the command's text: blanks are spaces and tabs, a word
    // ends at a space, a quoted path unescapes \", \' and \\, and an unquoted /~/ becomes
    // the home directory and a slash. A path must stay under curl's MAX_PATHLENGTH.
    private sealed class ArgumentReader(string text, int start, byte[] homeDirectory)
    {
        // curl's MAX_PATHLENGTH: its buffer refuses a path of this many bytes or more.
        private const int MaximumPathLength = 65535;

        private static readonly byte[] Slash = "/"u8.ToArray();

        private int position = start;

        internal int Position => position;

        internal bool AtEnd => position >= text.Length;

        // The next argument, or null where curl finds none, a malformed one or one too long;
        // result is curl's code for it, which sftp_quote returns as the transfer's.
        internal byte[]? Read(out CurlExitCode result)
        {
            result = ReadPathname(out byte[] path);
            return result == CurlExitCode.Ok ? path : null;
        }

        // Curl_get_pathname: the next argument and curl's code for it.
        internal CurlExitCode ReadPathname(out byte[] path)
        {
            path = [];
            if (AtEnd)
            {
                return CurlExitCode.QuoteError;
            }

            SkipBlanks();
            CurlExitCode result = position < text.Length && text[position] is '"' or '\'' ? ReadQuoted(out path) : ReadWord(out path);
            SkipBlanks();
            return result;
        }

        private static CurlExitCode Measured(byte[] path) =>
            path.Length < MaximumPathLength ? CurlExitCode.Ok : CurlExitCode.TooLarge;

        private static bool IsBlank(char character) => character is ' ' or '\t';

        private void SkipBlanks()
        {
            while (position < text.Length && IsBlank(text[position]))
            {
                position++;
            }
        }

        private CurlExitCode ReadQuoted(out byte[] path)
        {
            char quote = text[position++];
            StringBuilder argument = new();
            CurlExitCode result = AppendUntil(quote, argument);
            path = Encoding.UTF8.GetBytes(argument.ToString());
            return result == CurlExitCode.Ok ? PassClosingQuote(argument.Length) : result;
        }

        private CurlExitCode AppendUntil(char quote, StringBuilder argument)
        {
            CurlExitCode result = CurlExitCode.Ok;
            while (result == CurlExitCode.Ok && position < text.Length && text[position] != quote)
            {
                result = AppendCharacter(argument);
            }

            return result;
        }

        // Past the closing quote; curl refuses an unclosed or an empty one.
        private CurlExitCode PassClosingQuote(int length) =>
            position++ < text.Length && length > 0 ? CurlExitCode.Ok : CurlExitCode.QuoteError;

        // A backslash takes the character after it, which must be a quote or a backslash;
        // curl's buffer refuses the character that makes the path too long, as it is added.
        private CurlExitCode AppendCharacter(StringBuilder argument)
        {
            if (text[position] == '\\' && !IsEscapable(++position))
            {
                return CurlExitCode.QuoteError;
            }

            argument.Append(text[position++]);
            return argument.Length < MaximumPathLength ? CurlExitCode.Ok : CurlExitCode.TooLarge;
        }

        private bool IsEscapable(int index) => index < text.Length && text[index] is '"' or '\'' or '\\';

        // An empty word is a missing path unless /~/ gave it the home directory.
        private CurlExitCode ReadWord(out byte[] path)
        {
            bool fromHome = text.AsSpan(position).StartsWith("/~/", StringComparison.Ordinal);
            position += fromHome ? 3 : 0;
            int end = text.IndexOf(' ', position);
            end = end < 0 ? text.Length : end;
            byte[] word = Encoding.UTF8.GetBytes(text[position..end]);
            position = end;
            byte[] start = fromHome ? [.. homeDirectory, .. Slash] : [];
            path = [.. start, .. word];
            return word.Length > 0 || fromHome ? Measured(path) : CurlExitCode.QuoteError;
        }
    }
}
