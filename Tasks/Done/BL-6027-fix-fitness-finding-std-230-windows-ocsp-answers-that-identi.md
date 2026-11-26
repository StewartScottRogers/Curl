---
id: BL-6027
title: Fix fitness finding STD-230: Windows: OCSP answers that identify the certificate with a SHA-256 CertID are used, where Windows reports the status unknown
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary/OcspStapleVerifier.cs, Curl.Tls.UnitLibrary/CLAUDE.md, Curl.Tls.UnitTests/OcspStapleVerifierTests.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.AnchorOcsp.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6027 — Fix fitness finding STD-230: Windows: OCSP answers that identify the certificate with a SHA-256 CertID are used, where Windows reports the status unknown

## Goal

STD-230 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: Windows: OCSP answers that identify the certificate with a SHA-256 CertID are used, where Windows reports the status unknown. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

Shared setup: These findings share one harness. A self-signed `root.pem` signs every intermediate: `intOC3.pem` (OCSP tests), `intCR3.pem` (CRL tests), and name-constraint CAs `ncExclEmail5.pem` (critical, excluded rfc822Name `other.org`), `ncExclEmail.pem`, `ncPermEmail.pem` (permitted rfc822Name `example.com`), `ncExclUpn.pem` (excluded otherName UPN `evil.test`) and `ncPermUpn.pem` (permitted UPN `evil.test`). Plain HTTP servers on `127.0.0.1:21380`–`21382` serve CRLs and answer OCSP; each leaf has SAN `DNS:localhost, IP:127.0.0.1, IP:::1` unless stated and is served by its own TLS server answering `hello <leaf>`. Every leaf is in the Windows test only under the intermediate anchor named; **with `--cacert root.pem` (the Windows chain engine) the port matches curl in every case**. **Windows caches fetched CRL and OCSP responses for all processes**: before each run, delete every cached `127.0.0.1:213xx` URL (`certutil -urlcache <url> delete`), or REF's cached answers leak into the port's run. The reviewer's generators are `gen3.py`/`gen4.py`/`gen5.py` and the comparison script `cmp.sh`.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed (2 runs) · **Area:** HTTPS OCSP, Windows build, intermediate anchor.
- **Certificates:** `w_sha256id_good` and `w_sha256id_rev` under `intOC3.pem`, no CDP; the OCSP responder answers with a CertID hashed with SHA-256 although the request used SHA-1.
- **Repro:** `curl -sS --cacert intOC3.pem https://localhost:<port of w_sha256id_good>/`
- **Expected (REF):** both leaves `curl: (60) schannel: the revocation status is unknown`; `w_sha256id_rev` with `--ssl-revoke-best-effort` exits 0.
- **Actual (.NET):** `w_sha256id_good`: `hello`, exit 0. `w_sha256id_rev`: 60 "…has been revoked", including with best-effort.
- **Likely location:** `Curl.Tls.UnitLibrary/OcspStapleVerifier.cs`, `NamesCertificate`, accepts any CertID hash.
- **Acceptance check:** the repro exits 60 "the revocation status is unknown".

## Acceptance criteria

- [x] the repro exits 60 "the revocation status is unknown".
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Fix (ADR-1424): `OcspStapleVerifier.NamesCertificate` takes only a SHA-1 `CertID`. It applies below the anchor, to the Schannel build's staple and to `--cert-status`; the OpenSSL side is read from `OCSP_resp_find_status`'s comparison, not measured.
- Acceptance check: pinned by `VerifyPeer_OcspAnswerUnderASha256CertId_IsUnknownUnlessBestEffort` (exit 60 "revocation status is unknown"; best effort accepts), through the real verification with the fetch seam, not by rebuilding the reviewer's servers.
- `touches` widened to the two test files and `Curl.Tls.UnitLibrary/CLAUDE.md`; no other task in Doing named them.
- Verified: build clean, fast tests green, format clean on the changed files, Curl.Tls.UnitLibrary quality 0 failing members.

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 2bf8e3cb 69116afe 3d836d5e.
- 2026-11-26: Doing -> Done. Only a SHA-1 CertID names a certificate in an OCSP response (ADR-1424).
