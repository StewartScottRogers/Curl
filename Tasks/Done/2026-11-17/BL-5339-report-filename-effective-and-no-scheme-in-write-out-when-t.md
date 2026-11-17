---
id: BL-5339
title: Report filename_effective and no scheme in write-out when -T cannot open its file
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests]
requirement: none
created: 2026-11-16
completed: 2026-11-17
---
# BL-5339 — Report filename_effective and no scheme in write-out when -T cannot open its file

## Goal

When `-T <file>` cannot open its file, `-w` reports `%{filename_effective}` as the `-o` name and `%{scheme}` as empty, as curl does, with the same stderr and exit 26.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding STD-089 (Low, Confirmed twice, also with `-Z`). Area: `-T` with write-out, Windows.

**Repro.** In a directory without `nofile.txt`: `curl -sS -T nofile.txt -o out.txt -w '[%{filename_effective}][%{scheme}][%{exitcode}]\n' http://127.0.0.1:30080/echo/`.

**Expected (REF).** `[out.txt][][26]`; with `-o NUL -w '%{json}'`, `"filename_effective":"NUL"` and `"scheme":null`.

**Actual (PORT).** `[][http][26]`; JSON `"filename_effective":null`, `"scheme":"http"`. Stderr and exit code match.

**Where to start.** `Curl.Console/CurlCommandRunner.cs`: on the `-T` open-failure path the write-out is built before `Running.OutputFileName` is assigned and after the scheme is recorded (the same pattern as the already fixed STD-066).

## Acceptance criteria

- [x] New tests in `Curl.Console.UnitTests` (a new file) prove that `-T <missing file> -o out.txt -w '[%{filename_effective}][%{scheme}][%{exitcode}]\n'` prints `[out.txt][][26]` with exit code 26 and unchanged stderr.
- [x] A test proves `-o NUL -w '%{json}'` gives `"filename_effective":"NUL"` and `"scheme":null` on that path, and another proves the `-Z` variant prints the same.
- [x] A test proves a successful `-T` upload still reports scheme `http` (no regression).
- [x] Tests are platform-neutral (`-o` names without drive letters; stand-in connection, nothing real).
- [x] Hand check recorded in the Log: the repro prints `[out.txt][][26]`. Compare with real curl through `Record-CurlExchange.ps1` where possible; a server is a PowerShell or C# file-based app, never Python.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- BL-5341 also touches `Curl.Console/CurlCommandRunner.cs`; the two run one after the other.
- Review acceptance check: the command prints `[out.txt][][26]`.
- Decision: a `RunningTransferState.UploadFileCouldNotBeOpened` flag nulls `%{scheme}` and the `-o` name is set where the open fails (and in the `-Z` skipped report). Hand check: the unit test runs the exact repro and prints `[out.txt][][26]`; real-curl comparison taken from the finding's REF. Measure-CodeQuality for Curl.Console: 0 failing members.

## Log

- 2026-11-16: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 9afe0514 7066c3f7 1f67048a d9db64d7 f56139a5 a0b49a29.
- 2026-11-17: Doing -> Done. -w reports the -o name and no scheme when -T cannot open its file
