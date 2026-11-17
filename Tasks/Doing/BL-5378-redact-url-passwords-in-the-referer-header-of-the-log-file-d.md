---
id: BL-5378
title: Redact URL passwords in the Referer header of the --log-file diagnostic log
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpExchangeLog.cs, Curl.Original.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-5378 — Redact URL passwords in the Referer header of the --log-file diagnostic log

## Goal

`--log-level verbose --log-file` writes a `Referer` header's URL user-info as `REDACTED:REDACTED`, so a password from `-e` or `-H Referer:` never reaches the diagnostic log.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding EXT-077 (Low, Confirmed: 2 runs; a credential in a log that the docs say never holds one). Area: diagnostic log, Windows.

**Repro.** Any HTTP server on 127.0.0.1:43001 answering `/plain`.
- `curl -sS -m5 --log-level verbose --log-file dl.log -e "http://ru:REFSECRET@ref.example/x" http://127.0.0.1:43001/plain -o NUL; grep -c REFSECRET dl.log`
- Also `-H "Referer: http://hu:HREFSECRET@x/"`.

**Expected.** ADR-0222 rule 7: "Never logged: passwords, -u and URL user-info passwords". The same log already redacts `Location` and `Content-Location`.
**Actual (PORT).** `[verbose] [http] header sent Referer: http://ru:REFSECRET@ref.example/x` (and the `HREFSECRET` form); `grep -c` prints 1.
**Where to start.** `Curl.Original.Protocol.Http.UnitLibrary/HttpExchangeLog.cs:29`, `UrlHeaders = ["Location", "Content-Location"]`: add `Referer`.

## Acceptance criteria

- [ ] New tests in `Curl.Original.Protocol.Http.UnitTests` (a new file) prove a sent `Referer: http://ru:REFSECRET@ref.example/x` is logged as `Referer: http://REDACTED:REDACTED@ref.example/x`, for a Referer from `-e` and for one from `-H`.
- [ ] A test proves a `Referer` without user-info is logged unchanged, and `Location` and `Content-Location` are still redacted.
- [ ] Tests are platform-neutral.
- [ ] Hand check recorded in the Log: `grep -c REFSECRET dl.log` prints 0 and the line reads `Referer: http://REDACTED:REDACTED@ref.example/x`.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: `grep -c REFSECRET dl.log` prints 0 and the line reads `Referer: http://REDACTED:REDACTED@ref.example/x`.
- Related: EXT-078 / BL-5379 (the same kind of leak in `--har`).

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 2a5ecf76 b5e3fd8c c1cf21f9 29df4da9 a0b49a29.
