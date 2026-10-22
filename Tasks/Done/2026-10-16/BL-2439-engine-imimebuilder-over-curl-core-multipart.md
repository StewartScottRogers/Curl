---
id: BL-2439
title: Engine: IMimeBuilder over Curl.Core Multipart
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2456]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2439 — Engine: IMimeBuilder over Curl.Core Multipart

## Goal

The engine's `IMimeBuilder` and `IMimePartBuilder` build `curl_mime` trees (name, filename, type, encoder, data, file data, data callback, subparts, headers) over `Curl.Core`'s `Multipart`, and a perform with `MIMEPOST` sends them byte-identical to curl.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Engine tests compare the body bytes with those the tool builds for `-F` for the same parts.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Plan: `ManagedCurlEngine.CreateMime` gives `ManagedMimeBuilder` / `ManagedMimePartBuilder` (Curl.Engine) building the seam's `EasyMimeBody`; a perform from the options with `EasyHttpOptions.MimePost` builds the body through the new `MimePostForm`, reusing the tool's own path (`MultipartFormPartMapping` + `Curl.Core`'s `MultipartFormBodyBuilder`), so the bytes match `-F` by construction. Mail URLs (smtp/imap) upload the MIME message as the runner does.
- The seam in `Curl.Api.UnitLibrary` already had `IMimeBuilder`, `IMimePartBuilder` and the `EasyMime*` records; no seam change was needed, so the plan's phase-3 guess holds.
- Decisions (sensible defaults, Stewart's delegation): engine text parts are built in UTF-8 (libcurl sends bytes as given; the tool's platform encoding is a command-line concern); `SetFileData` on a missing path still sets the file and its base name and answers `CURLE_READ_ERROR`, so the transfer fails with exit 26 and `Failed to open/read local data from file/application`, as the tool's `-F name=@missing` does; the form is built before the cookie/alt-svc/HSTS setup, so a build failure sends and writes nothing.
- Follow-up filed: BL-2470: `curl_mime_data_cb` parts and non-UTF-8 `curl_mime_data` bytes need `Curl.Core`'s `MultipartFormPart` to carry bytes and a read callback (outside this task's touches); until then a callback part makes the perform throw `NotSupportedException` from `MultipartFormPartMapping.FromMimeBody`.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` 100% line, 100% branch, 914 members, 0 failing (worst CRAP 10) after splitting `PerformFromOptionsOverCachesAsync` (complexity 14) into `PerformBuildingMimePostAsync`, `PerformDisposingFormAsync` and `PerformParsedAsync`. Full fast run green; `PassingUpstreamCases.txt` untouched.
## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. IMimeBuilder builds curl_mime trees and a MIMEPOST perform sends the same bytes as -F; callback and non-UTF-8 parts left to BL-2470
