using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Imap;

/// <summary>
/// How <see cref="ImapProtocolHandler.ConnectOnlyAsync" /> ended: after the connect phase the
/// connection, still open and logged in when there was a login; otherwise the failure, with the
/// connection already closed (BL-2706).
/// </summary>
/// <param name="Result">
/// The transfer's result: success with no bytes transferred when the connect phase completed,
/// else the same failure and exit code a normal transfer's connect phase ends with.
/// </param>
/// <param name="Connection">
/// The open connection, the <c>STARTTLS</c>-secured one when the session was upgraded, which the
/// caller now owns and must dispose; <see langword="null" /> when the connect phase failed.
/// </param>
public sealed record ImapConnectOutcome(TransferResult Result, IConnection? Connection);
