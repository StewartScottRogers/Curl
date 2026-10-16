---
id: BL-2412
title: Libcurl: GetInfo for string and list CURLINFO values, and the dispatch
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2408]
touches: [Libcurl/Info/Strings, Libcurl.UnitTests/Info/Strings, Libcurl/Info/CurlInfo.cs]
requirement: none
created: 2026-10-15
completed:
---
# BL-2412 — Libcurl: GetInfo for string and list CURLINFO values, and the dispatch

## Goal

Libcurl has `CurlInfo` (every `CURLINFO_*` by name and id) and `CurlEasy.GetInfo(CurlInfo)` dispatching by type; this task answers the strings (`EFFECTIVE_URL`, `REDIRECT_URL`, `CONTENT_TYPE`, `PRIMARY_IP`, `LOCAL_IP`, `SCHEME`, `REFERER`, `CAINFO`, `CAPATH`, `PRIVATE`, ...) and lists (`SSL_ENGINES`, `COOKIELIST`, `CERTINFO`), and `TLS_SSL_PTR` answers the Schannel build's shape, documented.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [ ] A test per info reads `TransferInfo` from a fake transfer; an unknown id returns `CurlCode.UnknownOption`.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [ ] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

## Log

- 2026-10-15: Created.
