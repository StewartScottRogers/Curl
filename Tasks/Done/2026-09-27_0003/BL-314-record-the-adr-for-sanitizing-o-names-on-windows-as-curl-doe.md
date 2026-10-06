---
id: BL-314
title: Record the ADR for sanitizing -o names on Windows as curl does
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-283]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-314 — Record the ADR for sanitizing -o names on Windows as curl does

## Goal

`Documentation/Planning/Decisions` holds an ADR, marked "Decided by Claude under Stewart's delegation", recording the design BL-283 shipped for curl's Windows `-o` name sanitizing.

## Context

- BL-283 made the decision but could not write the ADR: `Documentation/Planning/Decisions` was named in the `touches` of BL-163, then in Doing on another lane. The decision and the measurements are in BL-283's Notes; copy them, do not re-decide.
- Code: `Curl.Core.UnitLibrary/Globbing/WindowsOutputFileNameSanitizer.cs`, `UrlGlobMatch.ResolveOutputFileName`.

## Acceptance criteria

- [x] An ADR under `Documentation/Planning/Decisions` states: sanitizing is a separate `WindowsOutputFileNameSanitizer`, run by `UrlGlobMatch.ResolveOutputFileName` only when globbing is on (not under `-g`), and only when the caller passes `sanitizesForWindows` (`OperatingSystem.IsWindows()`); with the alternatives BL-283's Notes list.
- [x] The ADR index in `Documentation/Planning/Decisions/README.md` lists it.

## Notes

- Wrote `ADR-0048-o-names-are-sanitized-on-windows-by-a-separate-step-only-when-globbing.md` from BL-283's Notes, each statement checked against `WindowsOutputFileNameSanitizer`, `UrlGlobMatch.ResolveOutputFileName` and `UrlGlob`; indexed in `README.md`.
- BL-283's Notes lost their backslashes (`\?\C:<tab>mp?b`); the ADR's measurement table takes the exact strings from the tests pinned in `Curl.Core.UnitTests/Globbing/UrlGlobTests.cs` instead.
- `Curl.Console` does not call `ResolveOutputFileName` yet; the ADR says the wiring is BL-240. ADR-0032's Consequences still name `SubstituteGlobValues` as the console's entry point; ADR-0048 says it refines that line, and the accepted ADR-0032 is left as written.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. ADR-0048 records the Windows -o name sanitizing design BL-283 shipped, indexed in the Decisions README
