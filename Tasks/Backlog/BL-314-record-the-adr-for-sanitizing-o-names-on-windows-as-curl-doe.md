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
completed:
---
# BL-314 — Record the ADR for sanitizing -o names on Windows as curl does

## Goal

`Documentation/Planning/Decisions` holds an ADR, marked "Decided by Claude under Stewart's delegation", recording the design BL-283 shipped for curl's Windows `-o` name sanitizing.

## Context

- BL-283 made the decision but could not write the ADR: `Documentation/Planning/Decisions` was named in the `touches` of BL-163, then in Doing on another lane. The decision and the measurements are in BL-283's Notes; copy them, do not re-decide.
- Code: `Curl.Core.UnitLibrary/Globbing/WindowsOutputFileNameSanitizer.cs`, `UrlGlobMatch.ResolveOutputFileName`.

## Acceptance criteria

- [ ] An ADR under `Documentation/Planning/Decisions` states: sanitizing is a separate `WindowsOutputFileNameSanitizer`, run by `UrlGlobMatch.ResolveOutputFileName` only when globbing is on (not under `-g`), and only when the caller passes `sanitizesForWindows` (`OperatingSystem.IsWindows()`); with the alternatives BL-283's Notes list.
- [ ] The ADR index in `Documentation/Planning/Decisions/README.md` lists it.

## Notes

## Log

- 2026-09-26: Created.
