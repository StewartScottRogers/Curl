---
id: BL-4316
title: MASQUE extension wire format: URI templates, upgrade requests, CONNECT-IP capsules and IP packets
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Masque.UnitLibrary, Curl.Extension.Masque.UnitTests, Curl.slnx]
requirement: none
created: 2026-10-29
completed:
---
# BL-4316 — MASQUE extension wire format: URI templates, upgrade requests, CONNECT-IP capsules and IP packets

## Goal

`Curl.Extension.Masque.UnitLibrary` exists with the byte-level MASQUE wire format of RFC 9298 (CONNECT-UDP) and RFC 9484 (CONNECT-IP), tested with no network.

## Context

First of BL-4144's split (wire format, then client flow BL-4317, then the option in BL-4144). Extension rules: ADR-0999. The library references only `Curl.Protocol.Abstractions.UnitLibrary`, `Curl.Extension.Abstractions.UnitLibrary` and the shared `Curl.HttpDatagram.UnitLibrary` (RFC 9297 capsules and DATAGRAM capsule framing, ADR-1004), never a `Curl.Protocol.*` library or another extension. Add the project pair with the `new-project` skill, in `/Extensions/Masque/` in `Curl.slnx` (ADR-1003). Curl's own CONNECT-UDP request for `-x` (ADR-0250, `Curl.Networking`'s `HttpProxyTunnel.BuildConnectUdpRequest`) is a reference, but is not referenced. Planned transport: HTTP/1.1 Upgrade (RFC 9298 section 3.2, RFC 9484 section 4.5), since HTTP/2 and HTTP/3 extended CONNECT live in protocol libraries an extension cannot reference.

## Acceptance criteria

- [ ] `Curl.Extension.Masque.UnitLibrary` and `Curl.Extension.Masque.UnitTests` exist in `/Extensions/Masque/` in `Curl.slnx` and the build's `VerifyExtensionIsolation` passes.
- [ ] A URI template is expanded with `target_host` and `target_port` (RFC 9298 section 2) and with `target` and `ipproto` (RFC 9484 section 3), values percent-encoded as RFC 6570 simple expansion does (`::1` is `%3A%3A1`); a template naming neither pair is refused; unit tests.
- [ ] The HTTP/1.1 upgrade request (`GET`, `Connection: Upgrade`, `Upgrade: connect-udp` or `connect-ip`, `Capsule-Protocol: ?1`) and the reply check (`101` opens; anything else refused with its status) match RFC 9298 section 3.2 and 3.3 and RFC 9484 section 4.5 and 4.6; unit tests.
- [ ] ADDRESS_ASSIGN, ADDRESS_REQUEST and ROUTE_ADVERTISEMENT capsules (RFC 9484 section 4.7) encode and decode, and a malformed one is refused; unit tests.
- [ ] An IPv4 and an IPv6 packet carrying one UDP datagram are built with correct header and UDP checksums, and an incoming packet's UDP payload, source and destination are read back, other protocols ignored (RFC 9484 section 6, RFC 768, RFC 791, RFC 8200); unit tests.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
