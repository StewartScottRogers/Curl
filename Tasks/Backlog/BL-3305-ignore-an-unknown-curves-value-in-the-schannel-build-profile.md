---
id: BL-3305
title: Ignore an unknown --curves value in the Schannel build profile as curl 8.22.0 Schannel does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/CurvesAndSignatureAlgorithms.cs, Curl.Networking.UnitTests/HandBuiltTlsProviderTests.CurvesAndSignatureAlgorithms.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3305 — Ignore an unknown --curves value in the Schannel build profile as curl 8.22.0 Schannel does

## Goal

With the Schannel build profile, --curves with a value Schannel does not know is ignored and the transfer goes on, as curl 8.22.0 Schannel does.

## Context

Found by BL-3282. Measured: real curl 8.22.0 Schannel (Git ucrt64) with -sSk --curves nope against a TLS 1.2 recorder (-Tls) exits 0 and prints the body; Curl exits 59 "failed setting curves list: 'nope'" (CurvesAndSignatureAlgorithms.Apply, TlsFailureMessages.CurvesListRefused). ADR-0151 and ADR-0284 chose to honour --curves on every platform with the OpenSSL build's text, measured against an earlier reference; the reference is now the Schannel build, which ignores the curves list. Measure what Schannel does with valid curves (X25519 works on both) and with --sigalgs, decide in an ADR that supersedes the relevant part of ADR-0151/0284, and change the HandBuiltTlsProviderTests rows for the Schannel build.

## Acceptance criteria

- [ ] curl -sSk --curves nope https://... with the Schannel build profile matches curl 8.22.0 Schannel's exit code and output, pinned by a unit test, with an ADR recording the decision.
- [ ] The fast tests pass and Measure-CodeQuality.ps1 reports 0 failing members for each changed library.

## Notes

## Log

- 2026-10-23: Created.
