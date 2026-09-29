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
completed: 2026-09-28
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

- [x] `Curl.Tls.UnitTests` rebuild each of ADR-0140's three captured ClientHellos (the LibreSSL 4.2.1, Schannel and OpenSSL 3.5.5 hex, with the OpenSSL X25519MLKEM768 share filled with a test value of the right length) from its profile with the random, session ID and key shares injected, byte for byte.
- [x] Each new extension codec round-trips and rejects a truncated length with `decode_error`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Design: `ClientHelloProfile` is a sealed record of the measured data (record version, suites, extension order, supported versions, groups, key share groups, signature algorithms, ALPN, point formats, PSK modes, certificate compression). `Build(host, random, sessionId, keyShares)` makes the `ClientHello`; `EncodeRecord` adds the record header with the profile's record version (0x0301 for Schannel and OpenSSL, 0x0303 for LibreSSL). Each extension type maps to one encoder in a static table, so the build stays at complexity 1; an order naming a type no encoder builds throws `InvalidOperationException`. Options later change lists with `with`; the "append an extension the profile lacks before `pre_shared_key`" rule is left to the option tasks that add one (BL-708 onwards), which is where it first applies.
- `ClientHelloProfileTests` takes the random, session ID and key shares from each capture (decoded with BL-698's codecs) and compares the full record hex. The OpenSSL test fills the 1216-byte X25519MLKEM768 share with bytes 0..255 repeating and checks the result is 1569 bytes, the measured length.
- New codecs: `EcPointFormatsExtension`, `SessionTicketExtension`, `ExtendedMasterSecretExtension`, `EncryptThenMacExtension`, `PostHandshakeAuthExtension`, `CompressCertificateExtension`, `CertificateAuthoritiesExtension`, `SrpExtension` (built here, small and codec-only; BL-704 uses it for the SRP exchange). ECH stays BL-706's.
- Truncation criterion: `session_ticket` (ticket is the whole data) and the three empty extensions have no inner length to truncate. The empty ones answer any data with `decode_error`; `session_ticket` accepts any bytes, as RFC 5077 defines it. Every codec with a length prefix is checked for truncation and trailing bytes.
- The capture's `compress_certificate` is `0001 0003` (zlib, zstd); ADR-0140's prose says zlib, brotli, zstd. The profile follows the bytes; BL-846 corrects the ADR (outside this task's `touches`).
- Pipeline: the plan was small enough (data plus codecs mirroring BL-698's) to write directly against ADR-0140 rather than through a separate architect pass; gates run: build with `-warnaserror`, all fast tests (Curl.Tls 746 pass), `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` 100% line, 100% branch, 0 failing, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. ClientHelloProfile.Schannel, OpenSsl and LibreSsl rebuild ADR-0140's captured hellos byte for byte, with typed codecs for the eight extensions they and ADR-0140 still needed
