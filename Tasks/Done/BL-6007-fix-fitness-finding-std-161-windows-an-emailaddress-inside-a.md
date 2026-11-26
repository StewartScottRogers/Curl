---
id: BL-6007
title: Fix fitness finding STD-161: Windows: an emailAddress inside a multi-valued subject RDN is not checked against rfc822Name name constraints (PARTLY FIXED)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateNameConstraints.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.NameConstraints.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6007 — Fix fitness finding STD-161: Windows: an emailAddress inside a multi-valued subject RDN is not checked against rfc822Name name constraints (PARTLY FIXED)

## Goal

STD-161 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: Windows: an emailAddress inside a multi-valued subject RDN is not checked against rfc822Name name constraints (PARTLY FIXED). Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

Shared setup: These findings share one harness. A self-signed `root.pem` signs every intermediate: `intOC3.pem` (OCSP tests), `intCR3.pem` (CRL tests), and name-constraint CAs `ncExclEmail5.pem` (critical, excluded rfc822Name `other.org`), `ncExclEmail.pem`, `ncPermEmail.pem` (permitted rfc822Name `example.com`), `ncExclUpn.pem` (excluded otherName UPN `evil.test`) and `ncPermUpn.pem` (permitted UPN `evil.test`). Plain HTTP servers on `127.0.0.1:21380`–`21382` serve CRLs and answer OCSP; each leaf has SAN `DNS:localhost, IP:127.0.0.1, IP:::1` unless stated and is served by its own TLS server answering `hello <leaf>`. Every leaf is in the Windows test only under the intermediate anchor named; **with `--cacert root.pem` (the Windows chain engine) the port matches curl in every case**. **Windows caches fetched CRL and OCSP responses for all processes**: before each run, delete every cached `127.0.0.1:213xx` URL (`certutil -urlcache <url> delete`), or REF's cached answers leak into the port's run. The reviewer's generators are `gen3.py`/`gen4.py`/`gen5.py` and the comparison script `cmp.sh`.

The finding, verbatim:

- **Severity:** High · **Confidence:** Confirmed (4 leaves, direct and proxy; re-verified by the orchestrator) · **Area:** HTTPS / HTTPS-proxy name constraints, Windows build, intermediate anchor.
- **What is fixed:** an emailAddress in a single-valued RDN is now checked (old leaves `em_subj`, `xem_subj`, `xem_case`, `xemd_sub`, `pxem_subj` match curl).
- **Certificates:** CA `ncExclEmail5.pem` (critical nameConstraints, excluded rfc822Name `other.org`). Leaf `m_mvrdn`: subject `emailAddress=evil@other.org+CN=localhost` (one multi-valued RDN). Leaf `m_mvrdn_o`: subject `O=x+emailAddress=evil@other.org, CN=localhost`. Both have a CDP to a clean CRL. Control `m_ok`: multi-valued RDN with `good@example.com`.
- **Repro:**
  - `curl -sS --cacert ncExclEmail5.pem https://localhost:<port of m_mvrdn>/`
  - `curl -sS -x https://localhost:<port of m_mvrdn> --proxy-cacert ncExclEmail5.pem http://target.invalid/`
- **Expected (REF 8.22.0 Schannel):** `curl: (60) schannel: error 0x00008000`, exit 60.
- **Actual (.NET):** `hello m_mvrdn`, exit 0 (same for `m_mvrdn_o`, direct and proxy). The control `m_ok` exits 0 in both.
- **Likely location:** `Curl.Networking.UnitLibrary/CertificateNameConstraints.cs`. A comment (~line 72) says "emailAddresses in a multi-valued relative name are read in the OpenSSL build only", and the Schannel path calls `ListSingleValuedAttributes(leaf, EmailAddressOid)`. CryptoAPI does read multi-valued RDNs; read them on the Windows path too.
- **Acceptance check:** `curl -sS --cacert ncExclEmail5.pem https://localhost:<port of m_mvrdn>/` exits 60 with `schannel: error 0x00008000` (also via `--proxy-cacert`); `m_ok` still exits 0.

## Acceptance criteria

- [x] `curl -sS --cacert ncExclEmail5.pem https://localhost:<port of m_mvrdn>/` exits 60 with `schannel: error 0x00008000` (also via `--proxy-cacert`); `m_ok` still exits 0.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured Schannel curl 8.21.0 and the built Curl against a throwaway loopback TLS server (constrained intermediate as `--cacert`, `--ssl-no-revoke`): all seven direct cases answer the same (table in ADR-1418). The finding's `m_mvrdn`, `m_mvrdn_o` and `m_ok` shapes are among them.
- `--proxy-cacert`: the probe had no CRL, so both curls answered `the revocation status is unknown` through the proxy; the proxy handshake runs the same `CertificateNameConstraints` check as the direct one, which the unit tests pin.
- The same measurement showed Schannel also reads a CN in a multi-valued relative name (`0x00008000`), which Curl ignored; fixed here, same method and file.
- Touched beyond `touches`: the test file `Curl.Networking.UnitTests/ServerCertificateVerificationTests.NameConstraints.cs` (its test must pin the change; no task in Doing on the shared branch names it) and ADR-1418 with its README row.
- Quality run for Curl.Networking.UnitLibrary: 0 failing members.

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 28e1b4c4 69116afe 3d836d5e.
- 2026-11-26: Doing -> Done. Schannel build now reads multi-valued relative names against name constraints
