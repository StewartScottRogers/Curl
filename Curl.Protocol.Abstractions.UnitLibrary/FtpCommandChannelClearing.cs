namespace Curl.Protocol.Abstractions;

/// <summary>
/// Whether an FTPS session sends <c>CCC</c> after <c>PROT</c> to clear TLS from its control
/// connection, and which side sends <c>close_notify</c> first when it does, per
/// <c>--ftp-ssl-ccc</c> and <c>--ftp-ssl-ccc-mode</c> (BL-636, ADR-0279).
/// </summary>
public enum FtpCommandChannelClearing
{
    /// <summary>The default: no <c>CCC</c>; the control connection stays TLS.</summary>
    Off = 0,

    /// <summary>
    /// <c>--ftp-ssl-ccc</c> or <c>--ftp-ssl-ccc-mode passive</c>: send <c>CCC</c>, then wait
    /// for the server's <c>close_notify</c> and send none.
    /// </summary>
    Passive,

    /// <summary>
    /// <c>--ftp-ssl-ccc-mode active</c>: send <c>CCC</c>, then send <c>close_notify</c> and
    /// wait for the server's.
    /// </summary>
    Active,
}
