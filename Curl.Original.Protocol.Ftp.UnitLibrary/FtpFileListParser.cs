using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Turns an FTP <c>LIST</c> reply, fed in chunks as it arrives, into the file entries a wildcard
/// download picks from, as upstream curl 8.22.0's <c>Curl_ftp_parselist</c> does. The first byte
/// of the reply picks the format for good: a digit means Windows (DOS) style, anything else Unix
/// <c>ls -l</c> style. Each completed entry whose name the selector accepts is kept, except a
/// symbolic link whose target holds another <c> -&gt; </c>. A byte that breaks the format ends
/// the parse with <see cref="CurlExitCode.FtpBadFileList" /> (exit 87), and an entry longer than
/// curl's 10,000-byte buffer with <see cref="CurlExitCode.OutOfMemory" /> (exit 27); every later
/// chunk is ignored, and the entries kept before the failure stay.
/// </summary>
public sealed class FtpFileListParser
{
    // MAX_FTPLIST_BUFFER is 10000 and curl's dynamic buffer keeps one byte of it for the NUL.
    private const int MaxEntryLength = 9999;
    private const string ChainedLinkArrow = " -> ";

    private readonly Func<string, bool> selectsFileName;
    private readonly long hardLinkLimit;
    private readonly List<FtpFileListEntry> entries = [];
    private FtpListLineReader? reader;

    /// <summary>Creates a parser at the start of a reply.</summary>
    /// <param name="selectsFileName">
    /// Tells whether an entry's file name is wanted, as libcurl's <c>CURLOPT_FNMATCH_FUNCTION</c>
    /// (by default <see cref="FtpWildcardPattern.Match" /> against the URL's pattern, as the platform's curl
    /// matches) answers 0;
    /// called once for every completed entry, in the reply's order.
    /// </param>
    /// <param name="runsOnWindows">
    /// Whether the platform's curl is the Windows build, whose C <c>long</c> holds hard link counts
    /// only up to 2³¹ − 1; elsewhere up to 2⁶³ − 1.
    /// </param>
    public FtpFileListParser(Func<string, bool> selectsFileName, bool runsOnWindows)
    {
        ArgumentNullException.ThrowIfNull(selectsFileName);
        this.selectsFileName = selectsFileName;
        hardLinkLimit = runsOnWindows ? int.MaxValue : long.MaxValue;
    }

    /// <summary>The entries kept so far, in the reply's order.</summary>
    public IReadOnlyList<FtpFileListEntry> Entries => entries;

    /// <summary>
    /// Why the parse ended early: <see cref="CurlExitCode.FtpBadFileList" /> or
    /// <see cref="CurlExitCode.OutOfMemory" />; <see langword="null" /> while it has not.
    /// </summary>
    public CurlExitCode? Failure { get; private set; }

    /// <summary>Reads the next chunk of the reply.</summary>
    /// <param name="chunk">The bytes, which may end part-way through an entry.</param>
    public void Read(ReadOnlySpan<byte> chunk)
    {
        foreach (byte c in chunk)
        {
            if (Failure is not null)
            {
                return;
            }

            ReadByte(c);
        }
    }

    private void ReadByte(byte c)
    {
        FtpListLineReader lineReader = reader ??= char.IsAsciiDigit((char)c)
            ? new FtpDosListLineReader()
            : new FtpUnixListLineReader(hardLinkLimit);
        if (lineReader.Line.Count >= MaxEntryLength)
        {
            Failure = CurlExitCode.OutOfMemory;
            return;
        }

        lineReader.Line.Add(c);
        FtpListStep step = lineReader.Read(c);
        if (step == FtpListStep.Malformed)
        {
            Failure = CurlExitCode.FtpBadFileList;
        }
        else if (step == FtpListStep.EntryComplete)
        {
            Keep(lineReader.CompletedEntry());
            lineReader.StartEntry();
        }
    }

    private void Keep(FtpFileListEntry entry)
    {
        bool selected = selectsFileName(entry.FileName);
        bool chainedLink = entry.Target is not null && entry.Target.Contains(ChainedLinkArrow, StringComparison.Ordinal);
        if (selected && !chainedLink)
        {
            entries.Add(entry);
        }
    }
}
