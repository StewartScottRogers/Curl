---
id: BL-3284
title: Port proto_servers.py's FTP server to the differential runner
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3233]
touches: [Curl.Conformance.Differential.UnitLibrary/Protocols/Core, Curl.Conformance.Differential.UnitTests/Protocols/Core, Curl.Conformance.Differential.UnitLibrary/Protocols/Ftp, Curl.Conformance.Differential.UnitTests/Protocols/Ftp, Curl.Conformance.Differential.IntegrationTests/CliProtocols/Host]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3284 — Port proto_servers.py's FTP server to the differential runner

## Goal

An FTP responder ports `proto_servers.py`'s `Ftp` class byte for byte (port 18200: its replies, `FILES`, the LIST/NLST listings, PASV/EPSV data connections, REST, the STOR/APPE upload log lines), together with the shared pieces the other protocol servers reuse: a CRLF line reader and writer, one shared wire log with its control responder, and the TCP listener host under `CliProtocols/Host`.

## Context

The tester's reference scripts are in `Z:\repos\Curl.difftest\2026-10-22\` (outside the repo; read only, never run or commit Python). Parent: BL-3234, which writes the case files and runs them once these servers exist. Follow BL-3233's HTTP pattern (ADR-0782): responder logic lives in `Curl.Conformance.Differential.UnitLibrary` over a `Stream` (no socket, so it stays at 100% coverage, tested with in-memory streams), one file per server; the listener that wraps it lives under `Curl.Conformance.Differential.IntegrationTests/CliProtocols/Host` (compare `IntegrationTests/Host/DifferentialLoopbackServer.cs`). Every server writes the lines it receives to one shared wire log that the suite's single control port answers (`/reset`, `/log`, as the Python `Ctl` did): cases run one at a time, so one log serves every server and `DifferentialCaseRunner` needs no per-case control port. The passive data connection needs a listener, so give the responder an injected opener for data channels (a stream pair in unit tests, a loopback `TcpListener` in the host).

## Acceptance criteria

- [x] An FTP responder answers every command `Ftp` handles with the same reply bytes and wire log lines, and its unit tests cover each command.
- [x] The shared line reader, wire log, control responder and CliProtocols TCP host exist for BL-3285 to BL-3288 to reuse.
- [x] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [x] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split out of BL-3234 by its lane-3 run on 2026-10-23 (about 12 servers were more than one run).
- Shared pieces for BL-3285 to BL-3288: `Protocols/Core` (ProtocolLineReader, ProtocolLineWriter, ProtocolWireLog, ProtocolControlResponder, IPassiveDataListener/Opener) and `CliProtocols/Host/ProtocolLoopbackHost` (serves a responder plus the control port at port+1). One wire log joined by `
`, as Python Ctl did.
- FTP quirks kept: PASS wrong gives 530 (USER always 331); a REST without a number or a transfer with no PASV/EPSV ends the connection, as the crashed Python handler did; REST follows Python slicing. Loopback test FtpLoopbackHostTests passes; Measure-CodeQuality: 0 failing members, 100/100 coverage.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: bda70747 6f3fdd20 ae951636 5e81fa96.
- 2026-10-23: Doing -> Done. FTP responder, shared line reader/writer/wire log/control responder and CliProtocols TCP host ported and tested
