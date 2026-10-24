---
id: BL-3427
title: Run the CLI protocol and proxy differential cases in an integration test and record the report
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3233, BL-3235, BL-3284, BL-3285, BL-3286, BL-3287, BL-3288, BL-3334, BL-3424, BL-3425, BL-3426]
touches: [Curl.Conformance.Differential.IntegrationTests/CliProtocols]
requirement: none
created: 2026-10-23
completed:
---
# BL-3427 — Run the CLI protocol and proxy differential cases in an integration test and record the report

## Goal

An integration test runs all the CLI non-HTTP protocol and proxy differential cases against the C# server ports, and one run's report is recorded.

## Context

Last step of BL-3234. Reports to compare with: `report_proto.txt`, `report_proto2.txt`, `report_proxy.txt` in `Z:\repos\Curl.difftest\2026-10-22\`. One shared wire log and one control port serve the whole suite (cases run one at a time). The host starts BL-3233's `DifferentialLoopbackServer` (tunnel target `{U}`), the TLS server from `Tls/DifferentialTlsEndpoints` and `CliTls/DifferentialTlsLoopbackServer` (`{PKI}`, port 18100), and needs a TcpClient `IProxyTunnelConnector` in `IntegrationTests/CliProtocols`. Responder constructors: `FtpResponder(log, IPassiveDataListenerOpener)`, `HttpProxyResponder(log, IProxyTunnelConnector, requiresAuthentication)`, `TelnetResponder(log, TimeProvider)`, the rest `(log)`.

## Acceptance criteria

- [ ] An Integration test in `Curl.Conformance.Differential.IntegrationTests/CliProtocols` starts every server port and runs every case from BL-3424, BL-3425 and BL-3426.
- [ ] One run's report is recorded under Notes, and each new difference is filed as a task.
- [ ] Projects build clean with -warnaserror and the fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [ ] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split from BL-3234 (over its cost cap) on 2026-10-23.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: ec95dc2b d0cfd46a 05cc481e 5e81fa96.
