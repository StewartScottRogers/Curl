namespace Curl.Protocol.Ssh.Scp;

/// <summary>
/// Builds the command line libssh2 1.11.1 runs with <c>exec</c> to download a file for
/// curl 8.21.0: <c>scp -pf </c> and the path quoted for both Bourne and C shells, as
/// libssh2's <c>shell_quotearg</c> quotes it. Measured 2026-09-29 (BL-574): the path
/// <c>/x/it's!''here</c> is sent as <c>scp -pf '/x/it'"'"'s'\!"''"'here'</c>.
/// </summary>
internal static class ScpCommand
{
    private static readonly byte[] DownloadPrefix = "scp -pf "u8.ToArray();

    // Which quoting the bytes written so far are inside.
    private enum Quoting
    {
        // Outside any quotes: the start, and after an escaped '!'.
        Unquoted,

        // Inside "...", which holds only apostrophes.
        DoubleQuoted,

        // Inside '...', which holds every other byte.
        SingleQuoted,
    }

    /// <summary>
    /// Builds <c>scp -pf &lt;path&gt;</c>, the command that sends one file with its times.
    /// </summary>
    /// <param name="path">The remote path's bytes.</param>
    /// <returns>The command line's bytes.</returns>
    internal static byte[] ForDownload(byte[] path) => [.. DownloadPrefix, .. Quote(path)];

    /// <summary>
    /// Quotes <paramref name="argument" /> as libssh2 does: runs of ordinary bytes in
    /// single quotes, runs of apostrophes in double quotes, and each <c>!</c> escaped with
    /// a backslash outside any quotes, because a C shell expands it even inside quotes.
    /// </summary>
    /// <param name="argument">The bytes to quote.</param>
    /// <returns>The quoted bytes.</returns>
    internal static byte[] Quote(byte[] argument)
    {
        List<byte> quoted = new((argument.Length * 3) + 2);
        Quoting current = Quoting.Unquoted;
        foreach (byte character in argument)
        {
            Quoting next = QuotingFor(character);
            if (next != current || next == Quoting.Unquoted)
            {
                quoted.AddRange(Closing(current));
                quoted.Add(Opening(next));
            }

            quoted.Add(character);
            current = next;
        }

        quoted.AddRange(Closing(current));
        return [.. quoted];
    }

    private static Quoting QuotingFor(byte character) => character switch
    {
        (byte)'\'' => Quoting.DoubleQuoted,
        (byte)'!' => Quoting.Unquoted,
        _ => Quoting.SingleQuoted,
    };

    private static byte[] Closing(Quoting quoting) => quoting switch
    {
        Quoting.DoubleQuoted => [(byte)'"'],
        Quoting.SingleQuoted => [(byte)'\''],
        _ => [],
    };

    // An unquoted '!' opens with the backslash that escapes it.
    private static byte Opening(Quoting quoting) => quoting switch
    {
        Quoting.DoubleQuoted => (byte)'"',
        Quoting.SingleQuoted => (byte)'\'',
        _ => (byte)'\\',
    };
}
