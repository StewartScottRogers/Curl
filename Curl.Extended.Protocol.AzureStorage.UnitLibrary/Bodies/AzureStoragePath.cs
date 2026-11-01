namespace Curl.Extended.Protocol.AzureStorage.Bodies;

/// <summary>One entry of an ADLS Gen2 Path List response's <c>paths</c> array.</summary>
/// <param name="Name">The path, relative to the file system.</param>
/// <param name="IsDirectory">Whether the path is a directory (<c>isDirectory</c>; absent means a file).</param>
/// <param name="ContentLength">The file's size in bytes (<c>contentLength</c>; absent reads as 0).</param>
/// <param name="LastModified">When the path was last written (<c>lastModified</c>), or <see langword="null"/> when absent.</param>
/// <param name="ETag">The path's entity tag (<c>etag</c>), or <see langword="null"/> when absent.</param>
public sealed record AzureStoragePath(string Name, bool IsDirectory, long ContentLength, DateTimeOffset? LastModified, string? ETag);
