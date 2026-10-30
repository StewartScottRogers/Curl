---
id: BL-4155
title: Extension: Export transfers as HAR with --har
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094]
touches: [Curl.Extension.Har.UnitLibrary, Curl.Extension.Har.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed: 2026-10-30
---
# BL-4155 — Extension: Export transfers as HAR with --har

## Goal

Curl provides `--har <file>`, as the extension `Curl.Extension.Har.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 55 of 63. Specification: HAR 1.2 specification (W3C draft). It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.Har.UnitLibrary` and `Curl.Extension.Har.UnitTests` exist in `/Extensions/Har/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `--har <file>`.
- [x] Unit tests check its wire behaviour against named sections of HAR 1.2 specification (W3C draft), with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-30 (BL-4155 run 1): Written and in the stash: the library (HarExtension, HarTransferHook, HarDocument, HarTimings, HarExchange, BodyCopyingStream, CLAUDE.md), 26 unit tests in Curl.Extension.Har.UnitTests (all pass), one line each in the option, description and transfer hook catalogues (added to touches: transfer hooks have no scheme, so no ExtensionCatalogue line, as BL-4134 did; all are merge=union shared list files), the Curl.slnx line, and ADR-1127. Builds clean (complexity gate passes). Left for the next claim: run `Measure-CodeQuality.ps1 -Library Curl.Extension.Har.UnitLibrary,Curl.Extension.Composition.UnitLibrary -ReportPath ...` once (this run's attempt hit its time limit while Curl.Mcp.UnitTests' test host crashed under load), fix any gap, run the fast tests, tick the boxes and commit. No follow-up task filed: the work lives in this task's stash, so a separate task would wait on it.

- 2026-10-30 (BL-4155 run 2): Measure-CodeQuality.ps1 for Har and Composition: HarDocument.WriteResponse was complexity 12, so its status line moved to WriteStatusLine; both libraries now 100% line and branch, 0 failing members. Build clean, fast tests green (exit 0). CI on Linux and macOS: the tests write only to an in-memory stream and use no paths or OS text, so they are platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 429822e8 6472bf92 b9114855 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-30: Doing -> Backlog. Out of time: code and 26 passing tests written (in stash); quality-gate measurement, fast tests and commit remain
- 2026-10-30: Backlog -> Doing. Criteria at claim: 429822e8 6472bf92 b9114855 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-30: Doing -> Done. --har writes HAR 1.2 archives; quality gates met, fast tests green
