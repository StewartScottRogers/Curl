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
completed: 2026-09-28
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

- [x] Measured first with `Record-CurlExchange.ps1` (`-Ftp` for FTP): `-w '%{local_ip} %{local_port} %{remote_ip} %{remote_port}'` for `ftp://`, `dict://` and `tftp://` (the last against a closed port is fine for the failure case), and for a failed connect; stdout copied into Notes.
- [x] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` records the mechanism (number checked unused) and `Documentation/Planning/Decisions/README.md` indexes it.
- [x] `Curl.Console.UnitTests` tests with fake connectors pin the four values for FTP, DICT and TFTP transfers as measured, and HTTP's values are unchanged.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

Measured 2026-09-28 on the reference curl 8.21.0 (Git for Windows mingw64) with `Record-CurlExchange.ps1`, `-s -w '%{local_ip} %{local_port} %{remote_ip} %{remote_port}\n'` (stdout copied verbatim):

- `-Ftp -FtpData hello`, `ftp://127.0.0.1:47515/f.txt` (EPSV data port 64512): `hello127.0.0.1 64513 127.0.0.1 47515`, exit 0 - the control connection.
- `-Response '220 hi\r\n'`, `dict://127.0.0.1:47516/d:word`: `220 hi` then `127.0.0.1 64515 127.0.0.1 47516`, exit 0.
- `-NoServer`, `tftp://127.0.0.1:47517/f` (closed port): ` 0 127.0.0.1 47517`, exit 7.
- `-NoServer` against a PowerShell `UdpClient` answering the RRQ from a new port (62775) with one short DATA block `hi`, `tftp://127.0.0.1:47519/f`: `hi 0 127.0.0.1 47519`, exit 0 - the URL's server, not the reply port; no local end, local port 0.
- `-NoServer`, `ftp://127.0.0.1:47518/f` (closed port, failed connect): ` -1  -1`, exit 7.

Decision (ADR-0119): connector decorators in `Curl.Console` record the first connection each transfer opens, and a handler decorator puts it on the report when the handler reported neither end point; HTTP keeps its own. `Curl.Output` prints `%{local_port}` 0 for a remote end point without a local one. A report is created for a result without one, carrying `BytesTransferred` as `DownloadSize` so `%{size_download}` does not change.

Quality: `Measure-CodeQuality.ps1 -Library Curl.Console*,Curl.Output*,Curl.Protocol.Abstractions*` reported 100% line and branch, 0 failing members, worst CRAP 10. Pass the libraries as an array from PowerShell; under `-File` a comma list arrives as one string and matches nothing.


## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. %{local_ip}, %{local_port}, %{remote_ip} and %{remote_port} print the first connection's ends for every scheme (FTP control, DICT, TFTP server) through connector decorators in the composition (ADR-0119)
