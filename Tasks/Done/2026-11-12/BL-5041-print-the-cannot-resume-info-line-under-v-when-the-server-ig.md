---
id: BL-5041
title: Print the Cannot resume info line under -v when the server ignores Range
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpDownloadConditions.cs, Curl.Original.Protocol.Http.UnitTests/HttpDownloadConditionsResumeInfoLineTests.cs]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5041 — Print the Cannot resume info line under -v when the server ignores Range

## Goal

With `-v`, a `-C <offset>` request whose server ignores Range prints the info line `* HTTP server does not seem to support byte ranges. Cannot resume.` before the failure, as REF does.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip; REF = curl 8.22.0 Schannel, REF2 = curl 8.18.0 OpenSSL), finding STD-046 (Low, Confirmed).

- **Repro:** a server answers `GET /norange` with `200` and `Content-Length: 1000`, ignoring Range; `curl -sS -v -C 10 http://127.0.0.1:41000/norange -o NUL`
- **Expected (REF and REF2):** after `< Content-Length: 1000`, in order: `* HTTP server does not seem to support byte ranges. Cannot resume.`, `< `, `* closing connection #0`, then `curl: (33) HTTP server does not seem to support byte ranges. Cannot resume.`
- **Actual (PORT):** the same output without the `* HTTP server does not seem ...` info line. Exit code (33) and the final error line match.
- **Where to start:** `Curl.Original.Protocol.Http.UnitLibrary/HttpDownloadConditions.cs` line 184 throws `HttpTransferException(RangeError, ResumeNotSupported)` without first reporting the info line. The `-f` and `--max-filesize` paths do report theirs; follow them.

## Acceptance criteria

- [x] A new `Curl.Original.Protocol.Http.UnitTests/HttpDownloadConditionsResumeInfoLineTests.cs` asserts that the 200-without-Range case reports the info line `HTTP server does not seem to support byte ranges. Cannot resume.` through the transfer-event/verbose channel before throwing, with the failure still mapping to `CurlExitCode` 33.
- [x] The info line appears between `< Content-Length: 1000` and the blank `< ` line, matching REF's order, in a handler-level test; the `curl: (33) ...` final line is unchanged.
- [x] Regression: a `206` response to `-C 10` still resumes with no such line, and the `-f` and `--max-filesize` info lines are unchanged.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text.

- Notes: info line reported in DecideResume before the exit-33 throw; it sits at the same spot as the other http_firstwrite info lines. Quality run: 0 failing members.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: ad8b8899 5683a6c9 1eb3f186 a0b49a29.
- 2026-11-12: Doing -> Done. Resume-not-supported info line now reported before exit 33; tests and quality run green
