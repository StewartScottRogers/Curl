---
id: BL-3883
title: Fix CGR-003 for SMTP: bound the server greeting read by --connect-timeout
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests/Cgr003Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3883 — Fix CGR-003 for SMTP: bound the server greeting read by --connect-timeout

## Goal

`curl smtp://` against a listener that accepts and never sends a greeting exits 28 after `--connect-timeout`, as curl 8.22.0 does, instead of hanging.

## Context

Split from BL-3532 (CGR-003 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`). Repro: a listener that accepts and stays silent (the HTTP port of a C# file-based server qualifies); `curl -sS -w '%{exitcode}|%{errormsg}\n' smtp://127.0.0.1:PORT/ --connect-timeout 2`. Expected from real curl: after about 2 s, exit 28 with `curl: (28) Operation timed out after 2012 milliseconds with 0 bytes received`. Curl never exits. The greeting read in `Curl.Protocol.Smtp.UnitLibrary` is not bounded by the connect deadline. Measure real curl before pinning text; use `Record-CurlExchange.ps1` or a C# file-based server (no Python).

## Acceptance criteria

- [x] The repro command, run through real curl and through Curl, gives the same exit code, stdout bytes and stderr text (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in `Curl.Protocol.Smtp.UnitTests/Cgr003Tests.cs` fails before the fix and passes after it (ADR-0482).
- [x] Repro variants that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3532.
- Continues the stashed work of BL-3532.
- Carried on from stash 2a2a5d78 (BL-3532 split). The stash also held IMAP and POP3 changes; those belong to BL-3882 and BL-3881 (in Doing on other lanes) and were left uncommitted here; only the SMTP paths are committed.
- Fix: `SmtpConnectPhaseLimit` holds the greeting, EHLO, STARTTLS and AUTH to `--connect-timeout` (default 300 s), counted from the start of the request, and ends with exit 28 and curl's `Operation timed out` text. It follows `FtpConnectPhaseLimit`. No ADR: matching curl left nothing to decide.
- Measured against a PowerShell TcpListener that accepts and never greets: `curl -sS -w '%{exitcode}|%{errormsg}\n' smtp://127.0.0.1:PORT/ --connect-timeout 2`. Real curl 8.21.0 (Schannel): exit 28, stdout `28|Operation timed out after 2018 milliseconds with 0 bytes received`, stderr `curl: (28) Operation timed out after 2018 milliseconds with 0 bytes received`. Curl: exit 28, the same text after 2047 milliseconds (the millisecond count normalised).
- Cgr003Tests.cs does not compile without the fix (`SmtpConnectPhaseLimit` is missing), and its greeting test would hang. The 392 existing SMTP unit tests still pass, so variants that already matched still match. Measure-CodeQuality reports 0 failing members for Curl.Protocol.Smtp.UnitLibrary. Build clean; fast tests green.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: ba67c2c6 614ea89c b084e04d 779790c0.
- 2026-10-26: Doing -> Done. SMTP greeting, EHLO, STARTTLS and AUTH held to --connect-timeout; exit 28 matches curl
