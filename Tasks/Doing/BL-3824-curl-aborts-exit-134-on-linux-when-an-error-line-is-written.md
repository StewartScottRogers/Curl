---
id: BL-3824
title: Curl aborts (exit 134) on Linux when an error line is written to a closed standard error (2>&-)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/Program.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3824 — Curl aborts (exit 134) on Linux when an error line is written to a closed standard error (2>&-)

## Goal

On Linux, Curl run with standard error closed (`2>&-`) exits with the transfer's own code, as curl does, instead of aborting.

## Context

Found by BL-3537 (CGR-008) on 2026-10-26 in WSL: `curl -sS http://127.0.0.1:21199/ 2>&-` exits 7 with curl 8.18.0 (OpenSSL); Curl (a self-contained linux-x64 publish of `Curl.Console`) prints `Aborted` and exits 134, both before and after BL-3537's `StandardErrorFailureIgnoringStream` (ADR-0815), which ignores every `IOException` from standard error. `-s` alone (no line written) and `--version 2>&-` exit normally, so the abort comes from writing to descriptor 2. Likely cause: the .NET runtime reuses the free descriptor 2 for one of its own (curl's `main_checkfds` fills 0, 1 and 2 with `/dev/null` at start for the same reason), so `Console.OpenStandardError` dups that descriptor and the error line lands in a runtime pipe or eventfd. Start in `Curl.Console/Program.cs`: find out what descriptor 2 is when `Main` starts (`/proc/self/fd/2`, `fstat`) and write standard error to `Stream.Null` when it is not the standard error the shell gave.

## Acceptance criteria

- [ ] On Linux, `curl -sS http://127.0.0.1:21199/ 2>&-; echo $?` gives 7 through Curl as through real curl, with the commands and both results recorded in Notes.
- [ ] A focused unit test in `Curl.Console.UnitTests` fails before the fix and passes after it (ADR-0482).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 8a2d5fed def504f7 779790c0.
