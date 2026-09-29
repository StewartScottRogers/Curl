namespace Curl.Protocol.Smb;

/// <summary>
/// The host triple curl compiles in as <c>CURL_OS</c> and sends as the session setup's
/// native operating system: the triple the first line of <c>curl -V</c> shows on each
/// platform (<c>CurlVersionText</c> in <c>Curl.Cli</c>).
/// </summary>
internal static class SmbCurlOperatingSystem
{
    /// <summary>The Windows reference build's triple.</summary>
    public const string Windows = "x86_64-w64-mingw32";

    /// <summary>The macOS reference build's triple.</summary>
    public const string MacOS = "aarch64-apple-darwin25.0.0";

    /// <summary>The Linux reference build's triple, measured in the session setup curl sends.</summary>
    public const string Linux = "x86_64-pc-linux-gnu";

    /// <summary>Gets the triple for the platform this process runs on.</summary>
    public static string Current => For(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS());

    /// <summary>Picks the triple for a platform.</summary>
    /// <param name="isWindows">Whether the platform is Windows.</param>
    /// <param name="isMacOS">Whether the platform is macOS.</param>
    /// <returns><see cref="Windows" />, <see cref="MacOS" />, else <see cref="Linux" />.</returns>
    public static string For(bool isWindows, bool isMacOS) =>
        isWindows ? Windows : isMacOS ? MacOS : Linux;
}
