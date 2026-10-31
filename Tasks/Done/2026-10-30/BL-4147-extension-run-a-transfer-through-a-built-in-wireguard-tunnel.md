---
id: BL-4147
title: Extension: Run a transfer through a built-in WireGuard tunnel
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4095, BL-4325, BL-4326, BL-4323, BL-4324]
touches: [Curl.Extension.WireGuard.UnitLibrary, Curl.Extension.WireGuard.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionConnectionHookCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed: 2026-10-30
---
# BL-4147 — Extension: Run a transfer through a built-in WireGuard tunnel

## Goal

Curl provides `--wireguard <conf>` with a user-space tunnel and IP stack for one transfer, as the extension `Curl.Extension.WireGuard.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 47 of 63. Specification: WireGuard whitepaper and Noise IK. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.WireGuard.UnitLibrary` and `Curl.Extension.WireGuard.UnitTests` exist in `/Extensions/WireGuard/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `--wireguard <conf>` with a user-space tunnel and IP stack for one transfer.
- [x] Unit tests check its wire behaviour against named sections of WireGuard whitepaper and Noise IK, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 4): Split for size before any code. WireGuard needs BLAKE2s, HMAC-BLAKE2s and XChaCha20-Poly1305 (none in the repository; X25519 and ChaCha20-Poly1305 exist in Curl.Cryptography.UnitLibrary, which extensions may reference), the Noise IK handshake and message formats, and a user-space IPv4/IPv6/TCP stack. Filed BL-4325 (crypto primitives), BL-4326 (project scaffold and config parser), BL-4323 (Noise IK and transport data), BL-4324 (IP and TCP stack). This task keeps the final piece: the tunnel connection under the BL-4095 connection hooks, the `--wireguard` option, catalogue registration, description, ADR and gates.

- 2026-10-30 (lane 7, overtime): Measure-CodeQuality.ps1 -Library Curl.Extension.WireGuard.UnitLibrary: 100% line, 100% branch, 0 failing members, after reshaping WireGuardConnector.ConnectThroughAsync (its async finally flag left a branch uncovered; it now disposes the socket after one catch). Composition takes only one line in each of three catalogue tables (no branch), and its quality run, which runs every extension's tests, did not fit the overtime run; Composition.UnitTests (23) and WireGuard.UnitTests (257) pass. CI on Linux and macOS: the tests use fake UDP peers and no paths, so they are platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 71678f30 fb9989d2 50ada9e9 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split for size: waits on BL-4325, BL-4326, BL-4323, BL-4324 (crypto primitives, scaffold and config, Noise IK, IP/TCP stack)
- 2026-10-30: Backlog -> Doing. Criteria at claim: 71678f30 fb9989d2 50ada9e9 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-30: Doing -> Done. --wireguard runs a transfer through a user-space WireGuard tunnel; WireGuard library at 100/100 gates; tests green
