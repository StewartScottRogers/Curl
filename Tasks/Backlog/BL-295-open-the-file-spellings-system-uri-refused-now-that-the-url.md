---
id: BL-295
title: Open the file:// spellings System.Uri refused, now that the URL is a CurlUrl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-294, BL-293]
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-295 — Open the file:// spellings System.Uri refused, now that the URL is a CurlUrl

## Goal

The `file://` spellings `System.Uri` refused reach the `file` handler and behave as in
curl 8.21.0: `file://C:`, `file:///C:` and `file:///Q:dir/../x` exit 37 quoting `C:`,
`C:` and `/x`, and `file:///C:%2FWindows/win.ini` opens `C:\Windows\win.ini`.

## Context

- ADR-0010 (Accepted) has the case table. After BL-294 these parse; `FileUrlPath`
  should read them from `CurlUrl` rather than re-parsing `OriginalString` where
  `CurlUrl` already gives the part.
- `Curl.Protocol.File.UnitTests` skips `file://C:` because `Uri` refused it; unskip it.
- `file://user:pass@localhost/x` and `file://ab:/x` must still exit 3.
- Measure each case with curl 8.21.0 (`/mingw64/bin/curl`) and record the command and
  bytes under Notes before pinning.

## Acceptance criteria

- [ ] Tests cover each spelling above with the measured exit code and error text, and the `file://C:` skip is gone.
- [ ] A test in `Curl.Console.UnitTests` runs `file://C:` through `CurlCommandRunner` and gets exit 37, not 3.
- [ ] `dotnet build` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.File`.

## Notes

- Filed by BL-010 when ADR-0010 was accepted.

## Log

- 2026-09-26: Created.
