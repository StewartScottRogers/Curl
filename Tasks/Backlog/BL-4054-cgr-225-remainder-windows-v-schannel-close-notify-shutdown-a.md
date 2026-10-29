---
id: BL-4054
title: CGR-225 remainder: Windows -v schannel close_notify, shutdown and certificate-store-cache lines
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/ITransferEvents.cs, Curl.Output.UnitLibrary/SchannelRenegotiationText.cs, Curl.Output.UnitLibrary/TransferEventInfoText.cs, Curl.Networking.UnitLibrary/SslStreamConnection.cs, Curl.Networking.UnitTests/Cgr225RemainderTests.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4054 — CGR-225 remainder: Windows -v schannel close_notify, shutdown and certificate-store-cache lines

## Goal

Windows `curl -v` over TLS prints the Schannel lines BL-3754 left out, as curl 8.22.0 does.

## Context

BL-3754 (CGR-225) added `schannel: added N certificate(s) from CA file` and `schannel: connection hostname (H) validated against certificate name (N)` in `SchannelVerificationText`. Still missing, measured with curl 8.22.0 Schannel against `openssl s_server -WWW` (Notes of BL-3754): `schannel: reusing certificate store from cache` and the hostname line again inside each renegotiation block (`SchannelRenegotiationText`, after `schannel: SSL/TLS handshake complete`), `schannel: server close notification received (close_notify)` before the `HTTP 1.0, assume close` line, then after the body `{ [0 bytes data]` and `schannel: server indicated shutdown in a prior call`. The close lines need a read-side event from `SslStreamConnection` (new `ITransferEvents` member with a no-op default, forwarded by every decorator that forwards `ReportTlsTrust`).

## Acceptance criteria

- [ ] `curl -sS -v --cacert ca --ssl-no-revoke https://localhost:<p>/index.txt` through Curl prints the close_notify, shutdown, `{ [0 bytes data]` and store-cache lines in curl 8.22.0's order.
- [ ] A focused unit test in `Curl.Networking.UnitTests/Cgr225RemainderTests.cs` fails before and passes after.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
