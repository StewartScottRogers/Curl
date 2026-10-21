---
id: BL-2894
title: Port upstream unit program 3400 (capsule protocol encode and decode) over CapsuleDatagramChannel
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2895]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Headers.Capsule.cs, Curl.Conformance.UnitProgram.UnitLibrary/Headers/Capsule, Curl.Conformance.UnitProgram.UnitTests/Headers/Capsule, Curl.Networking.UnitLibrary/CapsuleDatagramChannel.cs, Curl.Networking.UnitLibrary/Curl.Networking.UnitLibrary.csproj, Curl.Networking.UnitTests/CapsuleDatagramChannelTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2894 — Port upstream unit program 3400 (capsule protocol encode and decode) over CapsuleDatagramChannel

## Goal

Upstream case 3400 runs as a ported unit program over Curl's capsule code and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2846 for size. curl 8.22.0 `tests/unit/unit3400.c` tests `Curl_capsule_encap_udp_datagram` (header size at the variable-length-integer boundaries), an encode/decode round trip, sequential decode of several capsules from one buffer, and decode paths (partial capsule, oversized capsule discarded, zero-length capsule, unknown capsule type). Curl's counterpart is `Curl.Networking.UnitLibrary/CapsuleDatagramChannel.cs` (`internal`; `BuildCapsule` and the receive loop). It may need its decoder exposed as a testable internal method and an `InternalsVisibleTo` for `Curl.Conformance.UnitProgram.UnitLibrary` in `Curl.Networking.UnitLibrary.csproj` (both in `touches`); where the port finds Curl wrong, fix it with a focused test in `Curl.Networking.UnitTests` (add it to `touches`). Mechanism: ADR-0711 and BL-2842's Hashes ports. Register in `UnitPrograms.Headers.Capsule.cs`, which BL-2895 creates.

## Acceptance criteria

- [x] unit3400 is ported in `Curl.Conformance.UnitProgram.UnitLibrary/Headers/Capsule/`, check for check with the C program (named in its doc comment), registered in `UnitPrograms.Headers.Capsule.cs`, with a test in `Curl.Conformance.UnitProgram.UnitTests/Headers/Capsule/`.
- [x] The case ends `Passed` with `$env:CURL_UPSTREAM_CASE='3400'` on Windows and is on `PassingUpstreamCases.txt`.
- [x] Where the port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- Added `Curl.Networking.UnitTests/CapsuleDatagramChannelTests.cs` to touches, as the task allows for a fix found by the port; no task in Doing names it.
- The port found Curl wrong: `CapsuleDatagramChannel` skipped capsules of another type or context ID and length-0 capsules, and cut an oversized datagram, where curl 8.22.0's `Curl_capsule_process_udp_raw` fails with `CURLE_RECV_ERROR`. ADR-0725: `DecodeDatagram` is now curl's decoder over a span (one-byte context ID, curl's skip counts), `BuildCapsuleHeader` is `capsule_encap_udp_hdr`, and `ReceiveAsync` buffers and decodes with it, throwing `SocketException(ConnectionAborted)` for a rejected capsule. Focused tests replace the three skip/cut tests in `CapsuleDatagramChannelTests`.
- `Unit3400Capsule` runs every check on every platform: Curl carries HTTP/3 through a proxy everywhere, so ADR-0723's split does not apply. A `bufq` is a byte array the decode drops consumed bytes from.
- Measured: case 3400 Passed with `CURL_UPSTREAM_CASE=3400`; Measure-CodeQuality for Curl.Networking.UnitLibrary and Curl.Conformance.UnitProgram.UnitLibrary: 0 failing members (a first run found `DecodeHeader` at complexity 14 and a switch branch in `ReceiveAsync`, both split).

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 69e442a9 9bdd4a42 c29b1954 dd1cc079.
- 2026-10-21: Doing -> Done. unit3400 ported over CapsuleDatagramChannel, which now decodes capsules by curl's rules; case 3400 passes and is on the ratchet
