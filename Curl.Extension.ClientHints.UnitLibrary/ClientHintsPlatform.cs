using System.Runtime.InteropServices;

namespace Curl.Extension.ClientHints;

/// <summary>
/// Names the platform for <c>Sec-CH-UA-Platform</c> with one of the values the WICG User-Agent Client Hints spec
/// lists (§3.1, "platform brand"): <c>Windows</c>, <c>macOS</c>, <c>Linux</c>, or <c>Unknown</c> for any other.
/// </summary>
internal static class ClientHintsPlatform
{
    /// <summary>Names the platform the predicate reports.</summary>
    /// <param name="isOSPlatform">Reports whether this is a platform, as <see cref="RuntimeInformation.IsOSPlatform" /> does.</param>
    /// <returns>The platform's name.</returns>
    internal static string NameFor(Func<OSPlatform, bool> isOSPlatform) =>
        isOSPlatform(OSPlatform.Windows) ? "Windows"
        : isOSPlatform(OSPlatform.OSX) ? "macOS"
        : isOSPlatform(OSPlatform.Linux) ? "Linux"
        : "Unknown";
}
