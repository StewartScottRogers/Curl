---
id: BL-198
title: Find and read .curlrc and honour -q
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-197]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-198 — Find and read .curlrc and honour -q

## Goal

The default config file is found by curl's per-OS search order through an injected file system and environment, read with BL-197's reader, and skipped when `-q`/`--disable` is first.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item C12. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Search order involves `CURL_HOME`, `XDG_CONFIG_HOME`, `HOME`, `APPDATA` and the executable's directory (https://curl.se/docs/manpage.html#-K).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] The Windows search order is measured on curl 8.21.0 and pinned; the Linux/macOS order follows the manpage and is tested with a fake environment.
- [ ] `-q` as the first argument skips the file; elsewhere it is measured and pinned.
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Plan item: C12 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
