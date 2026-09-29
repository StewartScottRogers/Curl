using System.Text;

namespace Curl.Protocol.Ssh.Sftp;

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
    /// <see cref="UnknownCommand" /> for a name curl does not know.
    /// </exception>
    internal static SftpQuoteCommand Parse(string value, byte[] homeDirectory)
    {
        bool ignoresFailure = value.StartsWith('*');
        string text = ignoresFailure ? value[1..] : value;
        return string.Equals(text, "pwd", StringComparison.OrdinalIgnoreCase)
            ? new SftpQuoteCommand(SftpQuoteOperation.PrintWorkingDirectory, ignoresFailure, text, [], [])
            : ParseWithArguments(text, ignoresFailure, homeDirectory);
    }

    // curl takes the first argument before it looks at the name, so a bad one is reported
    // even for a name it does not know.
    private static SftpQuoteCommand ParseWithArguments(string text, bool ignoresFailure, byte[] homeDirectory)
    {
        int space = text.IndexOf(' ', StringComparison.Ordinal);
        ArgumentReader arguments = space >= 0
            ? new ArgumentReader(text, space, homeDirectory)
            : throw SshTransferException.SftpQuoteFailed($"Syntax error command '{text}', missing parameter");
        byte[] first = arguments.Read() ?? throw SshTransferException.SftpQuoteFailed($"Syntax error: Bad first parameter to '{text}'");
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
        arguments.Read() ?? throw SshTransferException.SftpQuoteFailed(
            failure == ChangeAttributeSecondParameter ? $"Syntax error in {text}: Bad second parameter" : failure);

    // curl's Curl_get_pathname over the command's text: blanks are spaces and tabs, a word
    // ends at a space, a quoted path unescapes \", \' and \\, and an unquoted /~/ becomes
    // the home directory and a slash.
    private sealed class ArgumentReader(string text, int position, byte[] homeDirectory)
    {
        private static readonly byte[] Slash = "/"u8.ToArray();

        internal bool AtEnd => position >= text.Length;

        // The next argument, or null where curl finds none or a malformed one.
        internal byte[]? Read()
        {
            if (AtEnd)
            {
                return null;
            }

            SkipBlanks();
            byte[]? argument = position < text.Length && text[position] is '"' or '\'' ? ReadQuoted() : ReadWord();
            SkipBlanks();
            return argument;
        }

        private static bool IsBlank(char character) => character is ' ' or '\t';

        private void SkipBlanks()
        {
            while (position < text.Length && IsBlank(text[position]))
            {
                position++;
            }
        }

        private byte[]? ReadQuoted()
        {
            char quote = text[position++];
            StringBuilder argument = new();
            while (position < text.Length && text[position] != quote)
            {
                if (!TryAppendCharacter(argument))
                {
                    return null;
                }
            }

            // Past the closing quote; curl refuses an unclosed or an empty one.
            return position++ < text.Length && argument.Length > 0 ? Encoding.UTF8.GetBytes(argument.ToString()) : null;
        }

        // A backslash takes the character after it, which must be a quote or a backslash.
        private bool TryAppendCharacter(StringBuilder argument)
        {
            if (text[position] == '\\' && !IsEscapable(++position))
            {
                return false;
            }

            argument.Append(text[position++]);
            return true;
        }

        private bool IsEscapable(int index) => index < text.Length && text[index] is '"' or '\'' or '\\';

        private byte[]? ReadWord()
        {
            bool fromHome = text.AsSpan(position).StartsWith("/~/", StringComparison.Ordinal);
            position += fromHome ? 3 : 0;
            int end = text.IndexOf(' ', position);
            end = end < 0 ? text.Length : end;
            byte[] word = Encoding.UTF8.GetBytes(text[position..end]);
            position = end;
            byte[] start = fromHome ? [.. homeDirectory, .. Slash] : [];
            return word.Length > 0 || fromHome ? [.. start, .. word] : null;
        }
    }
}
