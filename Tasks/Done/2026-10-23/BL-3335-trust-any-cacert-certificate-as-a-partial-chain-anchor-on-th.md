---
id: BL-3335
title: Trust any --cacert certificate as a partial-chain anchor on the OpenSSL build
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.PartialChain.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.LeafAnchor.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3335 — Trust any --cacert certificate as a partial-chain anchor on the OpenSSL build

## Goal

On the OpenSSL build, a `--cacert` file holding only the server's leaf (or only an intermediate of its chain) verifies that server, as curl's OpenSSL backend does with `X509_V_FLAG_PARTIAL_CHAIN` set by default (and does not under `--ssl-no-partialchain`).

## Context

Follow-up from BL-3289 (ADR-0797), which made the Schannel build accept a leaf listed in `--cacert`. curl's `lib/vtls/openssl.c` sets `X509_V_FLAG_PARTIAL_CHAIN` unless `--ssl-no-partialchain` or `--crlfile` is given, so any certificate in the CA file, leaf or intermediate, anchors the chain. Measure with Record-CurlExchange.ps1 on Linux (OpenSSL curl) before pinning; extend `SchannelTrustsListedLeaf` in `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs` to the OpenSSL build's wider rule.

## Acceptance criteria

- [x] A unit test in Curl.Networking.UnitTests pins that, on the OpenSSL build, a `--cacert` file holding only the server's leaf or only an intermediate verifies that server, as measured from real OpenSSL curl.
- [x] The build is clean and the fast tests pass.

## Notes

Measured curl 8.18.0 (OpenSSL 3.5.5, WSL): leaf, intermediate or root alone in --cacert all exit 0. ADR-0798. LeafAnchor test file added to touches (its OpenSSL leaf test pinned exit 60). Fast tests green; Measure-CodeQuality clean for Curl.Networking.UnitLibrary. Platform-neutral tests.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: c58749b7 c46ca32a.
- 2026-10-23: Doing -> Done. OpenSSL build trusts any listed chain certificate; measured, ADR-0798
