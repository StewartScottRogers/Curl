---
id: BL-3305
title: Ignore an unknown --curves value in the Schannel build profile as curl 8.22.0 Schannel does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/CurvesAndSignatureAlgorithms.cs, Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs, Curl.Networking.UnitLibrary/TlsFailureMessages.cs, Curl.Networking.UnitLibrary/CLAUDE.md, Curl.Networking.UnitTests/HandBuiltTlsProviderTests.CurvesAndSignatureAlgorithms.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3305 — Ignore an unknown --curves value in the Schannel build profile as curl 8.22.0 Schannel does

## Goal

With the Schannel build profile, --curves with a value Schannel does not know is ignored and the transfer goes on, as curl 8.22.0 Schannel does.

## Context

Found by BL-3282. Measured: real curl 8.22.0 Schannel (Git ucrt64) with -sSk --curves nope against a TLS 1.2 recorder (-Tls) exits 0 and prints the body; Curl exits 59 "failed setting curves list: 'nope'" (CurvesAndSignatureAlgorithms.Apply, TlsFailureMessages.CurvesListRefused). ADR-0151 and ADR-0284 chose to honour --curves on every platform with the OpenSSL build's text, measured against an earlier reference; the reference is now the Schannel build, which ignores the curves list. Measure what Schannel does with valid curves (X25519 works on both) and with --sigalgs, decide in an ADR that supersedes the relevant part of ADR-0151/0284, and change the HandBuiltTlsProviderTests rows for the Schannel build.

## Acceptance criteria

- [x] curl -sSk --curves nope https://... with the Schannel build profile matches curl 8.22.0 Schannel's exit code and output, pinned by a unit test, with an ADR recording the decision.
- [x] The fast tests pass and Measure-CodeQuality.ps1 reports 0 failing members for each changed library.

## Notes

- Measured 2026-10-23 with Record-CurlExchange.ps1 -Tls and curl 8.22.0 Schannel, -sSk: --curves nope, X25519, P-256:nope and --sigalgs nope, ecdsa_secp256r1_sha256 all exit 0 with the body, so Schannel ignores both lists. ADR-0793 decides the Schannel build ignores both (supersedes the Schannel part of ADR-0151 and ADR-0284).
- Touches widened (no task in Doing on origin/work/dark-factory names them): HandBuiltTlsProvider.cs passes the build to Apply and its handshake-failure text no longer depends on the lists; TlsFailureMessages.cs loses the now-unused LibreSslHandshakeFailureAlert; the project CLAUDE.md states the new behaviour.
- Done as a direct change rather than the full feature pipeline (a one-branch change under the cost cap). Routing (ADR-0140) left as is: the hand-built client with the Schannel profile sends what Schannel curl sends.
- Fast tests green (0 failed across 45 test assemblies); Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary: 0 failing members. The tests are platform-neutral (in-memory handshakes, no paths or OS text), which meets the CI criterion here.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 96be729b 69edf767.
- 2026-10-23: Doing -> Done. With the Schannel build profile --curves and --sigalgs are ignored, so --curves nope completes as curl 8.22.0 Schannel does (ADR-0793)
