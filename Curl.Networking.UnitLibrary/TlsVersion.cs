namespace Curl.Networking;

/// <summary>
/// A TLS protocol version, as one end of the range the client offers: the minimum curl's
/// <c>-1</c>/<c>--tlsv1</c>, <c>--tlsv1.0</c> … <c>--tlsv1.3</c> and <c>--proxy-tlsv1</c> set, or
/// the ceiling <c>--tls-max</c> sets. The members are in version order, so a lower version
/// compares lower.
/// </summary>
public enum TlsVersion
{
    /// <summary>
    /// Not set: as a minimum, the operating system chooses the lowest version offered; as a
    /// ceiling, there is none.
    /// </summary>
    SystemDefault = 0,

    /// <summary>TLS 1.0: <c>-1</c>/<c>--tlsv1</c>, <c>--tlsv1.0</c>, <c>--tls-max 1.0</c>.</summary>
    Tls10 = 1,

    /// <summary>TLS 1.1: <c>--tlsv1.1</c>, <c>--tls-max 1.1</c>.</summary>
    Tls11 = 2,

    /// <summary>TLS 1.2: <c>--tlsv1.2</c>, <c>--tls-max 1.2</c>.</summary>
    Tls12 = 3,

    /// <summary>TLS 1.3, the latest version there is: <c>--tlsv1.3</c>, <c>--tls-max 1.3</c>.</summary>
    Tls13 = 4,
}
