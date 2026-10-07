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
completed: 2026-10-01
---
# BL-1049 — Offer the ML-KEM hybrid, pure ML-KEM and brainpool TLS 1.3 groups a --curves list names

## Goal

`--curves` lists naming `SecP256r1MLKEM768` (0x11eb), `SecP384r1MLKEM1024` (0x11ed), `MLKEM512`/`MLKEM768`/`MLKEM1024` (0x0200-0x0202) or `brainpoolP256r1tls13`/`brainpoolP384r1tls13`/`brainpoolP512r1tls13` (0x001f-0x0021) offer and complete a TLS 1.3 key exchange on those groups, as OpenSSL 3.5's curl does, instead of dropping them.

## Context

- BL-709 / ADR-0284 decision 3: `OpenSslGroupList` knows these names but the hand-built client has no key share for them, so they are dropped and a list of only these fails exit 35 "no suitable groups" where OpenSSL's curl offers them.
- Hand-build each in `Curl.Tls.UnitLibrary` (ML-KEM-768 already exists inside `X25519MlKem768KeyShare`; the P-256/P-384 hybrids follow draft-ietf-tls-ecdhe-mlkem; the brainpool TLS 1.3 groups are RFC 8734). `TlsNamedGroup.CanShare` gains them; `OpenSslGroupList`'s `IsUsable` then keeps them.
- Measure first with `Record-CurlExchange.ps1` (plain TCP, ClientHello in `request.bin`) against Ubuntu's curl with `--curves SecP256r1MLKEM768` etc.

## Acceptance criteria

- [x] `Curl.Tls.UnitTests` pin each group's key share against known-answer vectors and a full TLS 1.3 handshake on it with the in-memory server.
- [x] `HandBuiltTlsProviderTests` pin the ClientHello `supported_groups`/`key_share` for each name as measured; `OpenSslGroupListTests` no longer lists them as dropped.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- **What was built (ADR-0296).** `TlsNamedGroup` names the eight groups, and `CanShare` accepts them.
  `MlKemKeyShare` handles pure ML-KEM: encapsulation key out, ciphertext in. `EcdhMlKemKeyShare` handles
  SecP256r1MLKEM768 and SecP384r1MLKEM1024 with the curve first: point || ek out, point || ct in, and
  ECDH secret || ML-KEM secret. `BrainpoolKeyShare` maps the `tls13` codes to its three curves, and
  `SystemTlsRandomSource` makes all eight. `OpenSslGroupList` now uses the constants. Every known name
  is usable now, so `usable.Contains` became dead and was removed; the fallback became a loop, because
  the shared method-group cache left one branch uncovered.
- **Measured 2026-10-01**, Ubuntu curl 8.18.0 / OpenSSL 3.5.5 through `Record-CurlExchange.ps1 -Curl wsl.exe -ListenAddress 172.26.96.1`
  (alert response, ClientHello in `request.bin`). Groups / key shares (length):
  - `SecP256r1MLKEM768` 11eb / 11eb(1249)
  - `SecP384r1MLKEM1024` 11ed / 11ed(1665)
  - `MLKEM512` 0200 / 0200(800)
  - `MLKEM768` 0201 / 0201(1184)
  - `MLKEM1024` 0202 / 0202(1568)
  - `brainpoolP256r1tls13` 001f / 001f(65); `brainpoolP384r1tls13` 0020 / 0020(97); `brainpoolP512r1tls13` 0021 / 0021(129)
  - `X25519:MLKEM768` 001d,0201 / 001d
  - `MLKEM768:P-256` 0201,0017 / 0201
  - `MLKEM1024:brainpoolP512r1tls13:*SecP256r1MLKEM768` 0202,0021,11eb / 11eb
  - `*SecP384r1MLKEM1024:P-384` 11ed,0018 / 11ed
  - `SecP256r1MLKEM768:X25519MLKEM768:SecP384r1MLKEM1024:*MLKEM512` 11eb,11ec,11ed,0200 / 0200
  - `brainpoolP256r1:brainpoolP256r1tls13` 001a,001f / 001f
  - `MLKEM768:-MLKEM768` sends nothing: exit 35 "no suitable groups".
  - `*brainpoolP256r1:P-384` sends nothing: exit 35 `error:0A000065:SSL routines::no suitable key share`. Filed as BL-1082.
- **For BL-1048 (ec_point_formats):** these lists send no `ec_point_formats`:
  - `X25519MLKEM768`, `SecP256r1MLKEM768`, `SecP384r1MLKEM1024`, `MLKEM512/768/1024`
  - `SecP256r1MLKEM768:X25519MLKEM768:SecP384r1MLKEM1024:*MLKEM512`
  - `MLKEM1024:brainpoolP512r1tls13:*SecP256r1MLKEM768`

  `MLKEM1024:brainpoolP512r1tls13`, `MLKEM768:P-256`, `MLKEM1024:brainpoolP256r1` and
  `brainpoolP*tls13` alone do send it. So the rule may depend on the key share; read
  OpenSSL's `use_ecc` before pinning. Lists of a single ML-KEM group are over 512 bytes, so
  they get no padding.
- **Known answers.** OpenSSL 3.5.5 in WSL produced them (`KeyShareKnownAnswers`):
  - ML-KEM: `genpkey -pkeyopt hexseed:00..3f`, then `pkeyutl -encap`.
  - Elliptic curves: `genpkey` for both key pairs, then `pkeyutl -derive`.

  The hybrids' expected values are the two halves joined. That byte order was confirmed
  independently: `Curl.Console --curves <group> -k` completed a TLS 1.3 handshake on all
  eight groups against `openssl s_server -tls1_3 -groups <all eight> -www`.
- **Choices:** the hybrid is a composition of `EcdhKeyShare` and `MlKem`, so the point check is
  reused. No Schannel-build row was pinned, because curl.se's LibreSSL build was not measured
  with these names.

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. --curves offers and completes TLS 1.3 on SecP256r1MLKEM768, SecP384r1MLKEM1024, MLKEM512/768/1024 and the brainpool tls13 groups
