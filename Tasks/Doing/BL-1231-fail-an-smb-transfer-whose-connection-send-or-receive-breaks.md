---
id: BL-1231
title: Fail an SMB transfer whose connection send or receive breaks with curl's exit 55 or 56
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Smb.UnitLibrary, Curl.Protocol.Smb.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1231 — Fail an SMB transfer whose connection send or receive breaks with curl's exit 55 or 56

## Goal

An `smb://` or `smbs://` transfer whose connection write or read throws an `IOException` ends with the `TransferResult` curl 8.21.0 gives it (exit 55 or 56 and curl's message, the bytes moved so far counted) instead of letting the exception escape `SmbProtocolHandler.ExecuteAsync`.

## Context

- Today `Curl.Protocol.Smb.UnitLibrary` catches an `IOException` only around the output write (`SmbFileTransfer.WriteAsync`, exit 23). `SmbSessionEstablisher.SendAsync`, `SmbFileTransfer.ExchangeAsync` (write, flush, then `SmbMessageReader.ReceiveAsync`) and `SmbMessageReader.ReceiveAsync`'s `connection.ReadAsync` are all unguarded, so a reset during negotiation, session setup, tree connect, open, read, write, close or tree disconnect throws out of the handler. A read of zero bytes is already handled (curl keeps polling until `-m`).
- curl 8.21.0, `lib/smb.c` at `curl-8_21_0`: `smb_send` and `smb_send_and_recv` return the transfer's send or receive error, and the callers close the connection with `connclose(conn, "SMB: failed to communicate")` (lines 925 and 1038) and return it: exit 55 (`CURLE_SEND_ERROR`) or 56 (`CURLE_RECV_ERROR`). The message is the socket filter's `failf`: `Send failure: Connection was reset` / `Recv failure: Connection was reset` for a reset. For any other `IOException` use the fallback texts the sibling handlers use, `Failed sending data to the peer` and `Failure when receiving data from the peer` (`lib/strerror.c` lines 177-181).
- Copy the pattern of `Curl.Protocol.Dict.UnitLibrary/DictIoFailures.cs` and `Curl.Protocol.Rtsp.UnitLibrary/RtspIoFailures.cs` (a reset is an `IOException` wrapping `SocketException` `ConnectionReset`); do not reference those libraries. The `-v` connection-end line follows `SmbProtocolHandler`'s existing rule (`closing connection #N` before the session is set up, `shutting down connection #N` after), and the message gets a `-v` line unless it is a fallback text.

## Acceptance criteria

- [ ] Tests in `Curl.Protocol.Smb.UnitTests` with a fake connection that throws on the Nth write pin exit 55 for a failure while sending the negotiate, the session setup, the tree connect and a write request of an upload: `Send failure: Connection was reset` for a reset, `Failed sending data to the peer` otherwise.
- [ ] Tests with a fake connection that throws on the Nth read pin exit 56 while waiting for the negotiate reply and for a read reply of a download: `Recv failure: Connection was reset` for a reset, `Failure when receiving data from the peer` otherwise, with the bytes already written to the output counted.
- [ ] A cancelled token still throws `OperationCanceledException`; every existing SMB test passes unchanged.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Smb.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
