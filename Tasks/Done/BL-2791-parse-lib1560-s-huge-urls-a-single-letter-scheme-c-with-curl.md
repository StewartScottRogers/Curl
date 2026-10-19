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
completed: 2026-10-19
---
# BL-2791 — Parse lib1560's huge URLs: a single-letter scheme c:// with CURLU_NON_SUPPORT_SCHEME and 119,998-byte parts

## Goal

lib1560's `huge` group passes: `ManagedCurlUrlHandle.Set(CurlUrlPart.Url, ..., CurlUrlFlags.NonSupportScheme)` answers `Ok` for `c://c:c@c/c?c#c` with any one of the user, password, host, path, query or fragment 119,998 bytes long, reads that part back whole, and answers `BadScheme` when the scheme is the long part.

## Context

Found by BL-2741 (2026-10-19, Windows): with BL-2783 done, upstream case 1560 (`CURL_UPSTREAM_CASE=1560`, `Curl.Conformance.UnitTests` Manual test) fails in `Lib1560Checks.Huge` (`Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.Handles.cs`) with `URL 1: failed to parse [c://aaaa...` through `URL 6`; URL 0 (long scheme, `BadScheme` wanted) passes. Every failing URL has the one-letter scheme `c`, so the likely cause is the drive-letter rule: `CurlUrlScheme.StartsLikeAScheme` (`Curl.Protocol.Abstractions.UnitLibrary/CurlUrlScheme.cs:104`) treats a letter and a colon as a Windows drive, and `UrlRelativeReference.IsAbsolute` (`Curl.Engine.UnitLibrary`) may too. curl 8.22.0's `Curl_is_absolute_url` skips a drive prefix on Windows only when guessing the scheme (`guess_scheme`), so with `CURLU_NON_SUPPORT_SCHEME` alone `c://` is a scheme. Confirm the cause first (a unit test calling `Set` with the URL), check Linux too: if it also fails off Windows the cause is a length limit instead. Measure real curl's answer only if the C source is unclear.

## Acceptance criteria

- [x] A focused test in `Curl.Engine.UnitTests` (or `Curl.Protocol.Abstractions.UnitTests`) pins that `c://c:c@c/c?c#c` with `CURLU_NON_SUPPORT_SCHEME` and a 119,998-byte part parses on every platform and the part reads back whole.
- [x] `CURL_UPSTREAM_CASE=1560` no longer prints any `URL <n>: failed to parse` line.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- Cause confirmed (a failing `ManagedCurlUrlHandleTests` row per part): the drive-letter rule, not a length limit. `CurlUrlParser.ReadScheme` passed `driveLetters` to `CurlUrlScheme.Read` even when the URL API does not guess schemes, so on Windows `c://...` had no scheme. curl 8.22.0's `Curl_is_absolute_url` checks `STARTS_WITH_DRIVE_PREFIX` only under `guess_scheme`; the fix applies the drive rule to the scheme only while guessing (`fileSchemeWithoutSlash` is the URL API's "not guessing"). Off Windows `driveLetters` is false, so Linux and macOS already parsed these; `CurlUrlFileUrlApiTests` pins both drive modes. `UrlRelativeReference.IsAbsolute` has no drive check and needed no change.
- Tests: `Set_OneLetterSchemeWithAHugePartUnderNonSupportScheme_ParsesAndReadsThePartBackWhole` (6 rows, Engine) and `TryParseForUrlApi_OneLetterSchemeWithoutGuessing_IsASchemeNotADrive` / `..._WhileGuessingOnWindows_IsNotAScheme` (Abstractions).
- Case 1560 is still skipped on this branch for "Curl lacks the feature pop3" (BL-2741's stashed `UpstreamCurlPlatform` change). With `imap`, `pop3`, `smtp` added locally (reverted), `CURL_UPSTREAM_CASE=1560` passed with no `failed to parse` line, so BL-2741 can now hold it.
- Fast tests: 39,086 passed, 0 failed. Measure-CodeQuality for Curl.Protocol.Abstractions.UnitLibrary: 0 failing members.

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: e8717ab5 2a6b3b39 4272a6b9.
- 2026-10-19: Doing -> Done. c://c:c@c/c?c#c with CURLU_NON_SUPPORT_SCHEME and a 119,998-byte part parses on Windows; lib1560's huge group passes
