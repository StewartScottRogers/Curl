using System.Text;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Reads the directory a <c>257</c> reply to <c>PWD</c> names, as curl 8.21.0 does for
/// <c>%{ftp_entry_path}</c>: from the first double quote after the code and its space to
/// the next lone one, each doubled quote in between standing for one (BL-514). A control
/// character (bytes 0x00 to 0x1f and 0x7f) inside the quotes is refused, as curl 8.21.0's
/// <c>ftp_pwd_resp</c> refuses it (upstream tests 3217 and 3218, BL-1983).
/// </summary>
internal static class FtpEntryPath
{
    /// <summary>
    /// Reads the directory <paramref name="pwd" /> names.
    /// </summary>
    /// <param name="pwd">The reply to <c>PWD</c>.</param>
    /// <param name="path">
    /// The directory; <see langword="null" /> when the reply is not a <c>257</c>, has no
    /// double quote, or quotes an empty name, all of which curl carries on without.
    /// </param>
    /// <returns>
    /// <see langword="false" /> when the quoted name never ends or holds a control
    /// character, which curl 8.21.0 fails with exit 8 <c>Weird server reply</c> and no
    /// <c>QUIT</c>; otherwise
    /// <see langword="true" />.
    /// </returns>
    public static bool TryRead(FtpReply pwd, out string? path)
    {
        path = null;
        string line = pwd.LastLine;
        int quote = pwd.Code == 257 ? line.IndexOf('"', Math.Min(4, line.Length)) : -1;
        return quote < 0 || TryReadQuoted(line, quote + 1, out path);
    }

    private static bool TryReadQuoted(string line, int start, out string? path)
    {
        StringBuilder name = new();
        for (int index = start; index < line.Length && !IsControl(line[index]); index++)
        {
            if (line[index] != '"')
            {
                name.Append(line[index]);
            }
            else if (IsDoubledQuote(line, index))
            {
                name.Append('"');
                index++;
            }
            else
            {
                path = name.Length == 0 ? null : name.ToString();
                return true;
            }
        }

        path = null;
        return false;
    }

    private static bool IsControl(char character) => character < ' ' || character == '\u007f';

    private static bool IsDoubledQuote(string line, int index) => index + 1 < line.Length && line[index + 1] == '"';
}
