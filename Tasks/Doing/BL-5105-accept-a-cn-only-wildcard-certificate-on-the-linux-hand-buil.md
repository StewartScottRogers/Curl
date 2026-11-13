---
id: BL-5105
title: Accept a CN-only wildcard certificate on the Linux hand-built TLS route
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/HandBuiltCertificateVerifier.cs, Curl.Networking.UnitTests/HandBuiltCertificateCommonNameWildcardTests.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5105 — Accept a CN-only wildcard certificate on the Linux hand-built TLS route

## Goal

On the Linux hand-built TLS route (used by `-v`, `--trace`, `--curves`), a certificate with subject `CN=*.a.test` and no subjectAltName is accepted for host `x.a.test`, as OpenSSL curl does, instead of failing with exit 60.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding STD-058 (Low, Confirmed twice). Fails closed. Area: Linux build, host-name check on the hand-built route.

**Repro (Linux).** Issue a leaf from `ca.pem` with subject `CN=*.a.test` and no subjectAltName; serve it on 127.0.0.1:22511. Run `curl -sS https://x.a.test:22511/ --resolve x.a.test:22511:127.0.0.1 --cacert ca.pem --curves P-256`, and the same with `-v` instead of `--curves`.

**Expected (OpenSSL curl on Linux).** Exit 0, body delivered. The Linux build without `-v`/`--curves` also exits 0, as does Windows.

**Actual.** `curl: (60) SSL: certificate subject name '*.a.test' does not match target hostname 'x.a.test'`, exit 60.

**Where to start.** `Curl.Networking.UnitLibrary/HandBuiltCertificateVerifier.cs`, `MatchesAsCurlDoes` (about line 153) returns false when .NET's `MatchesHostname` does. For a certificate with no DNS SAN, fall back to `SchannelCommonNameCheck.PatternMatchesHost(CN, host)`.

## Acceptance criteria

- [ ] New tests in `Curl.Networking.UnitTests/HandBuiltCertificateCommonNameWildcardTests.cs` prove that a certificate (generated in the test) with `CN=*.a.test` and no SAN matches host `x.a.test`.
- [ ] A test proves `CN=*.test` against `a.test` is still refused (the verifier returns false, the exit 60 path).
- [ ] A test proves a certificate that has a DNS SAN is still checked against the SAN only (a matching CN does not rescue a non-matching SAN).
- [ ] Tests are platform-neutral (they exercise the verifier type directly, not the OS TLS stack).
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check, as a hand check on Linux: both repro commands exit 0 with the body; `CN=*.test` against `a.test` still exits 60.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: c0cc8f3f 547e29c0 7d37dafb 021470d8 a0b49a29.
