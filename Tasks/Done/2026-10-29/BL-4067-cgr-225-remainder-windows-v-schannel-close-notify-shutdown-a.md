---
id: BL-4067
title: CGR-225 remainder: Windows -v schannel close_notify, shutdown and certificate-store-cache lines
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/TlsMessageEvent.cs, Curl.Output.UnitLibrary/SchannelRenegotiationText.cs, Curl.Output.UnitLibrary/SchannelVerificationText.cs, Curl.Output.UnitTests/SchannelRenegotiationTextTests.cs, Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs, Curl.Networking.UnitLibrary/SslStreamConnection.cs, Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitTests/Cgr225RemainderTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4067 — CGR-225 remainder: Windows -v schannel close_notify, shutdown and certificate-store-cache lines

## Goal

Windows `curl -v` over TLS prints the Schannel lines BL-3754 left out, as curl 8.22.0 does.

## Context

BL-3754 (CGR-225) added `schannel: added N certificate(s) from CA file` and `schannel: connection hostname (H) validated against certificate name (N)` in `SchannelVerificationText`. Still missing, measured with curl 8.22.0 Schannel against `openssl s_server -WWW` (Notes of BL-3754): `schannel: reusing certificate store from cache` and the hostname line again inside each renegotiation block (`SchannelRenegotiationText`, after `schannel: SSL/TLS handshake complete`), `schannel: server close notification received (close_notify)` before the `HTTP 1.0, assume close` line, then after the body `{ [0 bytes data]` and `schannel: server indicated shutdown in a prior call`. The close lines need a read-side event from `SslStreamConnection` (new `ITransferEvents` member with a no-op default, forwarded by every decorator that forwards `ReportTlsTrust`).

## Acceptance criteria

- [x] `curl -sS -v --cacert ca --ssl-no-revoke https://localhost:<p>/index.txt` through Curl prints the close_notify, shutdown, `{ [0 bytes data]` and store-cache lines in curl 8.22.0's order.
- [x] A focused unit test in `Curl.Networking.UnitTests/Cgr225RemainderTests.cs` fails before and passes after.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured curl 8.22.0 (Schannel, debug libcurl) and Curl with `-sS -v --cacert srv.crt --ssl-no-revoke https://localhost:14468/index.txt` against `openssl s_server -WWW`: after the fix Curl prints, in curl's order, the store-cache and hostname lines inside each renegotiation block, `schannel: server close notification received (close_notify)` before `HTTP 1.0, assume close after body`, and after `{ [10 bytes data]` the `server indicated shutdown in a prior call` line and `{ [0 bytes data]` (curl's debug-only `step`/`handshake complete`/`sendrecv_dl` lines are not printed by the release build and are not reproduced).
- Decision (ADR-0996): no new `ITransferEvents` member; the close_notify travels as a `TlsMessageEvent` alert through `ReportTlsMessage`, which all 35 decorators already forward, so none of them needed changing. `ITransferEvents.cs` and `TransferEventInfoText.cs` were left alone and dropped from touches; `TlsMessageEvent.cs`, `SchannelVerificationText.cs`, `SessionTicketRecordDetector.cs`, `SslStreamTlsProvider.cs` and `SchannelRenegotiationTextTests.cs` were added (the only other Doing task, BL-4062, touches Curl.Console only).
- `Cgr225RemainderTests` fails before (no `CloseNotifyFollows`, no `ReportShutdownAfterCloseNotify`) and passes after. Quality gates green for Curl.Networking.UnitLibrary and Curl.Output.UnitLibrary after extracting two helpers. Fast tests green; the change is platform-neutral (the detector runs only for the Schannel build; tests use in-memory records).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: d1fb76cb b6c2f678 3d836d5e.
- 2026-10-29: Doing -> Done. Schannel close_notify, shutdown, 0-bytes and store-cache -v lines match curl 8.22.0
