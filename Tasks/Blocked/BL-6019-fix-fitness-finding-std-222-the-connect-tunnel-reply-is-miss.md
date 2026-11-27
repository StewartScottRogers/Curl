---
id: BL-6019
title: Fix fitness finding STD-222: the CONNECT tunnel reply is missing from %{size_header} and %{num_headers} for FTP, FTPS, SFTP, SCP and DICT
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.ProxyConnect.cs, Curl.Networking.UnitLibrary/TcpConnector.HttpProxyTunnel.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6019 — Fix fitness finding STD-222: the CONNECT tunnel reply is missing from %{size_header} and %{num_headers} for FTP, FTPS, SFTP, SCP and DICT

## Goal

STD-222 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: the CONNECT tunnel reply is missing from %{size_header} and %{num_headers} for FTP, FTPS, SFTP, SCP and DICT. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed (Windows and Linux; HTTP and HTTPS proxies) · **Area:** write-out over `--proxytunnel`, non-HTTP protocols. Next to last round's STD-212 fix.
- **Servers:** `gogost/gost:3.3.3` as HTTP proxy (`-L "http://:8080"`, published on `127.0.0.1:25481`) and HTTPS proxy (`-L "http+tls://:8443"`, on `25443`); its CONNECT reply carries one header line. Targets on the same Docker network: vsftpd (`curlreview/vsftpd:1`, user `test`/`test`), pure-ftpd (`stilliard/pure-ftpd:bookworm-1.0.50`, `bob`/`bobpw`), `atmoz/sftp` (`test`/`test`).
- **Repro** (`W="%{http_connect}|%{response_code}|%{num_connects}|%{size_header}|%{num_headers}\n"`):
  - `curl -sS -x http://127.0.0.1:25481 -p -u test:test -o /dev/null -w "$W" ftp://<vsftpd>/hello.txt`
  - `curl -sS -x http://127.0.0.1:25481 -p -k -u test:test -o /dev/null -w "$W" sftp://<atmoz>/upload/`
  - `curl -sS -m 5 -x http://127.0.0.1:25481 -p -o /dev/null -w "$W" dict://<vsftpd>:21/x`
- **Expected (REF 8.22.0 and REF2):** vsftpd FTP `200|226|2|124|1`; pure-ftpd `200|226|2|124|2`; SFTP `200|000|1|62|1`; DICT `200|000|1|62|1`. The CONNECT reply's bytes are added to `size_header` and its header lines to `num_headers`; the same through the HTTPS proxy.
- **Actual (.NET, both platforms):** vsftpd `200|226|2|0|1`; pure-ftpd `200|226|2|0|1`; SFTP `200|000|1|0|0`; DICT `200|000|1|0|0`. TELNET and GOPHER timeouts get the right `size_header` but `num_headers` 0 (REF 1). HTTP targets through the same tunnel match.
- **Likely location:** the STD-212 fix carries only the status code (`Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.ProxyConnect.cs`, `RecordProxyConnect(connect.ProxyConnectResponseCode)`); `ConnectResult.ProxyConnectHeaderBytes` and `ConnectReplyHeadersStored` (`Curl.Networking.UnitLibrary/TcpConnector.cs`) are not carried into the transfer report for non-HTTP handlers.
- **Acceptance check:** the SFTP repro prints `200|000|1|62|1`; the vsftpd FTP repro prints `200|226|2|124|1`.

## Acceptance criteria

- [ ] the SFTP repro prints `200|000|1|62|1`; the vsftpd FTP repro prints `200|226|2|124|1`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
- 2026-11-26: Backlog -> Doing. Criteria at claim: d90091a4 69116afe 3d836d5e.
- 2026-11-26: Doing -> Blocked. Blocked at shift start instead of refusing the shift: BL-6019 is in Doing and held by no lane, no parked branch factory/BL-6019-lane-* exists, and lane branches factory/lane-1, factory/lane-12 have commits beyond the shift branch, or git could not say; which one is its work cannot be told. Kept: factory/lane-1 is kept as factory/BL-6019-lane-12-20261126-174800; factory/lane-12 is kept as factory/BL-6019-lane-12-20261126-174800. Claude (an interactive session) finds the task's work among these, moves it to Backlog naming it, or Backlog with no work.
