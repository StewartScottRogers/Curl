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
completed:
---
# BL-2459 — Libcurl: curl_multi_strerror, curl_share_strerror, curl_url_strerror and the header-API error text

## Goal

Libcurl has `CurlErrorText.Multi`, `Share`, `Url` and `Header` returning upstream's exact strings for every `CurlMultiCode`, `CurlShareCode`, `CurlUrlCode` and `CurlHeaderCode`. `curl_easy_strerror` waits for the `CurlEasyErrorText` move.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [ ] Every member of each code enum has a test row naming upstream's string, and an unknown code answers upstream's fallback text.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [ ] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
