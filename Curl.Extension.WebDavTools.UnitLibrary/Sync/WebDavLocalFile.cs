namespace Curl.Extension.WebDavTools.Sync;

/// <summary>A local file's length and last-write time.</summary>
/// <param name="Length">The file's length in bytes.</param>
/// <param name="LastWriteTime">The file's last-write time.</param>
public sealed record WebDavLocalFile(long Length, DateTimeOffset LastWriteTime);
