---
id: BL-5432
title: Fix fitness finding STD-114: Windows: name constraints are not applied to the subject CN when the leaf has no DNS SAN, so a CN-only certificate escapes permitted and excluded DNS subtrees
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateNameConstraints.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5432 — Fix fitness finding STD-114: Windows: name constraints are not applied to the subject CN when the leaf has no DNS SAN, so a CN-only certificate escapes permitted and excluded DNS subtrees

## Goal

STD-114 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Windows: name constraints are not applied to the subject CN when the leaf has no DNS SAN, so a CN-only certificate escapes permitted and excluded DNS subtrees. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** High (name-constraint bypass: Schannel matches the host against the CN when there is no DNS SAN, so a constrained CA can issue a certificate for an excluded host; default configuration and proxy)
- **Confidence:** Confirmed (3+ runs by the reviewer; re-reproduced twice by the orchestrator)
- **Protocol/area:** HTTPS and HTTPS-proxy verification, `--cacert` / `--proxy-cacert` holding a name-constrained intermediate, Windows
- **Repro:**
  1. Root (self-signed CA) issues two intermediates, CA:TRUE, keyCertSign+cRLSign: `ncExclDns.pem` with critical `nameConstraints = excluded;DNS:localhost`, and `ncPermDns.pem` with critical `permitted;DNS:example.com`.
  2. Leaves with subject `CN=localhost`, EKU serverAuth and **no SAN extension**: `ex_cnonly` under ncExclDns, `pd_cnonly` under ncPermDns. Also a leaf `CN=localhost` with only `SAN IP:127.0.0.1` under ncPermDns.
  3. Serve each as leaf + intermediate (Python ssl server answering 200 `hello`).
  4. Run:
     - `curl -sS https://localhost:<p1>/ --cacert ncExclDns.pem`
     - `curl -sS https://localhost:<p2>/ --cacert ncPermDns.pem --ssl-no-revoke`
     - `curl -sS https://127.0.0.1:<p3>/ --cacert ncPermDns.pem --ssl-no-revoke`
     - `curl -sS -x https://localhost:<pp> --proxy-cacert ncExclDns.pem http://127.0.0.1:40999/x` (proxy presents ex_cnonly)
- **Expected (REF):** `curl: (60) schannel: error 0x00008000`; `error 0x00004000`; `error 0x00004000`; proxy `error 0x00008000`; all exit 60. Control: a leaf with only `SAN IP:127.0.0.1` and `CN=www.example.com` under ncPermDns is accepted by both (REF uses the CN when no DNS SAN exists).
- **Actual (PORT):** exit 0 with the body in all four cases.
- **Likely location:** `Curl.Networking.UnitLibrary/CertificateNameConstraints.cs` `ListNames` reads only SAN DNS and IP entries. When the leaf has no dNSName SAN, add the subject CN(s) as dNSName names for the constraint check, as CryptoAPI does.
- **Acceptance check:** the first three commands exit 60 with `0x00008000`, `0x00004000`, `0x00004000`; the proxy command exits 60 with `0x00008000`; the IP-SAN-only `CN=www.example.com` control still exits 0.

## Acceptance criteria

- [x] the first three commands exit 60 with `0x00008000`, `0x00004000`, `0x00004000`; the proxy command exits 60 with `0x00008000`; the IP-SAN-only `CN=www.example.com` control still exits 0.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- ListNames now adds the subject CN(s) as DNS names when the SAN has no dNSName (CryptoAPI behaviour); multi-valued RDNs are skipped. Unit tests pin CN-only (0x4000, 0x8000), IP-SAN with outside CN, and the IP-SAN + CN=www.example.com control. Replaced the old test that expected a SAN-less leaf to be accepted. Quality run clean. Real Windows curl comparison not run; expected codes taken from the finding.

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-114 (High).
- 2026-11-18: Backlog -> Doing. Criteria at claim: cc75741b 69116afe 3d836d5e.
- 2026-11-18: Doing -> Done. CN used as DNS name for name constraints
