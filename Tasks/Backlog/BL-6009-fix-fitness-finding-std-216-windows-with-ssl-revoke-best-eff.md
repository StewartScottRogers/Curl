---
id: BL-6009
title: Fix fitness finding STD-216: Windows: with --ssl-revoke-best-effort, a certificate listed on an expired CRL is accepted below an intermediate anchor
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.AnchorRevocation.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6009 — Fix fitness finding STD-216: Windows: with --ssl-revoke-best-effort, a certificate listed on an expired CRL is accepted below an intermediate anchor

## Goal

STD-216 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: Windows: with --ssl-revoke-best-effort, a certificate listed on an expired CRL is accepted below an intermediate anchor. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

Shared setup: These findings share one harness. A self-signed `root.pem` signs every intermediate: `intOC3.pem` (OCSP tests), `intCR3.pem` (CRL tests), and name-constraint CAs `ncExclEmail5.pem` (critical, excluded rfc822Name `other.org`), `ncExclEmail.pem`, `ncPermEmail.pem` (permitted rfc822Name `example.com`), `ncExclUpn.pem` (excluded otherName UPN `evil.test`) and `ncPermUpn.pem` (permitted UPN `evil.test`). Plain HTTP servers on `127.0.0.1:21380`–`21382` serve CRLs and answer OCSP; each leaf has SAN `DNS:localhost, IP:127.0.0.1, IP:::1` unless stated and is served by its own TLS server answering `hello <leaf>`. Every leaf is in the Windows test only under the intermediate anchor named; **with `--cacert root.pem` (the Windows chain engine) the port matches curl in every case**. **Windows caches fetched CRL and OCSP responses for all processes**: before each run, delete every cached `127.0.0.1:213xx` URL (`certutil -urlcache <url> delete`), or REF's cached answers leak into the port's run. The reviewer's generators are `gen3.py`/`gen4.py`/`gen5.py` and the comparison script `cmp.sh`.

The finding, verbatim:

- **Severity:** High · **Confidence:** Confirmed (3 runs; re-verified by the orchestrator) · **Area:** HTTPS CRL revocation, Windows build, intermediate anchor. Next to last round's STD-189 and STD-197 fixes.
- **Certificates:** `intCR3.pem` under `root.pem`. Leaves `k_expired_rev` and `k_expired_good`: CDP `http://127.0.0.1:21380/n/expired.crl`, a CRL from intCR3 with thisUpdate now−10 days and **nextUpdate now−3 days**, which lists `k_expired_rev`.
- **Repro:** `curl -sS --cacert intCR3.pem --ssl-revoke-best-effort https://localhost:<port of k_expired_rev>/`
- **Expected (REF):** `curl: (60) schannel: trust for this certificate or one of the certificates in the certificate chain has been revoked`, exit 60.
- **Actual (.NET):** `hello k_expired_rev`, exit 0. Without `--ssl-revoke-best-effort`, and in the `--proxy-cacert` form, REF says "has been revoked" and the port "the revocation status is unknown" (both exit 60). Control `k_expired_good`: 60 "unknown" without best-effort and 0 with it, in both.
- **Likely location:** `Curl.Networking.UnitLibrary/ServerCertificateVerification.AnchorRevocation.cs`: `IsCurrentListOf` requires `IsUnexpiredAt`, so the expired list becomes Unresolved, which best-effort tolerates. Commit `7d8ac1946` already applies "an expired list that lists the leaf still revokes" to delta CRLs (STD-189), but not to base CRLs.
- **Acceptance check:** the repro exits 60 "…has been revoked"; `k_expired_good` with `--ssl-revoke-best-effort` still exits 0.

## Acceptance criteria

- [ ] the repro exits 60 "…has been revoked"; `k_expired_good` with `--ssl-revoke-best-effort` still exits 0.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
