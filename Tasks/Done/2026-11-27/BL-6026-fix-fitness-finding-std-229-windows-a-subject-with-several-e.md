---
id: BL-6026
title: Fix fitness finding STD-229: Windows: a subject with several emailAddress attributes is refused where Schannel accepts it
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateNameConstraints.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.NameConstraints.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6026 — Fix fitness finding STD-229: Windows: a subject with several emailAddress attributes is refused where Schannel accepts it

## Goal

STD-229 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: Windows: a subject with several emailAddress attributes is refused where Schannel accepts it. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

Shared setup: These findings share one harness. A self-signed `root.pem` signs every intermediate: `intOC3.pem` (OCSP tests), `intCR3.pem` (CRL tests), and name-constraint CAs `ncExclEmail5.pem` (critical, excluded rfc822Name `other.org`), `ncExclEmail.pem`, `ncPermEmail.pem` (permitted rfc822Name `example.com`), `ncExclUpn.pem` (excluded otherName UPN `evil.test`) and `ncPermUpn.pem` (permitted UPN `evil.test`). Plain HTTP servers on `127.0.0.1:21380`–`21382` serve CRLs and answer OCSP; each leaf has SAN `DNS:localhost, IP:127.0.0.1, IP:::1` unless stated and is served by its own TLS server answering `hello <leaf>`. Every leaf is in the Windows test only under the intermediate anchor named; **with `--cacert root.pem` (the Windows chain engine) the port matches curl in every case**. **Windows caches fetched CRL and OCSP responses for all processes**: before each run, delete every cached `127.0.0.1:213xx` URL (`certutil -urlcache <url> delete`), or REF's cached answers leak into the port's run. The reviewer's generators are `gen3.py`/`gen4.py`/`gen5.py` and the comparison script `cmp.sh`.

The finding, verbatim:

- **Severity:** Low (fails closed) · **Confidence:** Confirmed (1 batch run plus a matching earlier-run pattern) · **Area:** HTTPS name constraints, Windows build, intermediate anchor.
- **Certificates:** leaf `xem_multi` (subject emails `good@example.com`, then `evil@other.org`) under `ncExclEmail.pem` (excluded `other.org`); leaf `em_multi` (`a@example.com`, then `evil@other.org`) under `ncPermEmail.pem` (permitted `example.com`).
- **Repro:** `curl -sS --cacert ncExclEmail.pem --ssl-no-revoke https://localhost:<port of xem_multi>/` (and `ncPermEmail.pem` with `em_multi`).
- **Expected (REF):** `hello …`, exit 0 (CryptoAPI appears to check only the first emailAddress).
- **Actual (.NET):** exit 60 `schannel: error 0x00008000` / `0x00004000`.
- **Likely location:** `Curl.Networking.UnitLibrary/CertificateNameConstraints.cs`, `ListSubjectNames`.
- **Acceptance check:** both exit 0, while a leaf with a single `evil@other.org` still exits 60.

## Acceptance criteria

- [x] both exit 0, while a leaf with a single `evil@other.org` still exits 60.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured curl 8.21.0 Schannel against a throwaway C# loopback TLS server (intermediate as `--cacert`, `--ssl-no-revoke`), 28 cases; table in ADR-1455. After the fix Curl gave the same exit code and message line in all 28.
- Schannel checks only the first subject emailAddress in encoding order (the finding), and none when the subjectAltName holds an rfc822Name (found by the same measurement; same lines of `ListSchannelNames`, so fixed here). A single `evil@other.org` is still exit 60. Subject CNs are all still checked.
- Touches gained the test file for the ADR-0482 test; no task in Doing named it. ADR-1422 gained an "Amended by ADR-1455" line.
- Build clean, fast tests green, Measure-CodeQuality for Curl.Networking.UnitLibrary: 100% line and branch, 0 failing members.
- Linux and macOS: only the Schannel path changed and the new tests call it by flag with no platform-dependent output.

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
- 2026-11-26: Backlog -> Doing. Criteria at claim: ed8a100d 69116afe 3d836d5e.
- 2026-11-26: Doing -> Done. The Schannel build checks only the first subject emailAddress, and none beside an rfc822Name, as curl does
