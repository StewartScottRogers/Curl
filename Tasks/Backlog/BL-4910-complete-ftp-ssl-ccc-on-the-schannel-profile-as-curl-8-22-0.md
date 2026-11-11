---
id: BL-4910
title: Complete --ftp-ssl-ccc on the Schannel profile as curl 8.22.0 does instead of exit 81
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Networking.UnitLibrary/SslStreamConnection.cs, Curl.Networking.UnitLibrary/HandBuiltTlsConnection.cs, Curl.Original.Protocol.Ftp.UnitTests/FtpClearCommandChannelTests.cs]
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
- [ ] ADR-0280 is superseded by an ADR recording the 8.22.0 measurement.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
