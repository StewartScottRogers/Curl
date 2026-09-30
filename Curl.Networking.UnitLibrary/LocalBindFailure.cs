namespace Curl.Networking;

/// <summary>
/// Why a connection's local end could not be bound to what <see cref="LocalBinding" /> asks, as
/// libcurl's <c>bindlocal</c> returns it; the connect moves on to the next address either way,
/// and the last address's failure decides the exit code.
/// </summary>
public enum LocalBindFailure
{
    /// <summary>
    /// No interface or host of that name, or no port of the range free: <c>CURLE_INTERFACE_FAILED</c>,
    /// exit 45 <c>Failed binding local connection end</c>.
    /// </summary>
    InterfaceFailed,

    /// <summary>
    /// The address found is of the other family than the address dialled: libcurl's
    /// <c>CURLE_UNSUPPORTED_PROTOCOL</c> for that address, which ends as exit 7 when it is the last.
    /// </summary>
    AddressFamilyMismatch,

    /// <summary>
    /// An <c>ifhost!</c> interface part longer than <see cref="LocalBinding.LongestDeviceName" />:
    /// <c>CURLE_BAD_FUNCTION_ARGUMENT</c>, exit 43 <c>A libcurl function was given a bad argument</c>.
    /// </summary>
    BadArgument,
}
