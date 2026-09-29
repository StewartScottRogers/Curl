namespace Curl.Networking;

/// <summary>
/// Why a <see cref="DnsServerResolver" /> lookup found nothing, one value per c-ares status it can
/// end with (<c>ares_status_t</c>). <see cref="DnsLookupFailureText.Describe" /> gives the text
/// curl's c-ares build prints in brackets after <c>Could not resolve host: &lt;host&gt;</c>.
/// </summary>
public enum DnsLookupFailure
{
    /// <summary>The lookup found what it asked for.</summary>
    None,

    /// <summary>The server answered NOERROR with no record of the type asked for (<c>ARES_ENODATA</c>).</summary>
    NoData,

    /// <summary>The server answered FORMERR (<c>ARES_EFORMERR</c>).</summary>
    FormatError,

    /// <summary>The server answered SERVFAIL (<c>ARES_ESERVFAIL</c>), or a code c-ares treats as it.</summary>
    ServerFailure,

    /// <summary>The server answered NXDOMAIN (<c>ARES_ENOTFOUND</c>).</summary>
    NotFound,

    /// <summary>The server answered NOTIMP (<c>ARES_ENOTIMP</c>).</summary>
    NotImplemented,

    /// <summary>The server answered REFUSED (<c>ARES_EREFUSED</c>).</summary>
    Refused,

    /// <summary>The name cannot be put in a query (<c>ARES_EBADNAME</c>).</summary>
    BadName,

    /// <summary>The server's answer did not decode (<c>ARES_EBADRESP</c>).</summary>
    BadReply,

    /// <summary>No server could be reached: a socket could not be opened, bound or connected, or the server refused it (<c>ARES_ECONNREFUSED</c>).</summary>
    Unreachable,

    /// <summary>Every attempt timed out (<c>ARES_ETIMEOUT</c>).</summary>
    Timeout,

    /// <summary>
    /// The <c>--dns-servers</c> list, <c>--dns-ipv4-addr</c> or <c>--dns-ipv6-addr</c> did not parse
    /// (<c>ARES_EBADSTR</c>), which curl reports as exit 43, not exit 6.
    /// </summary>
    BadConfiguration,
}
