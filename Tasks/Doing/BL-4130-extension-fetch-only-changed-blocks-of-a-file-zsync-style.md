---
id: BL-4130
title: Extension: Fetch only changed blocks of a file zsync-style
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094]
touches: [Curl.Extension.DeltaDownload.UnitLibrary, Curl.Extension.DeltaDownload.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4130 — Extension: Fetch only changed blocks of a file zsync-style

## Goal

Curl provides an option that uses a `.zsync` control file to fetch only changed ranges into an existing local file, as the extension `Curl.Extension.DeltaDownload.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 29 of 63. Specification: zsync 0.6 control file format. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [ ] `Curl.Extension.DeltaDownload.UnitLibrary` and `Curl.Extension.DeltaDownload.UnitTests` exist in `/Extensions/DeltaDownload/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [ ] It provides an option that uses a `.zsync` control file to fetch only changed ranges into an existing local file.
- [ ] Unit tests check its wire behaviour against named sections of zsync 0.6 control file format, with no network (fake connections or recorded bytes).
- [ ] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [ ] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 68b799a4 164f279b b37a9b72 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
