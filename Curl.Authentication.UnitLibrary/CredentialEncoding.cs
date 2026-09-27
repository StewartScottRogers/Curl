using System.Text;

namespace Curl.Authentication;

/// <summary>
/// Chooses the encoding that turns a user name, password or token into the bytes the
/// platform's curl sends (ADR-0022).
/// </summary>
public static class CredentialEncoding
{
    /// <summary>
    /// Gets the encoding the platform's curl sends credentials in.
    /// </summary>
    /// <param name="isWindows">
    /// <see langword="true" /> on Windows, where the reference curl receives its arguments
    /// in the system's ANSI code page (Windows-1252 on an English system), unmappable
    /// characters best-fitted; <see langword="false" /> elsewhere, where curl sends the
    /// argument's bytes as given, which is UTF-8.
    /// </param>
    /// <returns>The system ANSI code page on Windows; UTF-8 elsewhere.</returns>
    public static Encoding ForPlatform(bool isWindows) =>
        isWindows ? CodePagesEncodingProvider.Instance.GetEncoding(0)! : Encoding.UTF8;
}
