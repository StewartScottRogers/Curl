---
id: BL-5379
title: Redact URL passwords in Location and Content-Location headers written by --har
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Har.UnitLibrary/HarDocument.cs, Curl.Extension.Har.UnitTests, Documentation/Planning/Decisions/ADR-1146-har-writes-each-option-groups-http-transfers-as-a-har-1-2-archive.md]
requirement: none
created: 2026-11-17
completed:
---
# BL-5379 — Redact URL passwords in Location and Content-Location headers written by --har

## Goal

`--har` writes the user-info of URLs in response `Location` and `Content-Location` headers (and the request `Referer`) as `REDACTED:REDACTED`, so a shared archive carries no password anywhere.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding EXT-078 (Low, Confirmed: 2 runs; a credential in a file meant to be shared). Area: `--har`, Windows. Relates to ADR-1146 (updated by BL-5323), which writes `url` and `redirectURL` user info as `REDACTED:REDACTED`.

**Repro.** A server whose `/redir` answers 302 with `Location: http://u:LOCSECRET8@127.0.0.1:43001/plain` and `Content-Location: http://cu:CLSECRET9@127.0.0.1:43001/x`.
- `curl -sS -m5 --har h6.har -L http://127.0.0.1:43001/redir -o NUL; grep -c 'LOCSECRET8\|CLSECRET9' h6.har`

**Expected.** ADR-1146 redacts `url` and `redirectURL`; the same values should not appear unredacted elsewhere in the archive (the diagnostic log already redacts these headers).
**Actual (PORT).** `redirectURL` is redacted, but `response.headers` holds `Location: http://u:LOCSECRET8@...` and `Content-Location: http://cu:CLSECRET9@...`; `grep -c` prints 2. Exit 0.
**Where to start.** `Curl.Extension.Har.UnitLibrary/HarDocument.cs` ~line 96 (`WriteNameValues(json, "headers", headers)` for the response): apply user-info redaction to `Location`, `Content-Location` and the request `Referer`.

## Acceptance criteria

- [ ] New tests in `Curl.Extension.Har.UnitTests` (a new file) prove a response `Location` and `Content-Location` carrying user-info are written as `REDACTED:REDACTED@...` in `response.headers`, and a request `Referer` likewise.
- [ ] A test proves headers without user-info, and other header names, are written unchanged, and `url` and `redirectURL` stay redacted.
- [ ] Tests are platform-neutral.
- [ ] Hand check recorded in the Log: `grep -c 'LOCSECRET8\|CLSECRET9' h6.har` prints 0.
- [ ] ADR-1146 is updated to name the header fields it redacts.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: `grep -c 'LOCSECRET8\|CLSECRET9' h6.har` prints 0.
- Related: EXT-077 / BL-5378 (the same leak in the diagnostic log's `Referer`).

## Log

- 2026-11-17: Created.
