---
id: BL-3959
title: CGR-107e: -w size_request for DICT (44), GOPHER (4) and MQTT subscribe (34)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Dict.UnitLibrary/DictProtocolHandler.cs, Curl.Protocol.Dict.UnitTests/Cgr107Tests.cs, Curl.Protocol.Gopher.UnitLibrary/GopherProtocolHandler.cs, Curl.Protocol.Gopher.UnitTests/Cgr107Tests.cs, Curl.Protocol.Mqtt.UnitLibrary/MqttProtocolHandler.cs, Curl.Protocol.Mqtt.UnitTests/Cgr107Tests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3959 — CGR-107e: -w size_request for DICT (44), GOPHER (4) and MQTT subscribe (34)

## Goal

DICT, GOPHER and MQTT transfers report `%{size_request}` as curl 8.22.0 does.

## Context

Split from BL-3636 (third-party gap report issue CGR-107, `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; read BL-3636's Context for the repro setups and commands). Measure real curl 8.22.0 with `Record-CurlExchange.ps1` or a C# file-based loopback server (no Python) before pinning a value. OP-11: curl prints 44 for DICT (`CLIENT ...`, `DEFINE ! word`, `QUIT`), 4 for a GOPHER fetch and 34 for an MQTT subscribe; Curl prints 0 for all three.

## Acceptance criteria

- [ ] Each repro prints the same `size_request` as real curl.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project (the file in `touches`) fails before the fix and passes after it (ADR-0482).
- [ ] The real-curl and Curl output of each repro is recorded in Notes and matches, platform-neutral (Windows and Linux answers pinned separately where they differ).
- [ ] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for the changed library shows no member outside a gate.

## Notes

## Log

- 2026-10-28: Created.
