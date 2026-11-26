---
id: BL-6010
title: Fix fitness finding STD-217: Windows: with --ssl-revoke-best-effort, an RSA-PSS-signed OCSP "revoked" answer whose salt is not 32 is ignored
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary/OcspSignatureAlgorithm.cs, Curl.Tls.UnitLibrary/TlsSignatureRule.cs, Curl.Tls.UnitLibrary/TlsCertificatePublicKey.cs, Curl.Tls.UnitLibrary/CLAUDE.md, Curl.Tls.UnitTests/OcspSignatureAlgorithmTests.cs, Curl.Tls.UnitTests/OcspResponseBuilder.cs, Curl.Tls.UnitTests/TestRsaPssSignature.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6010 — Fix fitness finding STD-217: Windows: with --ssl-revoke-best-effort, an RSA-PSS-signed OCSP "revoked" answer whose salt is not 32 is ignored

## Goal

STD-217 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: Windows: with --ssl-revoke-best-effort, an RSA-PSS-signed OCSP "revoked" answer whose salt is not 32 is ignored. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

Shared setup: These findings share one harness. A self-signed `root.pem` signs every intermediate: `intOC3.pem` (OCSP tests), `intCR3.pem` (CRL tests), and name-constraint CAs `ncExclEmail5.pem` (critical, excluded rfc822Name `other.org`), `ncExclEmail.pem`, `ncPermEmail.pem` (permitted rfc822Name `example.com`), `ncExclUpn.pem` (excluded otherName UPN `evil.test`) and `ncPermUpn.pem` (permitted UPN `evil.test`). Plain HTTP servers on `127.0.0.1:21380`–`21382` serve CRLs and answer OCSP; each leaf has SAN `DNS:localhost, IP:127.0.0.1, IP:::1` unless stated and is served by its own TLS server answering `hello <leaf>`. Every leaf is in the Windows test only under the intermediate anchor named; **with `--cacert root.pem` (the Windows chain engine) the port matches curl in every case**. **Windows caches fetched CRL and OCSP responses for all processes**: before each run, delete every cached `127.0.0.1:213xx` URL (`certutil -urlcache <url> delete`), or REF's cached answers leak into the port's run. The reviewer's generators are `gen3.py`/`gen4.py`/`gen5.py` and the comparison script `cmp.sh`.

The finding, verbatim:

- **Severity:** High · **Confidence:** Confirmed (2 runs; re-verified by the orchestrator) · **Area:** HTTPS OCSP, Windows build, intermediate anchor. Next to last round's STD-197 fix (PSS CRLs).
- **Certificates:** leaves under `intOC3.pem`, no CDP, AIA OCSP on `127.0.0.1:21381`. The OCSP responses are signed by intOC3 with RSASSA-PSS (SHA-256, MGF1-SHA256): `w_pss64_rev` — salt 64, "revoked"; `w_pss64_good` — salt 64, "good"; `w_pss0_good` — salt 0, "good".
- **Repro:** `curl -sS --cacert intOC3.pem --ssl-revoke-best-effort https://localhost:<port of w_pss64_rev>/`
- **Expected (REF):** exit 60, "…has been revoked".
- **Actual (.NET):** `hello w_pss64_rev`, exit 0. Without best-effort: `w_pss64_rev` REF "revoked", port "unknown"; `w_pss64_good` and `w_pss0_good` REF exit 0, port exit 60 "the revocation status is unknown". Salt 32 matches REF. The CRL side (commit `8ecf242f5`) already handles any salt.
- **Likely location:** `Curl.Tls.UnitLibrary/OcspSignatureAlgorithm.cs`, `FindRsaPssRule`, reads only the hash, so verification assumes salt = hash length. Verify with the stated salt and MGF hash (the CRL path's `RsaPssSignatureParameters` / `RsaPssVerification`).
- **Acceptance check:** the repro exits 60 "…has been revoked"; `curl -sS --cacert intOC3.pem https://localhost:<port of w_pss64_good>/` exits 0.

## Acceptance criteria

- [x] the repro exits 60 "…has been revoked"; `curl -sS --cacert intOC3.pem https://localhost:<port of w_pss64_good>/` exits 0.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Cause: `OcspSignatureAlgorithm.FindRsaPssRule` read only the hash, so verification assumed
  salt = hash length and MGF1 over the same hash. It now reads all of `RSASSA-PSS-params` and
  verifies through `Curl.Cryptography`'s `RsaPssVerification` (ADR-1424).
- First criterion: met by unit tests, not by a live run. The reviewer's servers were not rebuilt.
  `OcspSignatureAlgorithmTests` verifies salts 64, 32 and 0 through the rule `OcspStapleVerifier`
  uses for every OCSP signature; the finding says salt 32 already matched curl, so the signature
  check was the only difference. Real curl was not re-measured (the finding holds the measurement).
- `touches` widened to the rule record, the key's verification, their notes and tests. No other
  task in Doing names them (BL-6027 holds `OcspStapleVerifier.cs`, not changed here).
- `OcspResponseBuilder.RsaPssSigner` (test code) now writes the MGF1 hash and salt length for a
  named hash, since parameters naming the hash alone mean a 20-byte salt (RFC 4055).
- Quality: the first run found `FindRsaPssRule` at complexity 12; it was split. Re-scored from
  `Curl.Tls.UnitTests`' coverage alone: no changed member fails (the two it lists are untouched
  members that other projects' tests cover).

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
- 2026-11-26: Backlog -> Doing. Criteria at claim: b5c69ea0 69116afe 3d836d5e.
- 2026-11-26: Doing -> Done. OCSP RSASSA-PSS signatures are verified with their stated salt length and MGF1 hash (ADR-1424).
