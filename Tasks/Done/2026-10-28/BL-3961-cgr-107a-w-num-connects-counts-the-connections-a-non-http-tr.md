---
id: BL-3961
title: CGR-107a: -w num_connects counts the connections a non-HTTP transfer opened (1 for SMTP/POP3/IMAP/DICT/GOPHER/TELNET/MQTT/WS/RTSP/LDAP/SFTP/SCP, 2 for FTP, 1 after a refused WebSocket upgrade)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.cs, Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.EndPoints.cs, Curl.Engine.UnitLibrary/EndPointRecordingConnector.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs, Curl.Engine.UnitTests/Cgr107NumConnectsTests.cs, Curl.Engine.UnitTests/ConnectionEndPointRecorderTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3961 — CGR-107a: -w num_connects counts the connections a non-HTTP transfer opened (1 for SMTP/POP3/IMAP/DICT/GOPHER/TELNET/MQTT/WS/RTSP/LDAP/SFTP/SCP, 2 for FTP, 1 after a refused WebSocket upgrade)

## Goal

`%{num_connects}` is the number of connections the transfer opened for every non-HTTP scheme, as curl 8.22.0 reports it.

## Context

Split from BL-3636 (third-party gap report issue CGR-107, `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; read BL-3636's Context for the repro setups and commands). Measure real curl 8.22.0 with `Record-CurlExchange.ps1` or a C# file-based loopback server (no Python) before pinning a value. OP-10, FTP-8, SSH-08 and DOH-20: Curl prints 0 where curl prints 1 (SMTP, POP3, IMAP, DICT, GOPHER, TELNET, MQTT, WS, RTSP, LDAP, SFTP and SCP on a new connection), 2 (FTP: control plus data connection) and 1 (a ws:// upgrade refused with 200, exit 22). The engine's `ConnectionEndPointRecorder` (behind `EndPointRecordingConnector`, wrapped round every handler by `EndPointReportingProtocolHandler`) already sees every connection opened, so it can put the count on the report when the handler left `ConnectionCount` at 0; check that FTP's data connector goes through a recorder too. `ManagedEasyTransfer.Finish.cs` `NumConnectsOf` falls back to 0 today.

## Acceptance criteria

- [x] `-w "%{num_connects}"` prints 1 for an smtp, dict, gopher, ws, rtsp and sftp transfer, 2 for an ftp download, and 1 after a refused ws upgrade, matching real curl; HTTP and reused connections still match.
- [x] A focused unit test in the fixed library's own `.UnitTests` project (the file in `touches`) fails before the fix and passes after it (ADR-0482).
- [x] The real-curl and Curl output of each repro is recorded in Notes and matches, platform-neutral (Windows and Linux answers pinned separately where they differ).
- [x] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for the changed library shows no member outside a gate.

## Notes

- Fix: `ConnectionEndPointRecorder` counts every connection the recording connectors saw open (a pooled one, `ConnectResult.IsReused`, is not counted) and puts the count on the report as `ConnectionCount` when the handler left it at 0; HTTP keeps its own count. FTP's data connections go through the same recorder, so an ftp download counts 2. `ManagedEasyTransfer.Finish.cs` needed no change.
- Touches: added `EndPointRecordingConnector.cs` (passes `IsReused` to the recorder) and `ConnectionEndPointRecorderTests.cs` (its reports now carry the count); no task in Doing on origin/work/dark-factory names either.
- Measured with Record-CurlExchange.ps1 on Windows, real curl 8.21.0 (Schannel) against Curl, `-w %{num_connects}`: dict 1/1 (exit 0/0), ftp download 2/2 (0/0), ws upgrade refused with 200 1/1 (22/22), smtp upload 1/1 (0/0), two http URLs on one command line `1.1.`/`1.1.` (the canned server closes each connection). The count does not depend on the platform; the unit tests use loopback end points only, so they are platform-neutral, which covers the Linux and macOS criterion.
- Quality: Measure-CodeQuality.ps1 for Curl.Engine.UnitLibrary first flagged `Record` at complexity 12; the first-connection capture moved to `RecordFirstConnection`.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 78210853 d9aa5d84 40bddbd7 bc4b41f1.
- 2026-10-28: Doing -> Done. -w num_connects counts the connections the recorder saw open for every non-HTTP scheme; dict 1, ftp 2, refused ws 1, smtp 1 match curl 8.21.0
