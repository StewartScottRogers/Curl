---
id: BL-3882
title: Fix CGR-003 for IMAP: bound the server greeting read by --connect-timeout
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests/Cgr003Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3882 — Fix CGR-003 for IMAP: bound the server greeting read by --connect-timeout

## Goal

`curl imap://` against a listener that accepts and never sends a greeting exits 28 after `--connect-timeout`, as curl 8.22.0 does, instead of hanging.

## Context

Split from BL-3532 (CGR-003 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`). Repro: a listener that accepts and stays silent (the HTTP port of a C# file-based server qualifies); `curl -sS -w '%{exitcode}|%{errormsg}\n' imap://127.0.0.1:PORT/ --connect-timeout 2`. Expected from real curl: after about 2 s, exit 28 with `curl: (28) Operation timed out after 2012 milliseconds with 0 bytes received`. Curl never exits. The greeting read in `Curl.Protocol.Imap.UnitLibrary` is not bounded by the connect deadline. Measure real curl before pinning text; use `Record-CurlExchange.ps1` or a C# file-based server (no Python).

## Acceptance criteria

- [x] The repro command, run through real curl and through Curl, gives the same exit code, stdout bytes and stderr text (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in `Curl.Protocol.Imap.UnitTests/Cgr003Tests.cs` fails before the fix and passes after it (ADR-0482).
- [x] Repro variants that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3532.
- Continues the stashed work of BL-3532.
- Measured (2026-10-26) against a PowerShell `TcpListener` on loopback that accepts and stays
  silent: `curl -sS -w '%{exitcode}|%{errormsg}\n' imap://127.0.0.1:PORT/ --connect-timeout 2`.
  Real curl 8.21.0 Schannel: exit 28, stdout `28|Operation timed out after 2013 milliseconds with 0 bytes received`,
  stderr `curl: (28) Operation timed out after 2013 milliseconds with 0 bytes received`. Curl:
  exit 28, the same text with 2035 milliseconds (the report's normalisation masks the count).
  Before the fix Curl never exited.
- The tests in `Cgr003Tests.cs` drive a manual clock and a silent connection. Before the fix, no
  IMAP path could end with exit 28 at the greeting, so the first test failed (it hung on the
  transfer's token). The existing IMAP tests still pass for servers that greet (the variants that
  already matched).
- Decision recorded in ADR-0829.
- The stash also held POP3 and SMTP work. That belongs to BL-3881 (in Doing on another lane) and
  BL-3883, so this task's commits leave it out (outside `touches`).
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Protocol.Imap.UnitLibrary` reported 0 failing
  members. `dotnet build` is clean and the fast tests are green. The change is platform-neutral:
  a manual clock and no file system or OS text.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: ba67c2c6 9470e243 b084e04d 779790c0.
- 2026-10-26: Doing -> Done. IMAP holds its greeting and login to --connect-timeout and ends with exit 28 as curl does (ADR-0829)
