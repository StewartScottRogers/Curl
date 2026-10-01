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
completed:
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

- [ ] `HandBuiltTlsProviderTests` pin `*brainpoolP256r1:P-384` as exit 35 with the measured line, and each other measured value as measured.
- [ ] `OpenSslGroupListTests.Parse_WithATls12OnlyGroupStarred_SendsItNoKeyShare` is replaced by a test of the measured outcome.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
