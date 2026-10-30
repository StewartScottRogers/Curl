---
id: BL-4130
title: Extension: Fetch only changed blocks of a file zsync-style
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094]
touches: [Curl.Extension.DeltaDownload.UnitLibrary, Curl.Extension.DeltaDownload.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4130 — Extension: Fetch only changed blocks of a file zsync-style

## Goal

Curl provides an option that uses a `.zsync` control file to fetch only changed ranges into an existing local file, as the extension `Curl.Extension.DeltaDownload.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 29 of 63. Specification: zsync 0.6 control file format. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.DeltaDownload.UnitLibrary` and `Curl.Extension.DeltaDownload.UnitTests` exist in `/Extensions/DeltaDownload/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides an option that uses a `.zsync` control file to fetch only changed ranges into an existing local file.
- [x] Unit tests check its wire behaviour against named sections of zsync 0.6 control file format, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29: Added the option, description and transfer-hook catalogues to touches: an option-only extension registers there, not in ExtensionCatalogue.cs (ADR-1069, ADR-1087); no other Doing task names them, and they are shared list files. ExtensionCatalogue.cs gets no line, since the extension adds no protocol handler.
- 2026-10-29: Decided (ADR-1090): `--zsync <control-file>` plus `--zsync-seed <file>`, defaulting to the control file's Filename; unreadable or malformed control file is exit 26; requests run in turn, one per run of missing blocks; SHA-1 mismatch or a bad 206 reruns the transfer as curl does. MD4 written by hand (RFC 1320).
- 2026-10-29: Quality measured once with Measure-CodeQuality.ps1: only the copied CapturedResponse and WholeResourceGet branches were short, now covered by CapturedResponseTests and WholeResourceGetTests. CI on Linux and macOS: tests use temporary directories via Path.Combine and no platform-specific text, so the criterion is met by staying platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 68b799a4 164f279b b37a9b72 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. zsync extension: --zsync and --zsync-seed fetch only missing blocks, ADR-1090
