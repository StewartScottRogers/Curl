namespace Libcurl.Misc.Printf;

/// <summary>
/// The C platform facts <c>lib/mprintf.c</c> is compiled against: on Windows a C <c>long</c> is 32
/// bits and the <c>I</c>, <c>I32</c> and <c>I64</c> length modifiers are understood; elsewhere a
/// <c>long</c> is 64 bits and <c>I</c> is not a modifier.
/// </summary>
/// <param name="IsWindows">Whether to format as libcurl's Windows build does.</param>
internal sealed record PrintfPlatform(bool IsWindows)
{
    /// <summary>The platform this process runs on.</summary>
    public static PrintfPlatform Current { get; } = new(OperatingSystem.IsWindows());
}
