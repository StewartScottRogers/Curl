namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// One file of an FTP <c>LIST</c> reply, carrying every field upstream curl 8.22.0's
/// <c>lib/ftplistparser.c</c> fills in libcurl's <c>struct curl_fileinfo</c>. A field the
/// listing did not carry, or whose flag curl leaves unset, is <see langword="null" />. Every
/// text is the listing's bytes, one character per byte (Latin-1), cut at the first NUL byte as
/// curl's C strings are.
/// </summary>
public sealed record FtpFileListEntry
{
    /// <summary><c>filename</c>: the file's name; for a symbolic link, the part before <c> -&gt; </c>.</summary>
    public required string FileName { get; init; }

    /// <summary><c>filetype</c>: the file's type.</summary>
    public required FtpFileType FileType { get; init; }

    /// <summary><c>strings.time</c>: the modification time as the listing wrote it, never parsed.</summary>
    public required string TimeText { get; init; }

    /// <summary><c>perm</c>, set with <c>CURLFINFOFLAG_KNOWN_PERM</c>: the permission bits of a Unix listing.</summary>
    public int? Permissions { get; init; }

    /// <summary><c>strings.perm</c>: a Unix listing's nine permission characters.</summary>
    public string? PermissionsText { get; init; }

    /// <summary><c>hardlinks</c>, set with <c>CURLFINFOFLAG_KNOWN_HLINKCOUNT</c>: a Unix listing's hard link count.</summary>
    public long? HardLinks { get; init; }

    /// <summary><c>strings.user</c>: a Unix listing's owner.</summary>
    public string? User { get; init; }

    /// <summary><c>strings.group</c>: a Unix listing's group.</summary>
    public string? Group { get; init; }

    /// <summary><c>size</c>, set with <c>CURLFINFOFLAG_KNOWN_SIZE</c>: the size in bytes; 0 for a DOS <c>&lt;DIR&gt;</c>.</summary>
    public long? Size { get; init; }

    /// <summary><c>strings.target</c>: a symbolic link's target, the part after the first <c> -&gt; </c>.</summary>
    public string? Target { get; init; }
}
