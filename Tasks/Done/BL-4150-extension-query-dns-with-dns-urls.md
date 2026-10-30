---
id: BL-4150
title: Extension: Query DNS with dns:// URLs
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092, BL-4336, BL-4337, BL-4338, BL-4339]
touches: [Curl.Extension.DnsQuery.UnitLibrary, Curl.Extension.DnsQuery.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed: 2026-10-30
---
# BL-4150 — Extension: Query DNS with dns:// URLs

## Goal

Curl provides `dns://[server]/name?type=A` over UDP, TCP, DoH, DoT or DoQ, printed as text or JSON, as the extension `Curl.Extension.DnsQuery.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 50 of 63. Specification: RFC 1035, RFC 4501. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.DnsQuery.UnitLibrary` and `Curl.Extension.DnsQuery.UnitTests` exist in `/Extensions/DnsQuery/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `dns://[server]/name?type=A` over UDP, TCP, DoH, DoT or DoQ, printed as text or JSON.
- [x] Unit tests check its wire behaviour against named sections of RFC 1035, RFC 4501, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` a URL with its scheme fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 12): Split for size before any code, as the Notes above ask: five transports, a full RFC 1035 codec for every record type and two output formats are more than one run. BL-4336 (projects, wire codec, text and JSON rendering), BL-4337 (dns:// scheme over UDP and TCP, registration, `CURL_EXTENSIONS=0`, ADR), BL-4338 (DoT and DoQ), BL-4339 (DoH; after BL-4338 since both add to the transport selection file). Prerequisites checked: `ProtocolHandlerServices` gives the connector, datagram connector, TLS provider and resolver; registration follows `CoapExtension` (`ExtensionCatalogue.cs` plus `ExtensionDescriptionCatalogue.cs`, both shared list files); `Curl.Quic.UnitLibrary` and `Curl.Networking.UnitLibrary` are shared layers an extension may reference (EncryptedDns does); DoH needs its own minimal HTTP/1.1 client since an extension cannot reference `Curl.Protocol.Http`. No upstream cases, so no case runner work. Once those are Done this task needs only its criteria checked against their work.

- 2026-10-30 (lane 9): Verified only; no code changed in this run. The four subtasks delivered every criterion: BL-4336 (projects in `/Extensions/DnsQuery/`, RFC 1035 codec, text and JSON), BL-4337 (dns:// over UDP and TCP, RFC 4501 query, registration line `ExtensionCatalogue.cs:34`, description in `ExtensionDescriptionCatalogue.cs`, `CURL_EXTENSIONS=0` test, ADR-1140), BL-4338 (DoT, DoQ, ADR-1143), BL-4339 (DoH, ADR-1148). Each subtask ran `Measure-CodeQuality.ps1` with 0 failing members; the last code change to the library since then (b79175cc5) only renumbered ADR citations in comments. This run: `dotnet build` clean (0 warnings) with `VerifyExtensionIsolation`, fast tests 110 assemblies passed, 0 failed (214 in Curl.Extension.DnsQuery.UnitTests); the tests use fake connections only, so they do not depend on the platform.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: e386bf2a 576087e8 01612e53 bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split for size into BL-4336, BL-4337, BL-4338, BL-4339; waits on them
- 2026-10-30: Backlog -> Doing. Criteria at claim: e386bf2a 576087e8 01612e53 bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-30: Doing -> Done. dns:// URLs query DNS over UDP, TCP, DoT, DoQ and DoH and print text or JSON, as the dns extension
