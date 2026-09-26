namespace Curl.Protocol.File.Fakes;

/// <summary>
/// Which <c>IFileSystem</c> member a recorded call went to.
/// </summary>
public enum FileSystemCallKind
{
    /// <summary>A call to <c>OpenForReadAsync</c>.</summary>
    OpenForRead = 0,

    /// <summary>A call to <c>OpenForWriteAsync</c>.</summary>
    OpenForWrite,
}
