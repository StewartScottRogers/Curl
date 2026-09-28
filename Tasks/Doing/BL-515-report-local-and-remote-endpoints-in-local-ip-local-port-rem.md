---
id: BL-515
title: Report local and remote endpoints in %{local_ip}, %{local_port}, %{remote_ip} and %{remote_port} for every scheme
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-515 — Report local and remote endpoints in %{local_ip}, %{local_port}, %{remote_ip} and %{remote_port} for every scheme

## Goal

The four endpoint variables print the connection's addresses for every networked scheme (FTP, DICT, Gopher, Telnet, MQTT, TFTP and every future handler), as curl 8.21.0 does, from one mechanism that does not need each handler to copy them, and an ADR records that mechanism.

## Context

- Conformance audit 2026-09-28, row 42 (Major; sequenced into the opening queue at Stewart's request).
- `ConnectResult` (`Curl.Protocol.Abstractions.UnitLibrary/ConnectResult.cs`) carries `LocalEndPoint`/`RemoteEndPoint`; only the HTTP handler copies them onto `TransferReport`, which `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` reads. The connector is composed in `Curl.Console/CurlTransports.cs`, so a recording decorator there is one candidate; a report field every handler sets is another. Decide, and record it as an ADR marked "Decided by Claude under Stewart's delegation".
- The planned protocol libraries (SMTP, POP3, IMAP, SSH, WebSocket, LDAP, RTSP, SMB) depend on this task so they are written against the settled mechanism.
- FTP has two connections; which one curl reports (control) must be measured. TFTP is UDP.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` (`-Ftp` for FTP): `-w '%{local_ip} %{local_port} %{remote_ip} %{remote_port}'` for `ftp://`, `dict://` and `tftp://` (the last against a closed port is fine for the failure case), and for a failed connect; stdout copied into Notes.
- [ ] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` records the mechanism (number checked unused) and `Documentation/Planning/Decisions/README.md` indexes it.
- [ ] `Curl.Console.UnitTests` tests with fake connectors pin the four values for FTP, DICT and TFTP transfers as measured, and HTTP's values are unchanged.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
