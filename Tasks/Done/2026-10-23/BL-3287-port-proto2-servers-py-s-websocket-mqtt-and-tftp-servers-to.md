---
id: BL-3287
title: Port proto2_servers.py's WebSocket, MQTT and TFTP servers to the differential runner
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3284]
touches: [Curl.Conformance.Differential.UnitLibrary/Protocols/Binary, Curl.Conformance.Differential.UnitTests/Protocols/Binary, Curl.Conformance.Differential.IntegrationTests/CliProtocols/Host]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3287 — Port proto2_servers.py's WebSocket, MQTT and TFTP servers to the differential runner

## Goal

WebSocket (18440), MQTT (18450) and TFTP (18460, UDP) responders port the matching classes of `proto2_servers.py` byte for byte.

## Context

Reference scripts: `Z:\repos\Curl.difftest\2026-10-22\proto2_servers.py` (read only, never run Python). Parent: BL-3234. Reuse BL-3284's shared wire log and CliProtocols host; responders in the UnitLibrary, one file per server, tested without sockets (ADR-0782). TFTP is UDP: keep its packet logic in the library over byte arrays and add a UDP listener beside BL-3284's TCP host under `CliProtocols/Host`.

## Acceptance criteria

- [x] WebSocket, MQTT and TFTP responders send the same bytes and log the same wire lines as the Python classes, each covered by unit tests.
- [x] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [x] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split out of BL-3234 by its lane-3 run on 2026-10-23.
- WebSocket, MQTT and TFTP responders live in Protocols/Binary; TFTP packet logic sits behind ITftpTransferSocket, and TftpLoopbackHost (UDP plus the TCP control port) is the Integration-side adapter. Python crash paths end the connection, as the handler did.
- Platform-neutral: only byte arrays and loopback sockets; Measure-CodeQuality.ps1 reports 0 failing members for the library.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 6602971e ae951636 5e81fa96.
- 2026-10-23: Doing -> Done. WebSocket, MQTT and TFTP responders ported
