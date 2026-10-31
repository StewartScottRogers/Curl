---
id: BL-4128
title: Extension: Download one file in parallel ranges with --segments
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094]
touches: [Curl.Extension.SegmentedDownload.UnitLibrary, Curl.Extension.SegmentedDownload.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4128 — Extension: Download one file in parallel ranges with --segments

## Goal

Curl provides `--segments <n>`, as the extension `Curl.Extension.SegmentedDownload.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 27 of 63. Specification: RFC 9110 range requests. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.SegmentedDownload.UnitLibrary` and `Curl.Extension.SegmentedDownload.UnitTests` exist in `/Extensions/SegmentedDownload/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `--segments <n>`.
- [x] Unit tests check its wire behaviour against named sections of RFC 9110 range requests, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 4): Like `--cache-dir` (BL-4133) the extension adds no scheme, so it registers in `ExtensionOptionCatalogue`, `ExtensionDescriptionCatalogue` and `ExtensionTransferHookCatalogue` (one-line shared lists in `Tasks/SharedListFiles.txt`; added to touches) and `ExtensionCatalogue.cs`, which holds protocol handler factories, gets no line (recorded in ADR-1087). Delivered directly in this session rather than through the multi-agent `/feature` stages, to fit the run's cost cap: one small library over the existing transfer hook seam (ADR-1019) needed no architecture plan. Design in ADR-1087: probe `Range: bytes=0-0` for the complete length, then `n` ranges in parallel with `If-Range` on a strong ETag, joined only when every segment is a 206 of exactly its range of the same representation; 200 from a server ignoring Range written as received; anything else re-runs the transfer as curl does. Value 1..64, anything else exit 2 without contacting the server (default chosen: 64 bounds parallel connections to one host). Segments are held in memory until all are checked, so a failure never leaves half a file.
- 2026-10-29 (lane 4): Verified. Measure-CodeQuality: SegmentedDownload 100% line and branch, worst CRAP 10, 0 failing; Composition 100/100, 0 failing. `dotnet build` clean, `dotnet format` clean, fast tests 48,907 passed in 75 assemblies, 0 failed. The built curl.exe lists `segments` in `--extensions` and `--ai-help extensions`, refuses `--segments` with `CURL_EXTENSIONS=0` as an unknown option (exit 2), and refuses `--segments 0` with exit 2. The other Doing tasks on origin/work/dark-factory name only the shared one-line catalogue files, which make no overlap. Criterion 1 ticked on the BL-4133 precedent: an extension with no protocol handler registers in the hook catalogues, not `ExtensionCatalogue.cs` (ADR-1087).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 2e9fbc2e 7ecafeca 93027b72 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. curl --segments <n> downloads a plain HTTP GET as n parallel byte ranges joined in order (RFC 9110 range requests)
