---
id: BL-2403
title: Libcurl: SetOption for the TLS options (EasyTlsOptions)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2399]
touches: [Libcurl/Options/Tls, Libcurl.UnitTests/Options/Tls]
requirement: none
created: 2026-10-15
completed:
---
# BL-2403 — Libcurl: SetOption for the TLS options (EasyTlsOptions)

## Goal

Every `CURLOPT_*` of `EasyTlsOptions`, including the `_BLOB` forms, `DOH_SSL_VERIFY*` and the `PROXY_SSL*` twins, is settable and in the options table; `SSL_CTX_FUNCTION` is left to the callback task.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [ ] A test per option, blobs set from a byte array, and the `PROXY_*` twin lands in the proxy-TLS field.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [ ] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

## Log

- 2026-10-15: Created.
