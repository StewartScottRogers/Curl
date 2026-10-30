namespace Curl.Extension.WebDavTools.Sync;

/// <summary>The local file system as the sync planner sees it: whether a file exists, and its length and last-write time.</summary>
public interface IWebDavLocalFileSystem
{
    /// <summary>Returns the length and last-write time of the file at <paramref name="path"/>.</summary>
    /// <param name="path">The local file's path.</param>
    /// <returns>The file's length and last-write time, or <see langword="null"/> when no file is there.</returns>
    WebDavLocalFile? FindFile(string path);
}
