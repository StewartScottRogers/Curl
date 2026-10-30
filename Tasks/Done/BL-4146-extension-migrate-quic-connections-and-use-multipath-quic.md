---
id: BL-4146
title: Extension: Migrate QUIC connections and use multipath QUIC
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4095, BL-4320]
touches: [Curl.Extension.QuicMigration.UnitLibrary, Curl.Extension.QuicMigration.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionConnectionHookCatalogue.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-30
---
# BL-4146 — Extension: Migrate QUIC connections and use multipath QUIC

## Goal

Curl provides an option that keeps a QUIC transfer alive across a local address change, as the extension `Curl.Extension.QuicMigration.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 46 of 63. Specification: RFC 9000 section 9 and draft-ietf-quic-multipath (latest). It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.QuicMigration.UnitLibrary` and `Curl.Extension.QuicMigration.UnitTests` exist in `/Extensions/QuicMigration/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides an option that keeps a QUIC transfer alive across a local address change.
- [x] Unit tests check its wire behaviour against named sections of RFC 9000 section 9 and draft-ietf-quic-multipath (latest), with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 6, first claim): Design: a flag `--quic-migrate`, a connection hook (like `--npipe`'s, ADR-1021) that keeps curl's stream connector and wraps curl's datagram connector in `MigratingDatagramConnector`; each channel is a `MigratingDatagramChannel` that passes every datagram through unchanged and, once a 1-RTT (short header, RFC 9000 section 17.3.1) packet went each way (section 9: no migration before the handshake is confirmed), answers a send or receive failing with a local path loss socket error (`AddressNotAvailable`, `NetworkDown`, `NetworkUnreachable`, `NetworkReset`, `HostUnreachable`) by opening a fresh channel to the same server through curl's connector, disposing the old one (one path at a time, draft-ietf-quic-multipath section 2 without negotiation) and carrying on; the server validates the new path (section 9.3). Planned ADR: ADR-1109, recording that a new connection ID (section 9.5), PATH_CHALLENGE probing and multipath need a seam into the QUIC stack (to be filed as a follow-up task). Registration: like `--npipe`, an option extension registers in `ExtensionOptionCatalogue`, `ExtensionDescriptionCatalogue` and `ExtensionConnectionHookCatalogue` (all shared list files), not in `ExtensionCatalogue`, which holds protocol handler factories only; those three added to `touches`.
- 2026-10-29: Requeued for BL-4320. In production `EngineComposition.DialersOf` builds HTTP/3's `QuicDialer` over its own `UdpChannelOpener`, so a connection hook's `DatagramConnectorOver` never sees a QUIC datagram (only the dialing harness opens QUIC through a datagram connector). The library, its catalogue lines and the slnx line are written (uncommitted, in the stash); tests, the ADR, the build and the quality run are still to do.

- 2026-10-30 (lane 11): Finished from the stashed work: tests for every class (RFC 9000 sections 9, 9.2, 17.3.1 and multipath section 2, over fake datagram channels), ADR-1149 (ADR-1109 was taken by vm-socket, so the planned number moved), and `MoveFromAsync` now rethrows with `?? throw error` so no unreachable brace stays uncovered. Measure-CodeQuality.ps1 for the library: 100% line, 100% branch, 0 failing members. CI on Linux and macOS: the tests touch no path, file, OS error text or certificate and wait on a gate rather than scheduling, so they are platform-neutral; the fast tests are green here. The PATH_CHALLENGE, new connection ID and multipath work ADR-1149 leaves out needs a seam into the QUIC stack and is not filed here.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 3bb4418d d00a2947 dae9b5ab e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Waits on BL-4320: HTTP/3's QUIC dialer opens its own UDP sockets, so the extension's datagram connector never sees a QUIC datagram
- 2026-10-30: Backlog -> Doing. Criteria at claim: 3bb4418d d00a2947 dae9b5ab e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-30: Doing -> Done. Extension --quic-migrate built, tested, ADR-1149, gates met
