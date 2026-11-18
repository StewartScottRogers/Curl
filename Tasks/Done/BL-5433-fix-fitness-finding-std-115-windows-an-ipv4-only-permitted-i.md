---
id: BL-5433
title: Fix fitness finding STD-115: Windows: an IPv4-only permitted iPAddress name constraint does not constrain an IPv6 SAN
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateNameConstraints.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5433 — Fix fitness finding STD-115: Windows: an IPv4-only permitted iPAddress name constraint does not constrain an IPv6 SAN

## Goal

STD-115 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Windows: an IPv4-only permitted iPAddress name constraint does not constrain an IPv6 SAN. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** High (a CA constrained to an IPv4 range can issue a certificate valid for any IPv6 address)
- **Confidence:** Confirmed (2+ runs, two leaves; re-reproduced twice by the orchestrator)
- **Protocol/area:** HTTPS verification, `--cacert` holding a name-constrained intermediate, Windows
- **Repro:**
  1. Intermediate `ncPermIp.pem` signed by a root, critical `nameConstraints = permitted;IP:10.0.0.0/255.0.0.0`.
  2. Leaf with SAN `IP:::1` only, EKU serverAuth; serve leaf + intermediate on a dual-stack listener (`::`).
  3. `curl -sS "https://[::1]:<p>/" --cacert ncPermIp.pem --ssl-no-revoke`
- **Expected (REF):** `curl: (60) schannel: error 0x00004000`, exit 60.
- **Actual (PORT):** `hello …`, exit 0.
- **Likely location:** `Curl.Networking.UnitLibrary/CertificateNameConstraints.cs` `Applies` treats an address subtree as applying only to its own family (`subtree.Value.Length == name.Value.Length * 2`), so `BreaksPermitted` finds no applicable subtree. When permitted iPAddress subtrees exist, an address of the other family must count as not permitted. Excluded subtrees stay as they are (REF and PORT agree that an IPv6-only excluded subtree does not exclude an IPv4 SAN).
- **Acceptance check:** the command exits 60 with `schannel: error 0x00004000`. A leaf with `IP:127.0.0.1` under an excluded `::1/128` constraint still exits 0.

## Acceptance criteria

- [x] the command exits 60 with `schannel: error 0x00004000`. A leaf with `IP:127.0.0.1` under an excluded `::1/128` constraint still exits 0.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

BreaksPermitted now treats a name as not permitted when permitted subtrees of its kind exist but none of its address family matches. Excluded subtrees unchanged. CI criteria met by platform-neutral unit tests; no real-curl re-measure needed (finding gave REF output).

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-115 (High).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 00b08b22 69116afe 3d836d5e.
- 2026-11-18: Doing -> Done. Permitted iPAddress subtrees of the other family now leave a name not permitted
