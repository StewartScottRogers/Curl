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
completed: 2026-10-23
---
# BL-3427 — Run the CLI protocol and proxy differential cases in an integration test and record the report

## Goal

An integration test runs all the CLI non-HTTP protocol and proxy differential cases against the C# server ports, and one run's report is recorded.

## Context

Last step of BL-3234. Reports to compare with: `report_proto.txt`, `report_proto2.txt`, `report_proxy.txt` in `Z:\repos\Curl.difftest\2026-10-22\`. One shared wire log and one control port serve the whole suite (cases run one at a time). The host starts BL-3233's `DifferentialLoopbackServer` (tunnel target `{U}`), the TLS server from `Tls/DifferentialTlsEndpoints` and `CliTls/DifferentialTlsLoopbackServer` (`{PKI}`, port 18100), and needs a TcpClient `IProxyTunnelConnector` in `IntegrationTests/CliProtocols`. Responder constructors: `FtpResponder(log, IPassiveDataListenerOpener)`, `HttpProxyResponder(log, IProxyTunnelConnector, requiresAuthentication)`, `TelnetResponder(log, TimeProvider)`, the rest `(log)`.

## Acceptance criteria

- [x] An Integration test in `Curl.Conformance.Differential.IntegrationTests/CliProtocols` starts every server port and runs every case from BL-3424, BL-3425 and BL-3426.
- [x] One run's report is recorded under Notes, and each new difference is filed as a task.
- [x] Projects build clean with -warnaserror and the fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [x] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split from BL-3234 (over its cost cap) on 2026-10-23.
- `CliProtocols/CliProtocolsDifferentialTests.cs` starts the plain loopback server (18080, `{U}`), the TLS
  servers (`{PKI}`, 18100), and the FTP, SMTP, POP3, IMAP, proxy (HTTP and SOCKS on 18300), DICT, Gopher,
  Telnet, RTSP, WebSocket, MQTT and TFTP ports (18200 to 18460), all writing one `ProtocolWireLog`. It then runs
  every case file embedded under `Cases/CliProtocols`: 214 cases, from BL-3424, BL-3425 and BL-3426.
  `CliProtocols/TcpProxyTunnelConnector.cs` is the TCP `IProxyTunnelConnector`.
- Defaults chosen: the runner reads the shared wire log from the FTP host's control port (18201). Every host's
  control port serves the same log, so any of them would do. The proxy runs with `requiresAuthentication: false`,
  because proxy_server.py was started without `needauth` (no 407 appears in report_proxy.txt). Like
  CliTls, the test is Inconclusive unless `CURL_DIFFERENTIAL_REFERENCE` and `CURL_DIFFERENTIAL_PKI` are set.
- Run on 2026-10-23 against `C:\Program Files\Git\ucrt64\bin\curl.exe` (curl 8.22.0, Schannel, a Debug
  build) with `CURL_DIFFERENTIAL_PKI=Z:\repos\Curl.difftest\2026-10-22\pki`. It passed in 4 m 35 s, and the
  report had 30 `### DIFF` entries:
  ftp-active, ftp-ftps-refused, ftp-trace, ftp-verbose, imap-verbose, pop3-verbose, smtp-verbose,
  dict-define-v, gopher-gophers-refused, mqtt-auth, mqtt-pub-empty, mqtt-pub-file, mqtt-pub, mqtt-sub-multi,
  mqtt-sub-v, mqtt-sub-w, mqtt-sub, rtsp-setup, rtsp-verbose, telnet-basic, telnet-opt, telnet-stdin,
  telnet-timeout, telnet-upload-file, telnet-verbose, tftp-get-v, ws-headers, ws-plain, ws-verbose,
  px-proxy-tunnel-https-v.
- No new differences: every entry is also in the tester's report_proto.txt, report_proto2.txt or
  report_proxy.txt, with the same fields differing in the same way. The tester's reports had 35 entries.
  ftp-size-w, ftp-nlst, ftp-list-custom, ftp-w-vars and pop3-multi now match, so those 5 are gone. Many of
  the remaining entries come from the test setup, not from Curl's behaviour:
  - MQTT client IDs are random.
  - EPRT/PORT use ephemeral ports.
  - TLS ClientHello bytes are random.
  - The reference is a Debug build, so it prints extra verbose lines ("IMAP ... state change", "sendrecv_dl").
  The tester's reports already carried every entry, so no task was filed here.
- Build of Curl.slnx with -warnaserror is clean, and the fast tests pass on Windows. The new test is an
  Integration test with no platform-specific path or text of its own. Without the two variables it is
  Inconclusive, so CI on Linux and macOS is unaffected.
- Measure-CodeQuality.ps1 -Library Curl.Conformance.Differential.UnitLibrary: 0 failing members.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: ec95dc2b d0cfd46a 05cc481e 5e81fa96.
- 2026-10-23: Doing -> Done. An integration test runs all 214 CLI protocol and proxy differential cases against the C# server ports; the 30 differences it reports were all already in the tester's reports
