---
id: BL-2406
title: Libcurl: SetOption for the HTTP options (EasyHttpOptions)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2399]
touches: [Libcurl/Options/Http, Libcurl.UnitTests/Options/Http]
requirement: none
created: 2026-10-15
completed:
---
# BL-2406 — Libcurl: SetOption for the HTTP options (EasyHttpOptions)

## Goal

Every `CURLOPT_*` of `EasyHttpOptions` (headers, user agent, referer, encodings, redirects, `POST*` including `COPYPOSTFIELDS`, `HTTPGET`, `HTTP_VERSION`, cookies, alt-svc, HSTS, HTTP/2 stream options) is settable and in the options table; `MIMEPOST` and `HTTPPOST` are left to the MIME tasks.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [ ] A test per option; `POSTFIELDS` keeps the caller's buffer by reference and `COPYPOSTFIELDS` copies it, each pinned.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [ ] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
