namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Converts an FTP download's line endings as curl does for an ASCII transfer off Windows:
/// each carriage return followed by a line feed becomes the line feed alone, and each lone
/// carriage return becomes a line feed, across reads too; any other download's bytes pass
/// through unchanged. Public so its tests reach the off-Windows answer on Windows.
/// </summary>
/// <remarks>
/// curl 8.22.0 adds its <c>ftp_cw_lc_write</c> writer for an ASCII transfer (a listing,
/// <c>-B</c> or a <c>;type=a</c> URL suffix) only where it is built with
/// <c>CURL_PREFER_LF_LINEENDS</c>, which is everywhere but Windows (CGR-024, BL-3553).
/// </remarks>
/// <param name="useAscii">Whether the transfer is in ASCII mode.</param>
/// <param name="runsOnWindows">Whether Curl runs on Windows.</param>
public sealed class FtpDownloadLineEndings(bool useAscii, bool runsOnWindows)
{
    private bool newlinePending;

    /// <summary>Gets whether this download's line endings are converted: for ASCII off Windows.</summary>
    public bool AreConverted { get; } = useAscii && !runsOnWindows;

    /// <summary>
    /// Converts the next bytes read: a carriage return at their end is held back until the
    /// next byte, or <see cref="Finish" />, shows whether a line feed follows it.
    /// </summary>
    /// <param name="bytes">The bytes read from the data connection.</param>
    /// <returns>The bytes to write.</returns>
    public ReadOnlyMemory<byte> Convert(ReadOnlyMemory<byte> bytes) =>
        AreConverted ? ConvertLineEndings(bytes.Span) : bytes;

    /// <summary>Ends the download: a carriage return held back is written as a line feed.</summary>
    /// <returns>The bytes still to write.</returns>
    public ReadOnlyMemory<byte> Finish()
    {
        byte[] rest = newlinePending ? [(byte)'\n'] : [];
        newlinePending = false;
        return rest;
    }

    private byte[] ConvertLineEndings(ReadOnlySpan<byte> bytes)
    {
        var converted = new List<byte>(bytes.Length + 1);
        foreach (byte value in bytes)
        {
            if (newlinePending && value != (byte)'\n')
            {
                converted.Add((byte)'\n');
            }

            newlinePending = value == (byte)'\r';
            if (!newlinePending)
            {
                converted.Add(value);
            }
        }

        return [.. converted];
    }
}
