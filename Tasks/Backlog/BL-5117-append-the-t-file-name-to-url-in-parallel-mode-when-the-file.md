---
id: BL-5117
title: Append the -T file name to %{url} in parallel mode when the file cannot be opened
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/ParallelUploadMissingFileUrlTests.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5117 — Append the -T file name to %{url} in parallel mode when the file cannot be opened

## Goal

In parallel mode (`-Z`), a `-T` file that cannot be opened still reports `%{url}` and `%{url_effective}` with the file name appended to the URL, as upstream curl does and as the serial path already does.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding STD-067 (Low, Confirmed twice; the Linux OpenSSL build agrees with Windows curl). Area: `-Z` with `-T` and `-w`.

**Repro.** In a directory without `nofile.txt`: `curl -sS -Z -T nofile.txt http://127.0.0.1:20000/echo/ -o NUL -w "%{url_effective}|%{url}|%{exitcode}\n"`

**Expected (upstream curl).** `http://127.0.0.1:20000/echo/nofile.txt|http://127.0.0.1:20000/echo/nofile.txt|26` and `curl: (26) ...` on stderr.

**Actual.** `http://127.0.0.1:20000/echo/|http://127.0.0.1:20000/echo/|26`. Without `-Z` the port matches upstream.

**Where to start.** The parallel-transfer setup in `Curl.Console/CurlCommandRunner.cs` returns the open error before appending the `-T` file name to the URL; the serial path already appends it. Append first, then open.

## Acceptance criteria

- [ ] New tests in `Curl.Console.UnitTests/ParallelUploadMissingFileUrlTests.cs` prove that with `-Z -T nofile.txt` and a URL ending in `/`, a missing file gives exit 26 and `%{url_effective}` and `%{url}` both end in `/echo/nofile.txt`.
- [ ] A regression test proves the serial (no `-Z`) output for the same arguments is unchanged, and that a URL not ending in `/` is not given an appended name.
- [ ] Tests are platform-neutral (a file name that does not exist in a temporary directory the test creates).
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check, as a hand check: the repro prints `http://127.0.0.1:20000/echo/nofile.txt|http://127.0.0.1:20000/echo/nofile.txt|26`.

## Log

- 2026-11-13: Created.
