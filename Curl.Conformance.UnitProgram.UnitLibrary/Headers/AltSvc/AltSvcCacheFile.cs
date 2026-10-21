using Curl.Core.AltSvc;

namespace Curl.Conformance.UnitProgram.Headers.AltSvc;

/// <summary>
/// <c>Curl_altsvc_load</c> as the alt-svc ports call it: the file the case names in its first argument read
/// into a cache, and nothing read, without an error, when the file does not exist, as libcurl does.
/// </summary>
public static class AltSvcCacheFile
{
    /// <summary>The path of the file a case writes for its program: the program's first argument.</summary>
    /// <param name="context">The program's context.</param>
    /// <returns>The file's path.</returns>
    /// <exception cref="ArgumentException">The case gave the program no argument.</exception>
    public static string PathOf(UnitProgramContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Arguments.Count > 0
            ? context.Arguments[0]
            : throw new ArgumentException("An alt-svc unit program needs the alt-svc file's path as its argument.", nameof(context));
    }

    /// <summary>Reads the file at <paramref name="path" /> into <paramref name="cache" />, when it exists.</summary>
    /// <param name="cache">The cache to add the file's entries to.</param>
    /// <param name="path">The alt-svc file.</param>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>A task that completes once the file is read.</returns>
    public static async Task LoadAsync(AltSvcCache cache, string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cache);
        if (File.Exists(path))
        {
            cache.ReadFile(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false));
        }
    }
}
