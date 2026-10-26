---
id: BL-3881
title: Fix CGR-003 for POP3: bound the server greeting read by --connect-timeout
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests/Cgr003Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3881 — Fix CGR-003 for POP3: bound the server greeting read by --connect-timeout

## Goal

`curl pop3://` against a listener that accepts and never sends a greeting exits 28 after `--connect-timeout`, as curl 8.22.0 does, instead of hanging.

## Context

Split from BL-3532 (CGR-003 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`). Repro: a listener that accepts and stays silent (the HTTP port of a C# file-based server qualifies); `curl -sS -w '%{exitcode}|%{errormsg}\n' pop3://127.0.0.1:PORT/ --connect-timeout 2`. Expected from real curl: after about 2 s, exit 28 with `curl: (28) Operation timed out after 2012 milliseconds with 0 bytes received`. Curl never exits. The greeting read in `Curl.Protocol.Pop3.UnitLibrary` is not bounded by the connect deadline. Measure real curl before pinning text; use `Record-CurlExchange.ps1` or a C# file-based server (no Python).

## Acceptance criteria

- [x] The repro command, run through real curl and through Curl, gives the same exit code, stdout bytes and stderr text (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in `Curl.Protocol.Pop3.UnitTests/Cgr003Tests.cs` fails before the fix and passes after it (ADR-0482).
- [x] Repro variants that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3532.
- Continues the stashed work of BL-3532.
- Repro, against a PowerShell `TcpListener` on loopback that never accepts or sends a byte (the OS completes the TCP handshake from the backlog): `curl.exe -sS -w '%{exitcode}|%{errormsg}\n' pop3://127.0.0.1:PORT/ --connect-timeout 2`. Real curl 8.21.0 Schannel: exit 28 after about 2.4 s, stderr `curl: (28) Operation timed out after 2008 milliseconds with 0 bytes received`, stdout `28|Operation timed out after 2008 milliseconds with 0 bytes received`. Curl (`dotnet run --project Curl.Console`): exit 28, stderr `curl: (28) Operation timed out after 2036 milliseconds with 0 bytes received`, stdout `28|Operation timed out after 2036 milliseconds with 0 bytes received`. They match once the milliseconds are normalised. Before the fix, Curl never exited.
- The fix: `Pop3ConnectPhaseLimit` holds the greeting, `CAPA`, `STLS` and the login to `--connect-timeout` (300 s, curl's `DEFAULT_CONNECT_TIMEOUT`, when it is not given or is 0), counted from the start of the request as libcurl does. The control channel's token can be swapped for the phase's token while the session opens.
- `Cgr003Tests` fails before the fix: without the limit, the no-greeting test's transfer never ends after the clock is advanced. After the fix the POP3 unit tests pass (387 passed, 5 skipped). The tests use a manual clock and fake connections, so they behave the same on every platform. That is what the criterion about CI on Linux and macOS needs.
- Variant: against a refused port (`pop3://127.0.0.1:1/`) real curl on Windows gives `(28) Connection timed out after 2039 milliseconds`, because Windows retries a refused SYN. Curl exits 7 at once. This was already so before the fix, it comes from the TCP connect rather than POP3, and the change does not touch it. Variants that already matched still match: a listener that greets, and a stall after the session is open, which is not held to the limit (both have tests).
- The IMAP and SMTP half of the stashed BL-3532 work is left uncommitted for BL-3882 and BL-3883, which own those libraries.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Protocol.Pop3.UnitLibrary` found 0 failing members. `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: ba67c2c6 fcaa74d5 b084e04d 779790c0.
- 2026-10-26: Doing -> Done. pop3:// against a server that never greets exits 28 at --connect-timeout, as curl does
