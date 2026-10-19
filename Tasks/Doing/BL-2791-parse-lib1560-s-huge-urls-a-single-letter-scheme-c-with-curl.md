---
id: BL-2791
title: Parse lib1560's huge URLs: a single-letter scheme c:// with CURLU_NON_SUPPORT_SCHEME and 119,998-byte parts
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-19
completed:
---
# BL-2791 — Parse lib1560's huge URLs: a single-letter scheme c:// with CURLU_NON_SUPPORT_SCHEME and 119,998-byte parts

## Goal

lib1560's `huge` group passes: `ManagedCurlUrlHandle.Set(CurlUrlPart.Url, ..., CurlUrlFlags.NonSupportScheme)` answers `Ok` for `c://c:c@c/c?c#c` with any one of the user, password, host, path, query or fragment 119,998 bytes long, reads that part back whole, and answers `BadScheme` when the scheme is the long part.

## Context

Found by BL-2741 (2026-10-19, Windows): with BL-2783 done, upstream case 1560 (`CURL_UPSTREAM_CASE=1560`, `Curl.Conformance.UnitTests` Manual test) fails in `Lib1560Checks.Huge` (`Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.Handles.cs`) with `URL 1: failed to parse [c://aaaa...` through `URL 6`; URL 0 (long scheme, `BadScheme` wanted) passes. Every failing URL has the one-letter scheme `c`, so the likely cause is the drive-letter rule: `CurlUrlScheme.StartsLikeAScheme` (`Curl.Protocol.Abstractions.UnitLibrary/CurlUrlScheme.cs:104`) treats a letter and a colon as a Windows drive, and `UrlRelativeReference.IsAbsolute` (`Curl.Engine.UnitLibrary`) may too. curl 8.22.0's `Curl_is_absolute_url` skips a drive prefix on Windows only when guessing the scheme (`guess_scheme`), so with `CURLU_NON_SUPPORT_SCHEME` alone `c://` is a scheme. Confirm the cause first (a unit test calling `Set` with the URL), check Linux too: if it also fails off Windows the cause is a length limit instead. Measure real curl's answer only if the C source is unclear.

## Acceptance criteria

- [ ] A focused test in `Curl.Engine.UnitTests` (or `Curl.Protocol.Abstractions.UnitTests`) pins that `c://c:c@c/c?c#c` with `CURLU_NON_SUPPORT_SCHEME` and a 119,998-byte part parses on every platform and the part reads back whole.
- [ ] `CURL_UPSTREAM_CASE=1560` no longer prints any `URL <n>: failed to parse` line.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: e8717ab5 2a6b3b39 4272a6b9.
