---
id: BL-5008
title: Refuse a single-label wildcard certificate on the hand-built TLS route as curl does
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/HandBuiltCertificateVerifier.cs, Curl.Networking.UnitTests/HandBuiltCertificateVerifierTests.cs]
requirement: none
created: 2026-11-12
completed:
---
# BL-5008 — Refuse a single-label wildcard certificate on the hand-built TLS route as curl does

## Goal

The hand-built TLS route refuses a certificate whose only name is a single-label wildcard such as `*.test` (exit 60 with curl's message), while `*.a.test` still matches `x.a.test`.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip; REF = curl 8.22.0 Schannel, REF2 = curl 8.18.0 OpenSSL), finding STD-028 (Medium, Confirmed).

Area: HTTPS host-name verification; Windows default `--cacert` route, Linux build under `-v`. Weakens security (an attacker needs a trusted CA to issue a TLD-level wildcard).

Repro:
1. Issue a leaf signed by a test CA `ca.pem` with `subjectAltName=DNS:*.test` and a reachable CRL distribution point.
2. Serve it over HTTPS on 127.0.0.1:43402.
3. `curl -sS https://a.test:43402/ --resolve a.test:43402:127.0.0.1 --cacert ca.pem`
4. Also with `--ssl-no-revoke --curves P-256`, through `-x http://<any CONNECT proxy>`, and on Linux with `-v`.

Expected:
- REF exits 60 with `curl: (60) schannel: CertGetNameString() failed to match connection hostname (a.test) against server certificate names`.
- REF2 exits 60 with `SSL: no alternative certificate subject name matches target hostname 'a.test'`. curl requires at least two dots in a wildcard pattern.
- PORT's own SslStream route (`--ssl-no-revoke` alone) also exits 60.

Actual: exit 0 and the body is delivered on every variant. These patterns already behave like REF: `*.a.test` vs `x.a.test`, `*.test` vs `b.a.test`, `f*.test`, and `*`.

Where to start: `Curl.Networking.UnitLibrary/HandBuiltCertificateVerifier.cs`, `BuildChain` (about line 143) calls `sent[0].MatchesHostname(...)`, which applies .NET's wildcard rules. `ServerCertificateVerification.WithTheNameCheckCurlRuns` can only clear a mismatch, never add one. Fix: match the SAN DNS names with curl's rules; `SchannelCommonNameCheck.PatternMatchesHost` already implements the two-dot rule.

## Acceptance criteria

- [ ] A unit test in `Curl.Networking.UnitTests/HandBuiltCertificateVerifierTests.cs` (leaf with SAN `DNS:*.test`, target `a.test`) shows the verdict is Rejected with `CurlExitCode.PeerFailedVerification` (60).
- [ ] The Schannel profile's message is `schannel: CertGetNameString() failed to match connection hostname (a.test) against server certificate names` and the OpenSSL profile's is `SSL: no alternative certificate subject name matches target hostname 'a.test'`, each asserted in its own test.
- [ ] Regression: `x.a.test` against a `*.a.test` certificate is still accepted, and `*.test` vs `b.a.test`, `f*.test` and `*` still behave as today (refused).
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text. Both profiles share the hand-built route; the message differs per profile (Schannel on Windows, OpenSSL on Linux and macOS).

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 81536ade b6eefc54 2cefc5b3 a0b49a29.
