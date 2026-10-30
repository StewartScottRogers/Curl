---
id: BL-4137
title: Extension: Send requests through an Oblivious HTTP relay
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094, BL-4100, BL-4303, BL-4311, BL-4309, BL-4310]
touches: [Curl.Extension.ObliviousHttp.UnitLibrary, Curl.Extension.ObliviousHttp.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4137 — Extension: Send requests through an Oblivious HTTP relay

## Goal

Curl provides an option naming the relay and the gateway key config, as the extension `Curl.Extension.ObliviousHttp.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 36 of 63. Specification: RFC 9458 and RFC 9540. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [ ] `Curl.Extension.ObliviousHttp.UnitLibrary` and `Curl.Extension.ObliviousHttp.UnitTests` exist in `/Extensions/ObliviousHttp/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [ ] It provides an option naming the relay and the gateway key config.
- [ ] Unit tests check its wire behaviour against named sections of RFC 9458 and RFC 9540, with no network (fake connections or recorded bytes).
- [ ] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [ ] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 5, first pass before any code): split for size. The wire format is three tasks: BL-4311 (project scaffold and HPKE base mode, RFC 9180, using the shared `Curl.Cryptography.X25519`), BL-4309 (Binary HTTP, RFC 9292) and BL-4310 (key configurations and encapsulation, RFC 9458 sections 3 and 4, RFC 9540 section 3). What is left here is the client flow: a transfer hook that encodes the user's request, POSTs it as `message/ohttp-req` to the relay, decapsulates the `message/ohttp-res` answer and writes the inner response; the options naming the relay and the key config (a file, or fetched from the gateway per RFC 9540); the catalogue lines, description, `--ai-help`, ADR and the `CURL_EXTENSIONS=0` tests.
- The POST to the relay needs a hook context with another URL, method and body, which BL-4303 (in Doing) adds as `TransferContextRequests.WithRequest`; so this task waits on it rather than writing its own `ITransferContext`.
- Touches gained `ExtensionOptionCatalogue.cs`, `ExtensionDescriptionCatalogue.cs` and `ExtensionTransferHookCatalogue.cs`: an option-driven transfer hook registers there (as HttpCache and OAuth2 do), not in `ExtensionCatalogue.cs`. All three are shared list files, so they overlap no task.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 0ccaeafb c14da2cb 82b195bf e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split for size into BL-4311, BL-4309 and BL-4310 (HPKE, Binary HTTP, encapsulation); also waits on BL-4303 for the relay POST context
