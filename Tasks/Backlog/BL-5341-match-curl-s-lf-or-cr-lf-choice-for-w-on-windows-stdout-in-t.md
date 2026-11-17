---
id: BL-5341
title: Match curl's LF or CR LF choice for -w on Windows stdout in three early-failure cases
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests]
requirement: none
created: 2026-11-16
completed:
---
# BL-5341 — Match curl's LF or CR LF choice for -w on Windows stdout in three early-failure cases

## Goal

On Windows, `-w` output on stdout uses the same LF or CR LF line endings as curl in three early-failure cases, so the bytes match.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding STD-091 (Low, byte-level stdout difference, Confirmed, each case 2+ times). Area: `-w` on stdout, Windows.

**Background.** curl leaves stdout in text mode (LF becomes CR LF) until it sets up a transfer that writes to stdout, which switches it to binary.

**Repro.**
1. `curl -s -C 100 -d x -w '[W %{exitcode}]\n' http://127.0.0.1:1/ | od -c`. REF `[W 2]\n`, PORT `[W 2]\r\n`.
2. `curl -s -T k5.cfg -w 'E%{exitcode}\n' "http://a%20b/" | od -c` (`k5.cfg` is any existing file). REF `E3\r\n`, PORT `E3\n`.
3. `curl -sS -Z --fail-early http://127.0.0.1:30080/status/404 http://127.0.0.1:30080/hello -f -w 'A[%{http_code}]B\n' -o NUL 2>/dev/null | od -c` (first URL returns 404, second 200). REF `A[404]B\nA[000]B\n`, PORT `A[404]B\r\nA[000]B\n`.

**Expected.** REF's bytes above. **Actual.** PORT's bytes above.

**Where to start.** `Curl.Console/CurlCommandRunner.cs`: `standardOutputSwitchedToBinary` (lines about 2655 and 4190) and `IsStandardOutputBinaryForWriteOut` (about line 3423). With `-Z`, curl sets up all transfers before the first finishes.

## Acceptance criteria

- [ ] New tests in `Curl.Console.UnitTests` (a new file) prove, with a stand-in stdout recording its bytes and the Windows newline mode selected, that case 1 writes `[W 2]\n`, case 2 writes `E3\r\n` and case 3 writes `A[404]B\nA[000]B\n`. They carry `[OSCondition(OperatingSystems.Windows)]` unless the text/binary decision is injectable, in which case they are platform-neutral.
- [ ] A test proves existing cases (a normal successful stdout transfer, `-w` with `-o file`) keep their current line endings.
- [ ] Hand check recorded in the Log: the three repros through `od -c` match REF's bytes. Measure real curl with `Record-CurlExchange.ps1` or direct runs where possible; a server is a PowerShell or C# file-based app, never Python.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- BL-5339 also touches `Curl.Console/CurlCommandRunner.cs`; the two run one after the other.
- Review acceptance check: each of the three commands produces REF's bytes.

## Log

- 2026-11-16: Created.
