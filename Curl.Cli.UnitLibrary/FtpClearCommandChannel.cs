namespace Curl.Cli;

/// <summary>
/// Whether FTPS sends <c>CCC</c> after login to drop TLS from the control connection, and who closes the
/// TLS layer when it does, as the last <c>--ftp-ssl-ccc</c>, <c>--no-ftp-ssl-ccc</c> and
/// <c>--ftp-ssl-ccc-mode</c> on the command line chose it.
/// </summary>
public enum FtpClearCommandChannel
{
    /// <summary>No <c>--ftp-ssl-ccc</c> or <c>--ftp-ssl-ccc-mode</c>, or a later <c>--no-ftp-ssl-ccc</c>: never send <c>CCC</c>.</summary>
    Off = 0,

    /// <summary>
    /// <c>--ftp-ssl-ccc</c>, or <c>--ftp-ssl-ccc-mode passive</c> or an unrecognised mode: send <c>CCC</c> and
    /// leave the TLS shutdown to the server.
    /// </summary>
    Passive,

    /// <summary><c>--ftp-ssl-ccc-mode active</c>: send <c>CCC</c> and start the TLS shutdown ourselves.</summary>
    Active,
}
