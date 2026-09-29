---
id: BL-787
title: Build the three measured ClientHello profiles and the extensions they still need
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-698]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-787 — Build the three measured ClientHello profiles and the extensions they still need

## Goal

`Curl.Tls.UnitLibrary` has `ClientHelloProfile.Schannel`, `ClientHelloProfile.OpenSsl` and `ClientHelloProfile.LibreSsl`, each producing the ClientHello ADR-0140 captured from that curl build byte for byte once the random, session ID and key shares are injected, with codecs for the extensions those hellos carry that BL-698 did not build.

## Context

- ADR-0140 ("Default ClientHello: the platform curl's, as measured", and "What the tasks rely on": BL-698's line) assigns the profiles to BL-698. BL-698 built the message codecs and the extension codecs its acceptance criteria listed (plus `record_size_limit`, `padding`, `renegotiation_info`); the profiles were split off here so BL-698 stayed one run.
- Extensions the three captured hellos carry with no typed codec yet: `ec_point_formats` (11), `session_ticket` (35), `post_handshake_auth` (49), `extended_master_secret` (23), `encrypt_then_mac` (22), `compress_certificate` (27). Also named in ADR-0140's supported list and still missing: `certificate_authorities` (47), `srp` (12, BL-704 may own it), ECH (BL-706 owns it).
- Build on BL-698's types: `ClientHello`, `TlsExtension`, `ServerNameExtension`, `KeyShareExtension` and the rest; the internal `TlsReader`/`TlsWriter` for new codecs.
- Options change a profile's lists, never its extension order; an extension the profile lacks goes after its last extension and before `pre_shared_key` (ADR-0140).

## Acceptance criteria

- [ ] `Curl.Tls.UnitTests` rebuild each of ADR-0140's three captured ClientHellos (the LibreSSL 4.2.1, Schannel and OpenSSL 3.5.5 hex, with the OpenSSL X25519MLKEM768 share filled with a test value of the right length) from its profile with the random, session ID and key shares injected, byte for byte.
- [ ] Each new extension codec round-trips and rejects a truncated length with `decode_error`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
