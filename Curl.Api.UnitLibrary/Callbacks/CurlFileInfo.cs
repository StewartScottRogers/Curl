namespace Curl.Api.Callbacks;

/// <summary>
/// One file of an FTP wildcard listing, handed to a <see cref="ChunkBeginCallback"/>: libcurl's
/// <c>struct curl_fileinfo</c>. Every field the listing did not carry is <see langword="null"/>.
/// </summary>
public sealed record CurlFileInfo
{
    /// <summary><c>filename</c>: the file's name.</summary>
    public required string FileName { get; init; }

    /// <summary><c>filetype</c>: the file's type. Default <see cref="CurlFileType.Unknown"/>.</summary>
    public CurlFileType FileType { get; init; } = CurlFileType.Unknown;

    /// <summary><c>time</c>: the modification time, in seconds since the Unix epoch.</summary>
    public long? Time { get; init; }

    /// <summary><c>perm</c>: the permission bits.</summary>
    public int? Permissions { get; init; }

    /// <summary><c>uid</c>: the owner's user number.</summary>
    public int? UserId { get; init; }

    /// <summary><c>gid</c>: the owner's group number.</summary>
    public int? GroupId { get; init; }

    /// <summary><c>size</c>: the size in bytes.</summary>
    public long? Size { get; init; }

    /// <summary><c>hardlinks</c>: the number of hard links.</summary>
    public long? HardLinks { get; init; }

    /// <summary><c>strings.time</c>: the modification time as the listing wrote it.</summary>
    public string? TimeText { get; init; }

    /// <summary><c>strings.perm</c>: the permissions as the listing wrote them.</summary>
    public string? PermissionsText { get; init; }

    /// <summary><c>strings.user</c>: the owner's user name.</summary>
    public string? User { get; init; }

    /// <summary><c>strings.group</c>: the owner's group name.</summary>
    public string? Group { get; init; }

    /// <summary><c>strings.target</c>: a symbolic link's target.</summary>
    public string? Target { get; init; }
}
