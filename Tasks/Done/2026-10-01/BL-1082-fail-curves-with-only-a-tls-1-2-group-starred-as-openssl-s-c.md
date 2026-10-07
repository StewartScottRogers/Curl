---
id: BL-1082
title: Fail --curves with only a TLS 1.2 group starred as OpenSSL's curl does: exit 35 no suitable key share
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1082 — Fail --curves with only a TLS 1.2 group starred as OpenSSL's curl does: exit 35 no suitable key share

## Goal

`--curves '*brainpoolP256r1:P-384'` (a starred group that only TLS 1.2 can use, and no other starred group) fails before a byte is sent with exit 35 `TLS connect error: error:0A000065:SSL routines::no suitable key share`, as Ubuntu's curl 8.18.0 with OpenSSL 3.5.5 does, instead of quietly sharing the first TLS 1.3 group.

## Context

- Measured 2026-10-01 (BL-1049) with `Record-CurlExchange.ps1 -Curl wsl.exe -ListenAddress <host>` and Ubuntu's curl: `--curves '*brainpoolP256r1:P-384'` sends nothing and prints `curl: (35) TLS connect error: error:0A000065:SSL routines::no suitable key share`.
- Today `OpenSslGroupList.Parse` drops the starred brainpoolP256r1 from the key shares and falls back to the first group the client can share (P-384); `OpenSslGroupListTests.Parse_WithATls12OnlyGroupStarred_SendsItNoKeyShare` pins only that it gets no key share.
- Measure first: `*brainpoolP256r1:*P-384`, `*brainpoolP256r1:X25519`, and `--tls-max 1.2` with `*brainpoolP256r1`, to learn whether one usable starred group is enough and whether a TLS 1.2 ceiling avoids the failure.
- The message goes in `TlsFailureMessages` beside `OpenSslNoSuitableGroups`; `CurvesAndSignatureAlgorithms.Offer` returns it in the measured order.

## Acceptance criteria

- [x] `HandBuiltTlsProviderTests` pin `*brainpoolP256r1:P-384` as exit 35 with the measured line, and each other measured value as measured.
- [x] `OpenSslGroupListTests.Parse_WithATls12OnlyGroupStarred_SendsItNoKeyShare` is replaced by a test of the measured outcome.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports no failing member.

## Notes

- Measured 2026-10-01 with `Record-CurlExchange.ps1 -Curl wsl.exe -ListenAddress 172.26.96.1` (Ubuntu curl 8.18.0, OpenSSL 3.5.5), TLS 1.3 offered:
  - `*brainpoolP256r1:P-384`, `*brainpoolP256r1:X25519`, `brainpoolP256r1:*brainpoolP384r1:X25519` → 35 `no suitable key share`.
  - `*brainpoolP256r1:*P-384` → handshakes; key share [P-384] (one shareable starred group is enough).
  - `*brainpoolP256r1`, `brainpoolP256r1` → 35 `no suitable groups` (no group TLS 1.3 can use).
  - Order: no suitable groups, then `--sigalgs RSA+SHA1`'s no suitable signature algorithm, then no suitable key share.
  - `--tls-max 1.2` with any of them → ClientHello sent, groups offered (e.g. [0x1a, 0x18]), no failure.
- Done: `OpenSslGroupList.Parse` leaves key shares empty when every starred group is TLS 1.2-only; `CurvesAndSignatureAlgorithms.Offer` (now told whether TLS 1.3 is offered) fails with no suitable groups when TLS 1.3 is offered and no group can be shared, and with the new `TlsFailureMessages.OpenSslNoSuitableKeyShare` after the sigalgs check. Both builds fail alike, as for the other `--curves` failures (ADR-0284: the hand-built client applies `--curves` on every platform).
- Found and filed: BL-1086 (the combined TLS 1.3 ClientHello drops TLS 1.2-only groups from `supported_groups`; real curl offers them, so the existing `brainpoolP256r1:X25519` row is wrong) and BL-1087 (real curl writes an `internal_error` alert `15 03 01 00 02 02 50` before these failures; Curl writes nothing).
- Gates: build -warnaserror clean, fast tests green (Networking 2163 passed), Measure-CodeQuality 0 failing members.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. --curves with only TLS 1.2 groups starred fails with exit 35 no suitable key share when TLS 1.3 is offered, as OpenSSL's curl does
