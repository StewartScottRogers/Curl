using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// How <see cref="FtpProtocolHandler.ConnectOnlyAsync" /> ended: after FTP's connect phase the
/// logged-in control connection, still open; otherwise the failure, with the connection already
/// closed (BL-2705).
/// </summary>
/// <param name="Result">
/// The transfer's result: success with the connect phase's report (<c>%{response_code}</c> and
/// <c>%{ftp_entry_path}</c>) once the login and <c>PWD</c> are done, else the same failure and
/// exit code the tool's transfer ends with.
/// </param>
/// <param name="Connection">
/// The control connection, which the caller now owns and must dispose, or
/// <see langword="null" /> when the connect phase failed.
/// </param>
public sealed record FtpConnectOnlyOutcome(TransferResult Result, IConnection? Connection);
