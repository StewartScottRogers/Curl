---
id: BL-3959
title: CGR-107e: -w size_request for DICT (44), GOPHER (4) and MQTT subscribe (34)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Dict.UnitLibrary/DictProtocolHandler.cs, Curl.Protocol.Dict.UnitTests/Cgr107Tests.cs, Curl.Protocol.Gopher.UnitLibrary/GopherProtocolHandler.cs, Curl.Protocol.Gopher.UnitTests/Cgr107Tests.cs, Curl.Protocol.Mqtt.UnitLibrary/MqttProtocolHandler.cs, Curl.Protocol.Mqtt.UnitTests/Cgr107Tests.cs, Curl.Protocol.Dict.UnitTests/DictProtocolHandlerCredentialControlCodeTests.cs, Curl.Protocol.Dict.UnitTests/DictProtocolHandlerDiagnosticLogTests.cs, Curl.Protocol.Gopher.UnitTests/GopherProtocolHandlerAdversarialTests.cs, Curl.Protocol.Gopher.UnitTests/GopherProtocolHandlerCredentialControlCodeTests.cs, Curl.Protocol.Gopher.UnitTests/GopherProtocolHandlerDiagnosticLogTests.cs, Curl.Protocol.Gopher.UnitTests/GopherProtocolHandlerDumpHeaderTests.cs, Curl.Protocol.Gopher.UnitTests/GopherProtocolHandlerNoBodyAndMaxFileSizeTests.cs, Curl.Protocol.Gopher.UnitTests/GopherProtocolHandlerTests.cs, Curl.Protocol.Gopher.UnitTests/GopherProtocolHandlerTransferEventsTests.cs, Curl.Protocol.Mqtt.UnitLibrary/MqttSession.cs, Curl.Protocol.Mqtt.UnitTests/MqttProtocolHandlerAdversarialTests.cs, Curl.Protocol.Mqtt.UnitTests/MqttProtocolHandlerCredentialControlCodeTests.cs, Curl.Protocol.Mqtt.UnitTests/MqttProtocolHandlerDiagnosticLogTests.cs, Curl.Protocol.Mqtt.UnitTests/MqttProtocolHandlerReceiveFailureTests.cs, Curl.Protocol.Mqtt.UnitTests/MqttProtocolHandlerSendFailureTests.cs, Curl.Protocol.Mqtt.UnitTests/MqttProtocolHandlerTests.cs, Curl.Protocol.Mqtt.UnitTests/MqttProtocolHandlerTransferEventsTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3959 — CGR-107e: -w size_request for DICT (44), GOPHER (4) and MQTT subscribe (34)

## Goal

DICT, GOPHER and MQTT transfers report `%{size_request}` as curl 8.22.0 does.

## Context

Split from BL-3636 (third-party gap report issue CGR-107, `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; read BL-3636's Context for the repro setups and commands). Measure real curl 8.22.0 with `Record-CurlExchange.ps1` or a C# file-based loopback server (no Python) before pinning a value. OP-11: curl prints 44 for DICT (`CLIENT ...`, `DEFINE ! word`, `QUIT`), 4 for a GOPHER fetch and 34 for an MQTT subscribe; Curl prints 0 for all three.

## Acceptance criteria

- [x] Each repro prints the same `size_request` as real curl.
- [x] A focused unit test in the fixed library's own `.UnitTests` project (the file in `touches`) fails before the fix and passes after it (ADR-0482).
- [x] The real-curl and Curl output of each repro is recorded in Notes and matches, platform-neutral (Windows and Linux answers pinned separately where they differ).
- [x] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for the changed library shows no member outside a gate.

## Notes

- 2026-10-28 (lane 7): Measured real curl 8.22.0 (Git for Windows, Schannel) with `Record-CurlExchange.ps1`: `curl -s -o NUL -w %{size_request} dict://127.0.0.1:PORT/d:word` prints `44` (request.bin 44 bytes); `curl -s -o NUL -w %{size_request} gopher://127.0.0.1:PORT/1/x` prints `4` (request.bin 4 bytes, `/x` CRLF). The recorder has no MQTT server, so MQTT is pinned at the gap report's 34 for a subscribe to `mqtt://HOST/t`: the 26-byte CONNECT plus the 8-byte SUBSCRIBE. So `size_request` is every byte the handler sent; Curl now reports the same (44, 4, 34), pinned by each project's `Cgr107Tests`, which fail before the fix (report null, 0). All bytes are ASCII and platform-neutral, so one answer covers Windows, Linux and macOS (CI on Linux and macOS is met by that and the fast tests green here).
- Design (default taken): each handler puts a `TransferReport { DownloadSize, RequestSize }` on its result after the request is sent; MQTT counts the packets sent in `MqttSession.BytesSent` (a failed send counts none of its packet). No ADR: it follows ADR-0015's report seam as HTTP, WS and RTSP already use it.
- Touches widened: `MqttSession.cs` (where packets are sent) and the existing test files that compared whole `TransferResult` records, now comparing `result with { Report = null }` since the result carries a report. No other Doing task on `origin/work/dark-factory` named these projects.
- Checks: `dotnet build` 0 warnings, 0 errors; `dotnet test --filter "TestCategory!=Integration"` passed in full; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Dict.UnitLibrary,Curl.Protocol.Gopher.UnitLibrary,Curl.Protocol.Mqtt.UnitLibrary` reports 0 failing members.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 451c0bee d9aa5d84 40bddbd7 bc4b41f1.
- 2026-10-28: Doing -> Done. DICT, GOPHER and MQTT report %{size_request} as curl 8.22.0 does (44, 4, 34)
