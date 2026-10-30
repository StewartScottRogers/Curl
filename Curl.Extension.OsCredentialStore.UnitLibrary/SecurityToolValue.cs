using System.Text;

namespace Curl.Extension.OsCredentialStore;

/// <summary>
/// Reads the values macOS <c>security find-generic-password -g</c> prints (ADR-1139 section 3): an attribute as
/// <c>"acct"&lt;blob&gt;="alice"</c> on standard output and the password as <c>password: "secret"</c> on standard
/// error, each either quoted text, <c>0x</c> and the bytes in hexadecimal (then, after spaces, a quoted rendering)
/// when they are not printable, or <c>&lt;NULL&gt;</c> when empty.
/// </summary>
internal static class SecurityToolValue
{
    /// <summary>Finds the first line of <paramref name="output" /> that starts, after spaces, with <paramref name="prefix" /> and reads its value.</summary>
    /// <param name="output">What the tool printed.</param>
    /// <param name="prefix">What precedes the value, such as <c>"acct"&lt;blob&gt;=</c>.</param>
    /// <returns>The value, or <see langword="null" /> when no line has the prefix.</returns>
    internal static string? After(string output, string prefix)
    {
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                return ValueOf(trimmed[prefix.Length..]);
            }
        }

        return null;
    }

    /// <summary>Reads one printed value: quoted text, hexadecimal bytes decoded as UTF-8, or anything else as empty.</summary>
    /// <param name="printed">The value as printed.</param>
    /// <returns>The value.</returns>
    internal static string ValueOf(string printed)
    {
        if (printed.StartsWith("0x", StringComparison.Ordinal))
        {
            var hex = printed[2..].Split(' ')[0];
            return Encoding.UTF8.GetString(Convert.FromHexString(hex));
        }

        return printed.Length >= 2 && printed[0] == '"' && printed[^1] == '"'
            ? printed[1..^1]
            : string.Empty;
    }
}
