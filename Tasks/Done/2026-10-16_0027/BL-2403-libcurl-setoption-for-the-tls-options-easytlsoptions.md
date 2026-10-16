---
id: BL-2403
title: Libcurl: SetOption for the TLS options (EasyTlsOptions)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2399]
touches: [Libcurl/Options/Tls, Libcurl.UnitTests/Options/Tls, Libcurl/CLAUDE.md]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2403 — Libcurl: SetOption for the TLS options (EasyTlsOptions)

## Goal

Every `CURLOPT_*` of `EasyTlsOptions`, including the `_BLOB` forms, `DOH_SSL_VERIFY*` and the `PROXY_SSL*` twins, is settable and in the options table; `SSL_CTX_FUNCTION` is left to the callback task.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] A test per option, blobs set from a byte array, and the `PROXY_*` twin lands in the proxy-TLS field.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Rows in `Libcurl/Options/Tls` (ADR-0580): `TlsPeerOptionSetters` has one method per twinned option and
  two instances (`Origin`, `Proxy`), so each `PROXY_*` twin writes `EasyTlsOptions.Proxy` through the same
  code; `EasyTlsOptionSetters` adds the rows and holds the origin-only and `DOH_SSL_*` setters.
- Decisions: backend-gated options (`CAPATH`, `PINNEDPUBLICKEY`, `SSL_VERIFYSTATUS`, ...) are all stored,
  the same on every platform; `SSLENGINE` with a name, `SSLENGINE_DEFAULT` and `SSL_FALSESTART` answer
  `NotBuiltIn`; blobs are copied. `PROXY_SSL_OPTIONS` is `CURLOT_LONG` in the table, so it takes a number,
  not the `SslOptions` enum.
- Added `Libcurl/CLAUDE.md` to `touches`: the acceptance criteria require it kept true, and no task in
  Doing on `origin/work/dark-factory` names it.
- Measured: `Measure-CodeQuality.ps1 -Library Libcurl` 100% line, 100% branch, 492 members, 0 failing,
  worst CRAP 10. Libcurl.UnitTests 379 passed; full fast run green.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Libcurl SetOption sets every EasyTlsOptions CURLOPT, blobs and PROXY_* twins included
