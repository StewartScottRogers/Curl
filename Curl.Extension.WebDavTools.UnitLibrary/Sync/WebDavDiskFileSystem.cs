namespace Curl.Extension.WebDavTools.Sync;

/// <summary>The <see cref="IWebDavLocalFileSystem"/> that reads the real disk.</summary>
public sealed class WebDavDiskFileSystem : IWebDavLocalFileSystem
{
    /// <inheritdoc/>
    public WebDavLocalFile? FindFile(string path)
    {
        var file = new FileInfo(path);
        return file.Exists ? new WebDavLocalFile(file.Length, new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero)) : null;
    }
}
