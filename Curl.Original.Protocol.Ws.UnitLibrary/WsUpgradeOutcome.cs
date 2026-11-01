using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ws;

/// <summary>
/// How <see cref="WsProtocolHandler.ConnectAndUpgradeOnlyAsync" /> ended: on a <c>101</c> the
/// upgraded connection, still open, and the bytes that came with the reply head; otherwise the
/// failure, with the connection already closed (BL-2471).
/// </summary>
/// <param name="Result">
/// The transfer's result: success with the upgrade's report (<c>%{http_code}</c> 101) when the
/// upgrade was accepted, else the same failure and exit code the tool's transfer ends with.
/// </param>
/// <param name="Connection">
/// The upgraded connection, which the caller now owns and must dispose, or <see langword="null" />
/// when the upgrade failed.
/// </param>
/// <param name="AlreadyReceived">
/// The bytes received after the reply head, the start of the first frames, not yet decoded; empty
/// when none came or the upgrade failed.
/// </param>
public sealed record WsUpgradeOutcome(TransferResult Result, IConnection? Connection, ReadOnlyMemory<byte> AlreadyReceived);
