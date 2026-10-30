---
id: BL-4143
title: Extension: Transfer over vsock and Hyper-V sockets
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4095]
touches: [Curl.Extension.VirtualMachineSocket.UnitLibrary, Curl.Extension.VirtualMachineSocket.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4143 — Extension: Transfer over vsock and Hyper-V sockets

## Goal

Curl provides an option naming a vsock CID:port or Hyper-V VM and service GUID, as the extension `Curl.Extension.VirtualMachineSocket.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 42 of 63. Specification: vsock(7) and Hyper-V sockets (AF_HYPERV). It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [ ] `Curl.Extension.VirtualMachineSocket.UnitLibrary` and `Curl.Extension.VirtualMachineSocket.UnitTests` exist in `/Extensions/VirtualMachineSocket/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [ ] It provides an option naming a vsock CID:port or Hyper-V VM and service GUID.
- [ ] Unit tests check its wire behaviour against named sections of vsock(7) and Hyper-V sockets (AF_HYPERV), with no network (fake connections or recorded bytes).
- [ ] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [ ] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: c94c7f4b 92d348ae 3fee1654 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
