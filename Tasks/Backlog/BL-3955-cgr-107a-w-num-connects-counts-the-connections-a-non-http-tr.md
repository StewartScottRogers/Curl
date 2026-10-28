---
id: BL-3955
title: CGR-107a: -w num_connects counts the connections a non-HTTP transfer opened (1 for SMTP/POP3/IMAP/DICT/GOPHER/TELNET/MQTT/WS/RTSP/LDAP/SFTP/SCP, 2 for FTP, 1 after a refused WebSocket upgrade)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.cs, Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.EndPoints.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs, Curl.Engine.UnitTests/Cgr107NumConnectsTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3955 — CGR-107a: -w num_connects counts the connections a non-HTTP transfer opened (1 for SMTP/POP3/IMAP/DICT/GOPHER/TELNET/MQTT/WS/RTSP/LDAP/SFTP/SCP, 2 for FTP, 1 after a refused WebSocket upgrade)

## Goal

`%{num_connects}` is the number of connections the transfer opened for every non-HTTP scheme, as curl 8.22.0 reports it.

## Context

Split from BL-3636 (third-party gap report issue CGR-107, `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; read BL-3636's Context for the repro setups and commands). Measure real curl 8.22.0 with `Record-CurlExchange.ps1` or a C# file-based loopback server (no Python) before pinning a value. OP-10, FTP-8, SSH-08 and DOH-20: Curl prints 0 where curl prints 1 (SMTP, POP3, IMAP, DICT, GOPHER, TELNET, MQTT, WS, RTSP, LDAP, SFTP and SCP on a new connection), 2 (FTP: control plus data connection) and 1 (a ws:// upgrade refused with 200, exit 22). The engine's `ConnectionEndPointRecorder` (behind `EndPointRecordingConnector`, wrapped round every handler by `EndPointReportingProtocolHandler`) already sees every connection opened, so it can put the count on the report when the handler left `ConnectionCount` at 0; check that FTP's data connector goes through a recorder too. `ManagedEasyTransfer.Finish.cs` `NumConnectsOf` falls back to 0 today.

## Acceptance criteria

- [ ] `-w "%{num_connects}"` prints 1 for an smtp, dict, gopher, ws, rtsp and sftp transfer, 2 for an ftp download, and 1 after a refused ws upgrade, matching real curl; HTTP and reused connections still match.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project (the file in `touches`) fails before the fix and passes after it (ADR-0482).
- [ ] The real-curl and Curl output of each repro is recorded in Notes and matches, platform-neutral (Windows and Linux answers pinned separately where they differ).
- [ ] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for the changed library shows no member outside a gate.

## Notes

## Log

- 2026-10-28: Created.
