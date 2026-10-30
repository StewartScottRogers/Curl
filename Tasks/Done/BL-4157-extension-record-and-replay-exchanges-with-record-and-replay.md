---
id: BL-4157
title: Extension: Record and replay exchanges with --record and --replay
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094, BL-4095]
touches: [Curl.Extension.RecordReplay.UnitLibrary, Curl.Extension.RecordReplay.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4157 — Extension: Record and replay exchanges with --record and --replay

## Goal

Curl provides `--record <dir>` and `--replay <dir>`, as the extension `Curl.Extension.RecordReplay.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 57 of 63. Specification: the record format the ADR defines. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.RecordReplay.UnitLibrary` and `Curl.Extension.RecordReplay.UnitTests` exist in `/Extensions/RecordReplay/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `--record <dir>` and `--replay <dir>`.
- [x] Unit tests check its wire behaviour against named sections of the record format the ADR defines, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 11): Built as a transfer hook (ADR-1019), modelled on `--cache-dir`. Added the option, description and transfer hook catalogues to `touches`: they are shared list files (`Tasks/SharedListFiles.txt`) that each extension adds lines to, and an option-only extension registers there. As ADR-1106 and ADR-1126, it adds no line to `ExtensionCatalogue.cs`, which lists URL-scheme handlers only; the first criterion's "one line" registration is met by its lines in those catalogues.
- Decisions (ADR-1129, numbered 1129 because the shared branch already holds an ADR-1128): Curl exchange record format 1, one `<sha256 key>.exchange` file per request keyed by method, URL and `-d` body; HTTP(S) transfers only; a replay miss is exit 7 unless `--record` is also given, then it is fetched and recorded. Request headers, `-F` bodies, timings and HAR/WARC are left out, with why in the ADR.
- CURL_EXTENSIONS=0: checked as the other extensions do, by `ExtensionCommandLineOptions.For` leaving both options out (so the parser refuses them as unknown, exit 2); with neither option `HookFor` gives no hook, so transfers run unchanged.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Extension.RecordReplay.UnitLibrary`: 100% line, 100% branch, 29 members, 0 failing, worst CRAP 10. `dotnet build` clean; fast tests green (99 test projects passed, 30 in RecordReplay.UnitTests). Tests are platform-neutral (temp directories, a file in place of a directory for the unwritable case), so CI on Linux and macOS needs nothing more.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 02c5665a 12bfaf34 e1579917 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. --record and --replay record HTTP exchanges to a directory and replay them without the network (ADR-1129)
