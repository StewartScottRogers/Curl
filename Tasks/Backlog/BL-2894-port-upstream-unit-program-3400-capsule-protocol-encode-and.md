---
id: BL-2894
title: Port upstream unit program 3400 (capsule protocol encode and decode) over CapsuleDatagramChannel
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2895]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Headers.Capsule.cs, Curl.Conformance.UnitProgram.UnitLibrary/Headers/Capsule, Curl.Conformance.UnitProgram.UnitTests/Headers/Capsule, Curl.Networking.UnitLibrary/CapsuleDatagramChannel.cs, Curl.Networking.UnitLibrary/Curl.Networking.UnitLibrary.csproj]
requirement: none
created: 2026-10-21
completed:
---
# BL-2894 — Port upstream unit program 3400 (capsule protocol encode and decode) over CapsuleDatagramChannel

## Goal

Upstream case 3400 runs as a ported unit program over Curl's capsule code and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2846 for size. curl 8.22.0 `tests/unit/unit3400.c` tests `Curl_capsule_encap_udp_datagram` (header size at the variable-length-integer boundaries), an encode/decode round trip, sequential decode of several capsules from one buffer, and decode paths (partial capsule, oversized capsule discarded, zero-length capsule, unknown capsule type). Curl's counterpart is `Curl.Networking.UnitLibrary/CapsuleDatagramChannel.cs` (`internal`; `BuildCapsule` and the receive loop). It may need its decoder exposed as a testable internal method and an `InternalsVisibleTo` for `Curl.Conformance.UnitProgram.UnitLibrary` in `Curl.Networking.UnitLibrary.csproj` (both in `touches`); where the port finds Curl wrong, fix it with a focused test in `Curl.Networking.UnitTests` (add it to `touches`). Mechanism: ADR-0711 and BL-2842's Hashes ports. Register in `UnitPrograms.Headers.Capsule.cs`, which BL-2895 creates.

## Acceptance criteria

- [ ] unit3400 is ported in `Curl.Conformance.UnitProgram.UnitLibrary/Headers/Capsule/`, check for check with the C program (named in its doc comment), registered in `UnitPrograms.Headers.Capsule.cs`, with a test in `Curl.Conformance.UnitProgram.UnitTests/Headers/Capsule/`.
- [ ] The case ends `Passed` with `$env:CURL_UPSTREAM_CASE='3400'` on Windows and is on `PassingUpstreamCases.txt`.
- [ ] Where the port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
