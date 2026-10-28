using System.Diagnostics.CodeAnalysis;
using Curl.Output;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// The <see cref="IWriteOutFileOpener" /> the executable uses: it opens each <c>%output{file}</c>
/// target of a <c>-w</c> template on disk, truncating it, or appending to it for
/// <c>%output{&gt;&gt;file}</c>, and shares it for writing, because curl 8.21.0 opens the same
/// file twice in <c>%output{f}A%output{&gt;&gt;f}B</c>. A file it cannot open is refused
/// without throwing, so the output stays where it was, as curl leaves it. Off Windows a name
/// holding an argument byte that is not valid UTF-8 opens under its raw bytes, as curl 8.18.0
/// creates <c>%output{\xff.txt}</c> as FF 2E 74 78 74 (CGR-016, BL-3863, <see cref="RawByteFileName" />).
/// </summary>
/// <param name="writesLineFeedAsCrLf">
/// Whether each opened file is written in text mode, every line feed as CR LF through
/// <see cref="LineFeedToCrLfStream" />, as curl 8.21.0 writes these files on Windows (ADR-0081).
/// </param>
/// <param name="isWindows">Whether this is Windows, where argument bytes are never escaped.</param>
/// <param name="openRaw">Opens a name's NUL-terminated raw bytes.</param>
internal sealed class DiskWriteOutFileOpener(
    bool writesLineFeedAsCrLf,
    bool isWindows,
    Func<byte[], FileWriteMode, UnixFileMode, FileOpenResult> openRaw) : IWriteOutFileOpener
{
    /// <summary>
    /// The permissions a raw-byte file gets before the umask: <c>0666</c>, what curl's
    /// <c>fopen</c> of the file uses.
    /// </summary>
    internal const UnixFileMode CreateMode = DeferredOutputFileStream.CreateMode;

    /// <summary>
    /// Creates the opener this platform uses: raw-byte names open through
    /// <see cref="RawByteFileName.OpenUnderRawBytes" /> off Windows.
    /// </summary>
    /// <param name="writesLineFeedAsCrLf">Whether each opened file is written in text mode.</param>
    public DiskWriteOutFileOpener(bool writesLineFeedAsCrLf)
        : this(writesLineFeedAsCrLf, OperatingSystem.IsWindows(), RawByteFileName.OpenUnderRawBytes)
    {
    }

    /// <inheritdoc />
    public bool TryOpen(string path, bool append, [NotNullWhen(true)] out Stream? stream)
    {
        Stream? file = isWindows || !RawByteFileName.HoldsEscapedByte(path)
            ? OpenThroughFileStream(path, append)
            : openRaw(RawByteFileName.NullTerminatedBytes(path), append ? FileWriteMode.Append : FileWriteMode.Truncate, CreateMode).Content;
        stream = file is null || !writesLineFeedAsCrLf ? file : new LineFeedToCrLfStream(file, ownsInner: true);

        return stream is not null;
    }

    /// <summary>
    /// Tells whether <paramref name="exception" /> is one opening a <see cref="FileStream" />
    /// raises for a file the operating system will not open for writing.
    /// </summary>
    /// <param name="exception">The exception the open threw.</param>
    /// <returns>
    /// <see langword="true" /> for an <see cref="IOException" /> (a missing directory included),
    /// an <see cref="UnauthorizedAccessException" /> (a directory on Windows) or an
    /// <see cref="ArgumentException" /> (an empty name).
    /// </returns>
    internal static bool IsOpenFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException;

    /// <summary>Opens <paramref name="path" /> with a shared <see cref="FileStream" />.</summary>
    /// <param name="path">The file's name.</param>
    /// <param name="append">Whether to append rather than truncate.</param>
    /// <returns>The file, or <see langword="null" /> when it cannot be opened.</returns>
    private static FileStream? OpenThroughFileStream(string path, bool append)
    {
        try
        {
            return new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        }
        catch (Exception exception) when (IsOpenFailure(exception))
        {
            return null;
        }
    }
}
