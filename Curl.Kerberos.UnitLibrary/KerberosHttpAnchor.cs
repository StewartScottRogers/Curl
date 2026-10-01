namespace Curl.Kerberos;

/// <summary>
/// One value of a realm's <c>http_anchors</c> relation in <c>krb5.conf</c>: where the roots an
/// MS-KKDCP proxy's certificate must lead to are kept, read as MIT's <c>load_anchor</c> reads it
/// (ADR-0300). <c>FILE:</c> names a PEM file, <c>DIR:</c> a directory of PEM files, and
/// <c>ENV:</c> an environment variable holding another such value. The prefixes are
/// case-sensitive, as MIT compares them with <c>strncmp</c>.
/// </summary>
/// <param name="Kind">Which kind of location <paramref name="Location" /> is.</param>
/// <param name="Location">The path or environment variable name, after its prefix.</param>
public sealed record KerberosHttpAnchor(KerberosHttpAnchorKind Kind, string Location)
{
    /// <summary>Reads one <c>http_anchors</c> value.</summary>
    /// <param name="written">The value as written, e.g. <c>FILE:/etc/kdcproxy-ca.pem</c>.</param>
    /// <returns>The anchor, or <see langword="null" /> when the value has none of MIT's three prefixes.</returns>
    public static KerberosHttpAnchor? Parse(string written) =>
        written.StartsWith("FILE:", StringComparison.Ordinal) ? new(KerberosHttpAnchorKind.File, written[5..])
        : written.StartsWith("DIR:", StringComparison.Ordinal) ? new(KerberosHttpAnchorKind.Directory, written[4..])
        : written.StartsWith("ENV:", StringComparison.Ordinal) ? new(KerberosHttpAnchorKind.EnvironmentVariable, written[4..])
        : null;
}
