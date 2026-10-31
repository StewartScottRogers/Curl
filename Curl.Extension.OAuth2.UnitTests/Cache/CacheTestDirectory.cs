namespace Curl.Extension.OAuth2.Cache;

/// <summary>A temporary directory a cache test creates and deletes.</summary>
internal sealed class CacheTestDirectory : IDisposable
{
    public CacheTestDirectory() => System.IO.Directory.CreateDirectory(Path);

    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "curl-oauth2-cache-" + Guid.NewGuid().ToString("N"));

    public string Combine(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose() => System.IO.Directory.Delete(Path, recursive: true);
}

/// <summary>A <see cref="TimeProvider"/> whose time a test sets.</summary>
internal sealed class SetTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
