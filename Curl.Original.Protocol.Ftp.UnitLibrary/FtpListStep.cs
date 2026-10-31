namespace Curl.Original.Protocol.Ftp;

/// <summary>What one byte of an FTP <c>LIST</c> reply did to the line being read.</summary>
internal enum FtpListStep
{
    /// <summary>The byte was taken; the line goes on.</summary>
    Continue,

    /// <summary>The byte breaks the listing format: <c>CURLE_FTP_BAD_FILE_LIST</c>.</summary>
    Malformed,

    /// <summary>The byte ended a file entry, which <see cref="FtpListLineReader.CompletedEntry" /> now returns.</summary>
    EntryComplete,
}
