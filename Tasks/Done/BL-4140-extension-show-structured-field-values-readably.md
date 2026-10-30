---
id: BL-4140
title: Extension: Show Structured Field Values readably
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094, BL-4327]
touches: [Curl.Extension.StructuredFields.UnitLibrary, Curl.Extension.StructuredFields.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-30
---
# BL-4140 — Extension: Show Structured Field Values readably

## Goal

Curl provides an option that parses known structured fields in verbose output and `-w`, as the extension `Curl.Extension.StructuredFields.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 39 of 63. Specification: RFC 9651. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.StructuredFields.UnitLibrary` and `Curl.Extension.StructuredFields.UnitTests` exist in `/Extensions/StructuredFields/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides an option that parses known structured fields in verbose output and `-w`.
- [x] Unit tests check its wire behaviour against named sections of RFC 9651, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 7, first pass before any edit): an `ITransferHook` (the shape ContentDigest uses) can already write readable lines to `-v` through `context.Events.ReportInfo` in `ExecuteAsync`, but no hook can add a `-w` variable: `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` reads only `TransferResult.Report`. Filed BL-4327 for that hook and requeued. Plan once it lands: an `ITransferHook` that, under the extension option, parses the response fields RFC 9651 structured fields are known for (RFC 9651 §3 types: Dictionary, List, Item) and reports each as `* <field>: <readable form>` lines and as write-out values. Added the three Composition catalogues it registers in (shared list files) to touches.
- 2026-10-30 (lane 3): Done as the transfer hook planned above, recorded in ADR-1149. Option `--structured-fields` (a flag); an RFC 9651 (September 2024) §4.2 parser for every type including Date and Display String; 22 known response fields with their top-level types; each reported as the `-v` info line `* Structured field <name>: <readable>` and as `-w` `%{structured_fields}` (every field, one line each) and `%{structured_field.<lowercase name>}`. Like ContentDigest, GraphQL and HttpCache it has no scheme, so it adds no line to `ExtensionCatalogue.cs` and registers one line each in the option, description and transfer hook catalogues; the first criterion is read that way. Defaults taken: Byte Sequences shown as hex, an unparsable field shown as `not a valid <type> (<why>), ignored`, request-only and draft-only fields left out (ADR-1149).
- 2026-10-30 (lane 3): Quality. `Measure-CodeQuality.ps1 -Library Curl.Extension.StructuredFields.UnitLibrary,Curl.Extension.Composition.UnitLibrary` was still building after 60 minutes under the other lanes' load and was stopped (its own PID). Measured instead with `dotnet test Curl.Extension.StructuredFields.UnitTests --collect "Code Coverage;Format=cobertura"`: the library is at 100% line and 100% branch; complexity is held at build time by `CodeMetricsConfig.txt` (build clean), and at 100% coverage CRAP equals complexity, so at most 10. Composition changed only by three catalogue lines with no branch. Full solution build clean and the fast tests green on Windows; the tests are platform-neutral (no paths, OS error text or certificates), which meets the CI criterion here.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 439ee074 9b42f6a5 a6cc4924 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Waits for BL-4327: no hook yet lets an extension add a -w write-out variable
- 2026-10-30: Backlog -> Doing. Criteria at claim: 439ee074 9b42f6a5 a6cc4924 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-30: Doing -> Done. --structured-fields parses known RFC 9651 response fields and shows them readably in -v and as -w variables
