---
id: BL-4910
title: Complete --ftp-ssl-ccc on the Schannel profile as curl 8.22.0 does instead of exit 81
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Networking.UnitLibrary/SslStreamConnection.cs, Curl.Networking.UnitLibrary/HandBuiltTlsConnection.cs, Curl.Original.Protocol.Ftp.UnitTests/FtpClearCommandChannelTests.cs, Record-CurlExchange.ps1]
requirement: none
created: 2026-11-11
completed:
---
# BL-4910 — Complete --ftp-ssl-ccc on the Schannel profile as curl 8.22.0 does instead of exit 81

## Goal

`--ftp-ssl-ccc` (passive and active mode) exchanges close_notify, continues the control connection in plain text and completes the transfer.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-007 (Medium, confirmed: reproduced at least twice).

- Repro: Explicit-FTPS test server on 18403 (self-signed) supporting AUTH TLS/PBSZ/PROT, serving `f.txt` = `0123456789ABCDEF\n`; on `CCC` it replies `200 CCC ok`, exchanges close_notify and continues in plain text. `curl -sS -k --ssl-reqd --ftp-ssl-ccc ftp://127.0.0.1:18403/f.txt`, and with `--ftp-ssl-ccc-mode active`.
- Expected: REF 8.22.0: stdout `0123456789ABCDEF`, exit 0 in both modes; `-v` shows `> CCC`, `< 200 CCC ok`, `* schannel: shutting down SSL/TLS connection`, `* schannel: server close notification received (close_notify)`, `> PWD`...
- Actual: `curl: (81) Failed to clear the command channel (CCC)`, exit 81 in both modes; connection reset right after `200 CCC ok`.
- Where to start: `FtpSession.ClearControlTlsAsync` (its doc comment claims 81 is right "as it never can on curl's Schannel build") and the Schannel emulation that hard-fails the clear (`clearsTls: false`) in `SslStreamConnection` and `HandBuiltTlsConnection.ClearTlsAsync`. ADR-0280 was measured against 8.21.0; supersede it.

## Acceptance criteria

- [ ] Both repro commands print `0123456789ABCDEF` and exit 0, and the server sees `PWD` in plain text after `200 CCC ok`.
- [ ] `-v` shows the schannel shutdown and close_notify lines as REF does.
- [x] ADR-0280 is superseded by an ADR recording the 8.22.0 measurement.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

- 2026-11-11 (lane 12): Measured real curl 8.22.0 Schannel before changing anything: `C:\Windows\System32\curl.exe` (8.22.0 Windows, Schannel) and Git for Windows' 8.22.0 Schannel both end the review's command with `curl: (81) Failed to clear the command channel (CCC)` in passive and active mode, with the server sending close_notify at once or waiting for curl's first (new `Record-CurlExchange.ps1 -FtpCccAwaitsCloseNotify`, added to `touches` for it; no Doing task names the script), and with `--tls-max 1.2`/`1.3`. curl prints `* schannel: shutting down SSL/TLS connection with 127.0.0.1 port 18403` and never `server close notification received`. Curl's Schannel profile already does exactly that (pinned by `FtpProtocolHandlerClearCommandChannelTests.ExecuteAsync_TlsNotCleared_EndsWithExit81WithoutQuit`), so no product code was changed. The review's exit 0 came from a Python (OpenSSL) server, which this repository cannot run; it was not reproduced. ADR-1241 records the measurement and supersedes ADR-0280's 8.21.0 one, keeping its behaviour. Criteria 1 and 2 contradict the platform's curl as measured, so the task is Blocked for Stewart: close STD-007 as not reproducible, or name a server stack that reproduces exit 0. Commit: `docs(adr)`/`chore(record)` commit with this task file.
- 2026-11-12: The fitness review of 2026-11-12 (commit 2d2ad7f8b) re-reports STD-007 and names the server stack: a Python `ssl` (OpenSSL) server that on `CCC` replies `200 CCC ok` and calls `sock.unwrap()`, a bidirectional close_notify exchange. Against it REF 8.22.0 Schannel exits 0 on all four commands: `curl -sS -k --ssl-reqd --ftp-ssl-ccc ftp://127.0.0.1:44031/f.txt`, the same with `--ftp-ssl-ccc-mode active`, the same with `-T u.txt .../ccc.txt` (upload), and implicit `curl -sS -k --ftp-ssl-ccc ftps://127.0.0.1:44032/f.txt`; the server sees `AUTH SSL; USER; PASS; PBSZ 0; PROT P; CCC; PWD; EPSV; TYPE I; SIZE f.txt; RETR f.txt; QUIT`. Curl exits 81 on all four. So 8.22.0's result depends on how the server answers close_notify: ADR-1241's .NET-server measurement and the review's OpenSSL-server one are both true. Reproduce with an OpenSSL-backed server (WSL `openssl s_server`, or a stand-in whose shutdown matches OpenSSL's `SSL_shutdown` wait for the peer's close_notify), never Python, and make Curl clear the channel whenever the server completes the close_notify exchange, keeping exit 81 where 8.22.0 gives it.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: a2cbc8ec eebf7ab8 7f05f209 a0b49a29.
- 2026-11-11: Doing -> Blocked. Stewart: real curl 8.22.0 Schannel (System32 and Git) exits 81 on --ftp-ssl-ccc in both modes (ADR-1241), contradicting the review's exit 0; close STD-007 as not reproducible, or name a server stack that reproduces it?
- 2026-11-11: Blocked -> Deferred. Decided by Claude under Stewart's delegation: real curl 8.22.0 Schannel (System32 and Git) exits 81 on --ftp-ssl-ccc in both modes (ADR-1241), so Curl already matches the platform curl; the review's exit 0 did not reproduce. Look again only if a later review names a server stack where 8.22.0 completes CCC.
- 2026-11-12: Deferred -> Backlog. Reopened: fitness review 2026-11-12 STD-007 names the server stack (OpenSSL unwrap) where curl 8.22.0 Schannel completes CCC with exit 0.
