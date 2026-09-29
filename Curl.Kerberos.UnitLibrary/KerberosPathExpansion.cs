using System.Globalization;
using System.Text;

namespace Curl.Kerberos;

/// <summary>
/// Expands the <c>%{token}</c> parameters in a <c>krb5.conf</c> path value as MIT's
/// <c>k5_expand_path_tokens</c> does on Unix: <c>%{uid}</c>, <c>%{euid}</c> and
/// <c>%{USERID}</c> give the user id, <c>%{username}</c> the user name, <c>%{TEMP}</c>
/// <c>TMPDIR</c> or <c>/tmp</c>, <c>%{LIBDIR}</c>, <c>%{BINDIR}</c> and <c>%{SBINDIR}</c>
/// MIT's default installation directories, and <c>%{null}</c> nothing (BL-816).
/// </summary>
/// <param name="userId">The user id; curl never runs setuid, so it is both the real and effective id.</param>
/// <param name="readEnvironmentVariable">Reads an environment variable; <see langword="null" /> when unset.</param>
/// <param name="readUserName">Gives the user's login name.</param>
internal sealed class KerberosPathExpansion(uint userId, Func<string, string?> readEnvironmentVariable, Func<string> readUserName)
{
    /// <summary>The environment variable <c>%{TEMP}</c> reads.</summary>
    public const string TemporaryDirectoryVariable = "TMPDIR";

    private const string TokenStart = "%{";

    /// <summary>Expands every <c>%{token}</c> in <paramref name="value" />, left to right, without re-expanding what a token gives.</summary>
    /// <param name="value">A path value, e.g. <c>FILE:/tmp/krb5cc_%{uid}</c>.</param>
    /// <returns>The value with every token replaced.</returns>
    /// <exception cref="KerberosFileException">
    /// A <c>%{</c> has no closing <c>}</c> or names a token MIT does not know
    /// (<see cref="KerberosFileError.PathTokenInvalid" />, MIT's <c>EINVAL</c>).
    /// </exception>
    public string Expand(string value)
    {
        StringBuilder expanded = new();
        int position = 0;
        int start;
        while ((start = value.IndexOf(TokenStart, position, StringComparison.Ordinal)) >= 0)
        {
            int end = value.IndexOf('}', start);
            if (end < 0)
            {
                throw new KerberosFileException(KerberosFileError.PathTokenInvalid);
            }

            expanded.Append(value, position, start - position).Append(TokenValue(value[(start + TokenStart.Length)..end]));
            position = end + 1;
        }

        return expanded.Append(value, position, value.Length - position).ToString();
    }

    private string TokenValue(string token) =>
        TokenValues().TryGetValue(token, out Func<string>? value)
            ? value()
            : throw new KerberosFileException(KerberosFileError.PathTokenInvalid);

    private Dictionary<string, Func<string>> TokenValues() => new(StringComparer.Ordinal)
    {
        ["uid"] = UserIdText,
        ["euid"] = UserIdText,
        ["USERID"] = UserIdText,
        ["username"] = readUserName,
        ["TEMP"] = () => readEnvironmentVariable(TemporaryDirectoryVariable) ?? "/tmp",
        ["LIBDIR"] = () => "/usr/local/lib",
        ["BINDIR"] = () => "/usr/local/bin",
        ["SBINDIR"] = () => "/usr/local/sbin",
        ["null"] = () => string.Empty,
    };

    private string UserIdText() => userId.ToString(CultureInfo.InvariantCulture);
}
