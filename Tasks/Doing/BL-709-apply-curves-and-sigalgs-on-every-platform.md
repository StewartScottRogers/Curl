---
id: BL-709
title: Apply --curves and --sigalgs on every platform
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-618, BL-708]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-709 — Apply --curves and --sigalgs on every platform

## Goal

`--curves <list>` restricts the key-exchange groups and `--sigalgs <list>` the signature algorithms offered in the handshake, on Windows, Linux and macOS, with curl 8.21.0's syntax (OpenSSL-style colon lists), its failure for an unknown name, and its failure when the server shares nothing.

## Context

- Conformance audit 2026-09-28, row 18; standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). curl: `--curves` "Set specific curves to use during SSL session establishment according to RFC 8422, 5.1." (https://curl.se/docs/manpage.html, checked 2026-09-28).
- Parsing: BL-618. How each platform carries it (`SslStream` cannot restrict groups per connection; the hand-built client can): BL-617's ADR; the routing row is added to BL-708's function.
- Measure with an OpenSSL build of curl through `Record-CurlExchange.ps1 -Tls -k`: `--curves X25519`, `--curves bogus`, `--sigalgs ECDSA+SHA256`, `--sigalgs bogus`, and a server limited to P-384 with `--curves X25519`; stderr and exit code; also the ClientHello groups and signature algorithms captured with `-NoServer`.

## Acceptance criteria

- [ ] Measured first as above; copied into Notes.
- [ ] Tests pin the ClientHello `supported_groups`/`key_share` and `signature_algorithms` for each option value, and the measured failures, on every platform (no `OSCondition` refusal).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-30: Backlog -> Doing.
