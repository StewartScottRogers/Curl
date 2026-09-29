---
id: BL-929
title: Log WebSocket, RTSP and LDAP transfer steps to the diagnostic log
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-938]
touches: [Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests, Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests, Curl.Protocol.Ldap.UnitLibrary, Curl.Protocol.Ldap.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-929 — Log WebSocket, RTSP and LDAP transfer steps to the diagnostic log

## Goal

The WebSocket, RTSP and LDAP handlers write the diagnostic log (components `ws`, `rtsp`, `ldap`) from `ITransferContext.DiagnosticLog` for each transfer step, forward it through every context they wrap and set it on every `ConnectTarget` they build.

## Context

- The rules are BL-937's ADR: levels (decision 2), components (6), never-logged values (7), and "test `IsEnabled` before building a message" (8). The contract is BL-938's `IDiagnosticLog` and `DiagnosticLogComponents`.
- This task changes no `-v`, `--trace`, standard output or exit-code behaviour: every existing test in the touched test projects passes unmodified.
- Tests use a hand-rolled `RecordingDiagnosticLog : IDiagnosticLog` in each touched test project (no mocking library), recording `(level, component, message)` at a configurable level. Tests are platform-neutral.
- Where: `Curl.Protocol.Ws.UnitLibrary`: `WsProtocolHandler.cs`, `WsFrameReceiver.cs` (upgrade request and `101` reply, frames); `Curl.Protocol.Rtsp.UnitLibrary`: `RtspProtocolHandler.cs`, `RtspReplyReader.cs`, `RtspSessionState.cs` (request method and CSeq, reply status, session ID); `Curl.Protocol.Ldap.UnitLibrary`: `LdapProtocolHandler.cs`, `LdapEntryWriter.cs` (bind, search scope and filter, entries returned). All three implement or wrap `ITransferContext`: forward `DiagnosticLog`.
- What, per level: `error` the failure that ends the transfer with its `CurlExitCode` (a refused upgrade, an RTSP CSeq mismatch, an LDAP result code); `warning` a WebSocket close frame with an unexpected code, an RTSP reply header ignored; `info` upgrade done, RTSP request and reply status, LDAP bind result and entries returned, transfer end with bytes and ms; `verbose` each WebSocket frame (opcode, FIN, length), each RTSP header name, each LDAP message ID and operation.
- Credential-bearing paths: `ws://user:s3cret@host/` (the pre-emptive `Authorization` value), `rtsp://user:s3cret@host/`, and an LDAP simple bind password.

## Acceptance criteria

- [ ] Each of `Curl.Protocol.Ws.UnitTests`, `Curl.Protocol.Rtsp.UnitTests` and `Curl.Protocol.Ldap.UnitTests` pins its `info` milestone lines, one `error` naming its `CurlExitCode`, a wrapper or `ConnectTarget` carrying the incoming `DiagnosticLog`, and the no-secret test above.
- [ ] With the default `NoDiagnosticLog.Instance` every existing test in the touched test projects passes unmodified.
- [ ] A test shows that at `DiagnosticLogLevel.Error` no `info` or `verbose` line is recorded, and one test per credential-bearing path listed in Context shows no recorded message contains the secret.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1 -Library <library>` reports 100% line and branch coverage and no failing member for each library touched (complexity at most 10 per method: put logging in small helpers rather than growing a method).

## Notes

## Log

- 2026-09-29: Created.
