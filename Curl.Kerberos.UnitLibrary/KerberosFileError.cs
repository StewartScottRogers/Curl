namespace Curl.Kerberos;

/// <summary>Why a credential cache or keytab could not be read.</summary>
public enum KerberosFileError
{
    /// <summary>The file ends inside a field, or a length runs past its end.</summary>
    Truncated,

    /// <summary>
    /// The file does not start with the version this library reads: <c>0x05 0x04</c> for a
    /// credential cache, <c>0x05 0x02</c> for a keytab.
    /// </summary>
    UnknownVersion,

    /// <summary>The file named by the cache or keytab name does not exist.</summary>
    NotFound,

    /// <summary>
    /// The name's type prefix is one this library does not read: anything but <c>FILE:</c>,
    /// <c>DIR:</c> or <c>KCM:</c> for a credential cache, and anything but <c>FILE:</c> or
    /// <c>WRFILE:</c> for a keytab.
    /// </summary>
    UnsupportedType,

    /// <summary>
    /// A <c>DIR:</c> collection's <c>primary</c> file does not start with a line ending in a
    /// newline that names a file starting <c>tkt</c> with no <c>/</c> in it, as MIT's
    /// <c>cc_dir.c</c> requires (<c>KRB5_CC_FORMAT</c>).
    /// </summary>
    DirectoryPrimaryMalformed,

    /// <summary>
    /// No KCM daemon listens on the configured socket, or <c>[libdefaults] kcm_socket</c> is
    /// <c>-</c>, which turns the Unix socket off.
    /// </summary>
    KcmNotRunning,

    /// <summary>
    /// The KCM daemon answered a request with a non-zero status;
    /// <see cref="KerberosFileException.KcmStatus" /> holds it.
    /// </summary>
    KcmFailed,

    /// <summary>
    /// A KCM reply is shorter than its status code, longer than MIT's 10 MiB limit, or not
    /// shaped as its request's reply must be.
    /// </summary>
    KcmReplyMalformed,
}
