---
id: BL-2437
title: Engine: ICurlUrlHandle set, append-query, dup and clear
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2436]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2437 — Engine: ICurlUrlHandle set, append-query, dup and clear

## Goal

The engine's `ICurlUrlHandle` sets every part with `CURLU_*` flags (`APPENDQUERY`, `URLENCODE`, `NON_SUPPORT_SCHEME`, `GUESS_SCHEME`, `ALLOW_SPACE`, ...), duplicates, and clears a part with a null value.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] A test per flag and part; bad input returns the exact `CurlUrlCode`.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Decided in ADR-0602 (Decided by Claude under Stewart's delegation): `UrlPartSetter` sets and clears
  each part as curl 8.22.0's `curl_url_set` (scheme, port, host checks with upstream's codes;
  `URLENCODE`, `APPENDQUERY`, `NON_SUPPORT_SCHEME`, `NO_AUTHORITY`); `UrlRelativeReference` resolves
  a relative URL as `redirect_url`; `ALLOW_SPACE` parses spaces as `%20` and puts them back;
  `Duplicate` shares the immutable `UrlHandleParts`. No upstream source or real curl was reachable
  from the lane (the gap cache is behind the audit guard), so the rules follow upstream's code as
  written, not a measurement.
- Two existing tests changed because their premise is now upstream's behaviour, not weakened:
  `Set_NoSchemeWithoutGuessOrDefault_ReturnsBadSchemeAndKeepsTheHandle` now uses a handle with no
  whole URL (one holding a URL resolves the text as a relative reference, as upstream does), and
  `Get_DefaultPortFlagForASchemeWithoutOne_ReturnsNoPort` parses `foo://` under
  `CURLU_NON_SUPPORT_SCHEME` (without it upstream refuses the scheme). The old
  `Set_PartOtherThanTheUrlOrAClear_ThrowsNotSupported` is replaced by the set, clear and dup tests.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` 100% line, 100% branch,
  0 failing members, worst CRAP 10. Fast tests green; conformance ratchet untouched.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. ICurlUrlHandle sets every part with the CURLU_* set flags, resolves relative URLs, clears with null and duplicates (ADR-0602)
