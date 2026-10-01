---
id: BL-1049
title: Offer the ML-KEM hybrid, pure ML-KEM and brainpool TLS 1.3 groups a --curves list names
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-30
completed:
---
# BL-1049 — Offer the ML-KEM hybrid, pure ML-KEM and brainpool TLS 1.3 groups a --curves list names

## Goal

`--curves` lists naming `SecP256r1MLKEM768` (0x11eb), `SecP384r1MLKEM1024` (0x11ed), `MLKEM512`/`MLKEM768`/`MLKEM1024` (0x0200-0x0202) or `brainpoolP256r1tls13`/`brainpoolP384r1tls13`/`brainpoolP512r1tls13` (0x001f-0x0021) offer and complete a TLS 1.3 key exchange on those groups, as OpenSSL 3.5's curl does, instead of dropping them.

## Context

- BL-709 / ADR-0284 decision 3: `OpenSslGroupList` knows these names but the hand-built client has no key share for them, so they are dropped and a list of only these fails exit 35 "no suitable groups" where OpenSSL's curl offers them.
- Hand-build each in `Curl.Tls.UnitLibrary` (ML-KEM-768 already exists inside `X25519MlKem768KeyShare`; the P-256/P-384 hybrids follow draft-ietf-tls-ecdhe-mlkem; the brainpool TLS 1.3 groups are RFC 8734). `TlsNamedGroup.CanShare` gains them; `OpenSslGroupList`'s `IsUsable` then keeps them.
- Measure first with `Record-CurlExchange.ps1` (plain TCP, ClientHello in `request.bin`) against Ubuntu's curl with `--curves SecP256r1MLKEM768` etc.

## Acceptance criteria

- [ ] `Curl.Tls.UnitTests` pin each group's key share against known-answer vectors and a full TLS 1.3 handshake on it with the in-memory server.
- [ ] `HandBuiltTlsProviderTests` pin the ClientHello `supported_groups`/`key_share` for each name as measured; `OpenSslGroupListTests` no longer lists them as dropped.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
