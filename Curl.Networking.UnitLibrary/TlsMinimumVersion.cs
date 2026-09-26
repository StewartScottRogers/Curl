namespace Curl.Networking;

/// <summary>
/// The lowest TLS version the client offers, as curl's <c>--tlsv1.2</c> and
/// <c>--tlsv1.3</c> options set it.
/// </summary>
public enum TlsMinimumVersion
{
    /// <summary>No minimum is set: the operating system chooses the versions offered.</summary>
    SystemDefault = 0,

    /// <summary><c>--tlsv1.2</c>: TLS 1.2 or later.</summary>
    Tls12 = 1,

    /// <summary><c>--tlsv1.3</c>: TLS 1.3 only, the latest version there is.</summary>
    Tls13 = 2,
}
