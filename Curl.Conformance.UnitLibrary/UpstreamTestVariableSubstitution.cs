using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Replaces <c>%NAME</c> variables in a line of a test file with the run's values, and records
/// every upstream variable the run has no value for.
/// </summary>
/// <remarks>
/// At each <c>%</c> the longest name that matches wins, so <c>%CLIENT6IP-NB</c> is not read as
/// <c>%CLIENT6IP</c> followed by <c>-NB</c>; for values that contain no <c>%NAME</c> this gives
/// the same result as the order in which <c>subvariables</c> in upstream's <c>servers.pm</c>
/// replaces them. Names are case-sensitive.
/// A supplied name is replaced even when upstream does not define it (such as <c>SRVDIR</c>,
/// which curl 8.21.0 no longer substitutes). An upstream name with no supplied value is left as
/// written and listed in <see cref="UnknownVariables"/>; anything else after a <c>%</c>, such as
/// the <c>%20</c> of a URL or the <c>%{http_code}</c> of <c>--write-out</c>, is not a variable.
/// Replacement is one pass: a value that itself contains <c>%NAME</c> is not expanded again.
/// </remarks>
internal sealed class UpstreamTestVariableSubstitution
{
    private static readonly string[] PortServers =
    [
        "DICT", "DNS", "FTP", "FTP6", "FTPS", "GOPHER", "GOPHER6", "GOPHERS",
        "HTTP", "HTTP6", "HTTPS", "HTTPS-MTLS", "HTTPSPROXY", "HTTPTLS", "HTTPTLS6",
        "HTTP2", "HTTP2TLS", "HTTP3", "IMAP", "IMAP6", "IMAPS", "MQTT", "MQTTS", "NOLISTEN",
        "POP3", "POP36", "POP3S", "RTSP", "RTSP6", "SMB", "SMBS", "SMTP", "SMTP6", "SMTPS",
        "SOCKS", "SSH", "TELNET", "TFTP", "TFTP6", "PROXY",
    ];

    private static readonly string[] OtherUpstreamNames =
    [
        "HTTPUNIXPATH", "SOCKSUNIXPATH", "CLIENT6IP-NB", "CLIENT6IP", "CLIENTIP", "HOST6IP", "HOSTIP",
        "PERL", "CURL", "LOGDIR", "PWD", "VERSION", "VERNUM", "DATE", "TESTNUMBER", "RESOLVE",
        "FILE_PWD", "SCP_PWD", "SFTP_PWD", "SRCDIR", "CERTDIR", "USER", "DEV_NULL", "LIBTESTS",
        "SSHSRVMD5", "SSHSRVSHA256", "SSHKEYALGO", "FTPTIME2", "H2CVER",
    ];

    private readonly Dictionary<string, string> values;
    private readonly string[] namesLongestFirst;
    private readonly List<string> unknownVariables = [];

    /// <summary>Creates a substitution for one run.</summary>
    /// <param name="variables">The run's values, keyed by name without the <c>%</c>.</param>
    public UpstreamTestVariableSubstitution(IReadOnlyDictionary<string, string> variables)
    {
        values = variables.ToDictionary(
            variable => variable.Key,
            variable => Encoding.Latin1.GetString(Encoding.UTF8.GetBytes(variable.Value)),
            StringComparer.Ordinal);
        namesLongestFirst = UpstreamNames
            .Concat(values.Keys)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(name => name.Length)
            .ToArray();
    }

    /// <summary>Every name curl 8.21.0's <c>subvariables</c> replaces, without the <c>%</c>.</summary>
    public static IEnumerable<string> UpstreamNames =>
        PortServers.Select(server => server + "PORT").Concat(OtherUpstreamNames);

    /// <summary>
    /// Each upstream variable met with no supplied value, with its <c>%</c>, in the order first met.
    /// </summary>
    public IReadOnlyList<string> UnknownVariables => unknownVariables;

    /// <summary>Replaces every variable in one line.</summary>
    /// <param name="line">The line, one character per byte.</param>
    /// <returns>The line with every supplied variable replaced.</returns>
    public string Substitute(string line)
    {
        StringBuilder output = new(line.Length);
        int position = 0;
        while (position < line.Length)
        {
            int percent = line.IndexOf('%', position);
            if (percent < 0)
            {
                output.Append(line, position, line.Length - position);
                break;
            }

            output.Append(line, position, percent - position);
            position = AppendVariableAt(line, percent, output);
        }

        return output.ToString();
    }

    private int AppendVariableAt(string line, int percent, StringBuilder output)
    {
        string? name = namesLongestFirst.FirstOrDefault(candidate => string.CompareOrdinal(line, percent + 1, candidate, 0, candidate.Length) == 0);
        if (name is null)
        {
            output.Append('%');
            return percent + 1;
        }

        if (values.TryGetValue(name, out string? value))
        {
            output.Append(value);
        }
        else
        {
            RecordUnknown("%" + name);
            output.Append('%').Append(name);
        }

        return percent + 1 + name.Length;
    }

    private void RecordUnknown(string variable)
    {
        if (!unknownVariables.Contains(variable))
        {
            unknownVariables.Add(variable);
        }
    }
}
