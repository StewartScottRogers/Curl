---
id: BL-6011
title: Fix fitness finding STD-218: Windows: otherName (UPN) name constraints are ignored below an intermediate anchor
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateNameConstraints.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6011 — Fix fitness finding STD-218: Windows: otherName (UPN) name constraints are ignored below an intermediate anchor

## Goal

STD-218 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: Windows: otherName (UPN) name constraints are ignored below an intermediate anchor. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

Shared setup: These findings share one harness. A self-signed `root.pem` signs every intermediate: `intOC3.pem` (OCSP tests), `intCR3.pem` (CRL tests), and name-constraint CAs `ncExclEmail5.pem` (critical, excluded rfc822Name `other.org`), `ncExclEmail.pem`, `ncPermEmail.pem` (permitted rfc822Name `example.com`), `ncExclUpn.pem` (excluded otherName UPN `evil.test`) and `ncPermUpn.pem` (permitted UPN `evil.test`). Plain HTTP servers on `127.0.0.1:21380`–`21382` serve CRLs and answer OCSP; each leaf has SAN `DNS:localhost, IP:127.0.0.1, IP:::1` unless stated and is served by its own TLS server answering `hello <leaf>`. Every leaf is in the Windows test only under the intermediate anchor named; **with `--cacert root.pem` (the Windows chain engine) the port matches curl in every case**. **Windows caches fetched CRL and OCSP responses for all processes**: before each run, delete every cached `127.0.0.1:213xx` URL (`certutil -urlcache <url> delete`), or REF's cached answers leak into the port's run. The reviewer's generators are `gen3.py`/`gen4.py`/`gen5.py` and the comparison script `cmp.sh`.

The finding, verbatim:

- **Severity:** High · **Confidence:** Confirmed (2 runs each; re-verified by the orchestrator) · **Area:** HTTPS name constraints, Windows build, intermediate anchor.
- **Certificates:** `ncExclUpn.pem` (critical nameConstraints, excluded `otherName:UPN:evil.test`) issues leaf `o_x_upn` with SAN `DNS:localhost, IP:127.0.0.1, otherName:UPN:user@evil.test`. `ncPermUpn.pem` (permitted `UPN:evil.test`) issues leaf `o_p_bad` with UPN `user@good.test`. Controls `o_x_ok` / `o_p_ok` carry compliant UPNs.
- **Repro:**
  - `curl -sS --cacert ncExclUpn.pem --ssl-no-revoke https://localhost:<port of o_x_upn>/`
  - `curl -sS --cacert ncPermUpn.pem --ssl-no-revoke https://localhost:<port of o_p_bad>/`
- **Expected (REF):** exit 60 with `schannel: error 0x00008000` (excluded) and `schannel: error 0x00004000` (not permitted).
- **Actual (.NET):** `hello o_x_upn` / `hello o_p_bad`, exit 0. The controls exit 0 in both.
- **Likely location:** `Curl.Networking.UnitLibrary/CertificateNameConstraints.cs`: `SchannelOtherNameTags = [DirectoryNameTag, EmailTag, UriTag]` has no otherName `[0]`. Match UPN (`1.3.6.1.4.1.311.20.2.3`) as CryptoAPI does, or refuse a chain carrying a constraint type the port does not evaluate.
- **Acceptance check:** both repro commands exit 60 with the codes above; `o_x_ok` exits 0.

## Acceptance criteria

- [ ] both repro commands exit 60 with the codes above; `o_x_ok` exits 0.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
