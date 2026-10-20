---
id: BL-2865
title: Make upstream case 2108 pass: FTP rejects a NUL byte in a server response line with exit 8
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-19
completed:
---
# BL-2865 — Make upstream case 2108 pass: FTP rejects a NUL byte in a server response line with exit 8

## Goal

Upstream case 2108 passes and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`: an FTP response line holding a NUL byte ends the transfer with exit 8 (`CURLE_WEIRD_SERVER_REPLY`) and no further command.

## Context

Split from BL-2831. test2108's server answers `PASS` with `230 logged\x00 in` (`REPLY PASS 230 logged\x00 in`, read as ftpserver.pl's `eval "qq{...}"`). Expected `<protocol>`: `USER anonymous`, `PASS ftp@example.com`, then nothing; exit 8. Measured 2026-10-19 on Windows: Curl sends an extra `PWD`, carrying on after the NUL. curl 8.22.0's `pingpong.c` / `ftp.c` response reader rejects a response line containing a zero byte (`Curl_pp_readresp`: "response contains null byte" -> `CURLE_WEIRD_SERVER_REPLY`).

First check the harness emits the NUL (`LineProtocolServerCommands` reading `\x00` as qq would); if not, that is harness work in `Curl.Conformance.UnitLibrary` (add it to `touches`). Then the fix belongs in the FTP response reader (`Curl.Protocol.Ftp.UnitLibrary`, see `FtpSession.cs`); check whether a shared pingpong reader serves SMTP/IMAP/POP3 too and whether curl rejects the NUL there as well. Measure real curl's exit code (`Record-CurlExchange.ps1` or a loopback FTP stand-in) before pinning.

## Acceptance criteria

- [ ] Notes record real curl's exit code and commands sent for 2108, and a verdict (a), (b) or (c) as BL-2831 defines them.
- [ ] For (a) or (b), 2108 passes with `$env:CURL_UPSTREAM_CASE='2108'` and is on `PassingUpstreamCases.txt`.
- [ ] A product fix has a focused unit test in `Curl.Protocol.Ftp.UnitTests` that fails without it (ADR-0482).
- [ ] Build clean, fast tests green, the changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-19: Created.
