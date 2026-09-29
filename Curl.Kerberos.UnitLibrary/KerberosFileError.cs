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
    /// The name's type prefix is one this library does not read: anything but <c>FILE:</c>
    /// for a credential cache, and anything but <c>FILE:</c> or <c>WRFILE:</c> for a keytab.
    /// </summary>
    UnsupportedType,
}
