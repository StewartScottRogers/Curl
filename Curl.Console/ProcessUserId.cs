using System.Globalization;
using System.Text;

namespace Curl.Console;

/// <summary>
/// Gives this process's real user id, the <c>&lt;uid&gt;</c> of MIT's default credential cache
/// <c>FILE:/tmp/krb5cc_&lt;uid&gt;</c>, from the <c>Uid:</c> line of Linux's
/// <c>/proc/self/status</c>, since the BCL has no <c>getuid</c>. Where that file does not
/// exist (Windows, macOS) it gives 0; neither reaches the hand-built route's default cache
/// name, because SSPI and macOS's GSS framework answer there (ADR-0142).
/// </summary>
internal static class ProcessUserId
{
    /// <summary>The Linux file whose <c>Uid:</c> line holds the real, effective, saved and file-system user ids.</summary>
    public const string StatusPath = "/proc/self/status";

    /// <summary>Reads the real user id from <see cref="StatusPath" />.</summary>
    /// <returns>The user id, or 0 when the file or its line cannot be read.</returns>
    public static uint Read() => Parse(new KerberosDiskFileReader().ReadAllBytes(StatusPath));

    /// <summary>Reads the real user id from the bytes of a <c>/proc/&lt;pid&gt;/status</c> file.</summary>
    /// <param name="status">The file's bytes, or <see langword="null" /> when it could not be read.</param>
    /// <returns>The first number on the <c>Uid:</c> line, or 0 when there is none.</returns>
    public static uint Parse(byte[]? status)
    {
        string[] fields = UidLineOf(status is null ? string.Empty : Encoding.ASCII.GetString(status))
            .Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries);
        return fields.Length > 1 && uint.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint userId) ? userId : 0;
    }

    private static string UidLineOf(string status) =>
        status.Split('\n').FirstOrDefault(line => line.StartsWith("Uid:", StringComparison.Ordinal)) ?? string.Empty;
}
