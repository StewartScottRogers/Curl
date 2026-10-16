---
id: BL-2435
title: Engine: sharing of the public-suffix list and HSTS
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2434]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2435 — Engine: sharing of the public-suffix list and HSTS

## Goal

The engine's `IShareHandle` shares PSL and HSTS state between attached handles.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Engine tests show an HSTS entry learned by one handle upgrading another's request.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Found on claim: BL-2434 already gave `ManagedShareHandle` its own `HstsTransferPolicy`, taken under the `Hsts` lock, so HSTS needed only the cross-engine tests: `ManagedShareHandleTests.PerformAsync_HstsShared_SwitchesAnotherEnginesHandleToHttpsForAHostOneHandleLearned` (one engine's handle learns `Strict-Transport-Security` over https; another engine's `http://` request then connects to port 443) and `PerformAsync_HstsNotShared_LeavesAnotherEnginesHandleOnHttp`.
- PSL (ADR-0617, decided under delegation): Curl has no libpsl. Every transfer of every engine checks against `Curl.Cookies`' one embedded, immutable `PublicSuffixList` snapshot, so the list is already shared. `Attach` now takes the `Psl` lock right after the `Share` lock while PSL is shared, so lock callbacks see it like every other shared kind (`PerformAsync_PslShared_TakesThePublicSuffixListUnderItsLock`). No lock around each cookie check: the snapshot is thread-safe and the lock would guard nothing.
- The plan (`Curl-Api-And-libcurl.md`) needed no correction for this task. `Libcurl` untouched, no option changes, `--ai-help` untouched.
- Verified: `dotnet build` 0 warnings; fast tests 41 projects passed, 0 failed (`ManagedShareHandleTests` 23/23); `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` 0 failing members; `PassingUpstreamCases.txt` unchanged.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. Engine share handle: HSTS learned by one engine's handle upgrades another's http request; PSL lock taken while shared (ADR-0617)
