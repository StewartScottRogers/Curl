---
id: BL-4129
title: Extension: Upload resumably with --resumable-upload
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094, BL-4299, BL-4300]
touches: [Curl.Extension.ResumableUpload.UnitLibrary, Curl.Extension.ResumableUpload.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4129 — Extension: Upload resumably with --resumable-upload

## Goal

Curl provides `--resumable-upload` using the IETF protocol, or tus when the server offers it, as the extension `Curl.Extension.ResumableUpload.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 28 of 63. Specification: draft-ietf-httpbis-resumable-upload (latest) and tus 1.0. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [ ] `Curl.Extension.ResumableUpload.UnitLibrary` and `Curl.Extension.ResumableUpload.UnitTests` exist in `/Extensions/ResumableUpload/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [ ] It provides `--resumable-upload` using the IETF protocol, or tus when the server offers it.
- [ ] Unit tests check its wire behaviour against named sections of draft-ietf-httpbis-resumable-upload (latest) and tus 1.0, with no network (fake connections or recorded bytes).
- [ ] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [ ] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 10): Split for size before any code. BL-4299 is the project, the HTTP/1.1 exchange over `IConnection` and the IETF and tus header fields; BL-4300 is the transfer hook running the tus and IETF flows with resumption. What is left here: the `--resumable-upload` option (`ExtensionOption`, flag), `HookFor`, the lines in `ExtensionOptionCatalogue.cs`, `ExtensionDescriptionCatalogue.cs` and `ExtensionTransferHookCatalogue.cs` (an option extension registers there, as `--segments` does, not in `ExtensionCatalogue.cs`, which holds scheme handlers; added to touches, all three are shared list files), the `CURL_EXTENSIONS=0` and unchanged-transfer tests, `--ai-help extensions`, and the ADR (draft version and interop version followed, tus discovery by `OPTIONS`, what is left out).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 46b019b9 f97944a0 82eeeed2 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split for size: waits on BL-4299 (project, HTTP/1.1 exchange, header fields) and BL-4300 (tus and IETF upload flows)
