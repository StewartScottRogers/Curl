---
id: BL-2459
title: Libcurl: curl_multi_strerror, curl_share_strerror, curl_url_strerror and the header-API error text
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2456]
touches: [Libcurl/Misc/ErrorText, Libcurl.UnitTests/Misc/ErrorText]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2459 — Libcurl: curl_multi_strerror, curl_share_strerror, curl_url_strerror and the header-API error text

## Goal

Libcurl has `CurlErrorText.Multi`, `Share`, `Url` and `Header` returning upstream's exact strings for every `CurlMultiCode`, `CurlShareCode`, `CurlUrlCode` and `CurlHeaderCode`. `curl_easy_strerror` waits for the `CurlEasyErrorText` move.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Every member of each code enum has a test row naming upstream's string, and an unknown code answers upstream's fallback text.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Strings pinned byte for byte from upstream test1538 (`Curl.Conformance.UnitTests/UpstreamTestData/test1538.curltest`), fallbacks included: `Unknown error`, `CURLSHcode unknown`, `CURLUcode unknown`.
- Upstream has no strerror for `CURLHcode`; `Header` answers the `CURLHE_*` comments from `header.h` and `CURLHcode unknown` (ADR-0562, decided by Claude under Stewart's delegation).
- Table lookups keep every method at complexity 2; `Measure-CodeQuality.ps1 -Library Libcurl`: 0 failing members. Ratchet file untouched.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. CurlErrorText.Multi, Share, Url and Header answer upstream's strerror strings and fallbacks for every code
