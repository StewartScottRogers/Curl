---
id: BL-4336
title: DnsQuery: project, RFC 1035 message codec and text and JSON rendering
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.DnsQuery.UnitLibrary/Wire, Curl.Extension.DnsQuery.UnitLibrary/Rendering, Curl.Extension.DnsQuery.UnitLibrary/Curl.Extension.DnsQuery.UnitLibrary.csproj, Curl.Extension.DnsQuery.UnitLibrary/CLAUDE.md, Curl.Extension.DnsQuery.UnitTests/Wire, Curl.Extension.DnsQuery.UnitTests/Rendering, Curl.Extension.DnsQuery.UnitTests/Curl.Extension.DnsQuery.UnitTests.csproj, Curl.slnx]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4336 — DnsQuery: project, RFC 1035 message codec and text and JSON rendering

## Goal

`Curl.Extension.DnsQuery.UnitLibrary` and `Curl.Extension.DnsQuery.UnitTests` exist in `/Extensions/DnsQuery/` in `Curl.slnx`, with an RFC 1035 message codec for a query of any QTYPE and QCLASS and an answer of any record type, rendered as text (dig-like presentation format) or JSON.

## Context

Split from BL-4150 (extension 50 of 63, ADR-0999, CLAUDE.md "Extensions"). Tested against RFC 1035 and RFC 4501, not against curl; never changes curl's behaviour. Never reference a `Curl.Protocol.*` library or another extension (in particular not `Curl.Extension.EncryptedDns`, whose DoT/DoQ resolvers and wire codec are a model to follow but must be written here); `Curl.Networking.UnitLibrary` and `Curl.Quic.UnitLibrary` are shared layers it may reference, as EncryptedDns does.

Create the projects with the `new-project` skill (ADR-1003: one single-line `<Folder>` under the marker comment in `Curl.slnx`). Wire: header (section 4.1.1), question (4.1.2), resource records (4.1.3), name compression (4.1.4), presentation of A, AAAA, CNAME, NS, PTR, MX, TXT, SOA, SRV (RFC 2782), CAA and unknown types as RFC 3597 `\# len hex`. Type and class mnemonics both ways (`A` <-> 1, `IN` <-> 1, `TYPE65`). Rendering: `Rendering/` text and JSON writers (System.Text.Json writer only, no serializer reflection - AOT).

## Acceptance criteria

- [x] `Curl.Extension.DnsQuery.UnitLibrary` and `Curl.Extension.DnsQuery.UnitTests` exist in `/Extensions/DnsQuery/` in `Curl.slnx` and the build's `VerifyExtensionIsolation` passes.
- [x] `Wire/` encodes a query for any type and class and decodes header, question, answer, authority and additional sections with compression pointers; malformed input (pointer loops, truncation, label over 63, name over 255) gives a failure, never an exception escaping or a hang; unit tests name the RFC 1035 sections they check.
- [x] `Rendering/` writes a decoded message as text and as JSON; unit tests pin both for each record type listed under Context.
- [x] The library meets the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Not registered in `ExtensionCatalogue.cs` yet: BL-4337 adds the scheme and registration.
- Plan (done in-session, no subagents, to stay inside the run's cost): `Wire/` holds `DnsQueryEncoder`, `DnsMessageDecoder`, `DnsMessageReader` (ref struct; a compression pointer must point before the labels that hold it, so loops fail at once), `DnsRecordDataDecoder` (one table entry per type), `DnsMnemonics`, `DnsPresentation` and the records; `Rendering/` holds `DnsTextRenderer` (dig-like) and `DnsJsonRenderer` (`Utf8JsonWriter`, relaxed escaping). Decisions recorded in ADR-1124.
- The library references only `Curl.Extension.Abstractions.UnitLibrary` (for BL-4337); it needs no shared layer yet. Build's `VerifyExtensionIsolation` passes with the full `dotnet build`.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Extension.DnsQuery.UnitLibrary` gives 100% line, 100% branch, max complexity 10, no failing member; 86 tests. Full fast run green on Windows; the tests touch no file system, path, OS error text or certificate, so they are platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 1eb1b74c cc8db1cc e2b1bc3f f4f7f560 3d836d5e.
- 2026-10-29: Doing -> Done. Curl.Extension.DnsQuery has an RFC 1035 query encoder, message decoder with safe compression, and dig-like text and JSON renderers for A, AAAA, NS, CNAME, PTR, MX, TXT, SOA, SRV, CAA and RFC 3597 unknown types
