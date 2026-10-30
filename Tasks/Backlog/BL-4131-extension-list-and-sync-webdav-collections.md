---
id: BL-4131
title: Extension: List and sync WebDAV collections
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094, BL-4303, BL-4304, BL-4305]
touches: [Curl.Extension.WebDavTools.UnitLibrary, Curl.Extension.WebDavTools.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4131 — Extension: List and sync WebDAV collections

## Goal

Curl provides `--webdav-list` (parsed PROPFIND) and a directory sync option, as the extension `Curl.Extension.WebDavTools.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 30 of 63. Specification: RFC 4918. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [ ] `Curl.Extension.WebDavTools.UnitLibrary` and `Curl.Extension.WebDavTools.UnitTests` exist in `/Extensions/WebDavTools/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [ ] It provides `--webdav-list` (parsed PROPFIND) and a directory sync option.
- [ ] Unit tests check its wire behaviour against named sections of RFC 4918, with no network (fake connections or recorded bytes).
- [ ] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [ ] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 3): Split for size before any code, following BL-4125 (GCS). An option extension registers an `ExtensionOption` and an `ITransferHook` (as `SegmentedDownloadExtension` does), but a hook cannot yet change the URL, method or body of the request it passes on (`HookedTransferContext` forwards `Url` and `PostData`; nothing sets `Http.CustomMethod`), so filed BL-4303 (`WithRequest` in Extension.Abstractions/Transfer). Planned default: send PROPFIND and the sync GETs through the engine (TLS, proxy, `--user`) via that hook, not raw HTTP over IConnection. Filed BL-4304 (project scaffold, PROPFIND body and Depth header, 207 Multistatus reader) and BL-4305 (sync planner from a listing and a local folder). This task keeps the options (`--webdav-list`, a sync option taking a local directory), the hook, catalogue, option, transfer-hook and description registration (shared list files, added to touches), the ADR and the CURL_EXTENSIONS=0 test.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 9634e382 6e6e8883 ceea66be e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split for size: waits on BL-4303 (hook WithRequest), BL-4304 (scaffold, PROPFIND and Multistatus) and BL-4305 (sync planner)
